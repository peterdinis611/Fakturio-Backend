using System.Text.Json.Serialization;
using Fakturio.Api.Data;
using Fakturio.Api.Extensions;
using Fakturio.Api.Infrastructure;
using Fakturio.Api.Services;
using Fakturio.Api.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("Application", "Fakturio.Api")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    QuestPDF.Settings.License = LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.AddFakturioSerilog();

    builder.Services.AddControllers(options =>
    {
        options.Filters.Add<ValidationFilter>();
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest,
                Type = $"https://httpstatuses.com/{StatusCodes.Status400BadRequest}",
                Instance = context.HttpContext.Request.Path,
                Detail = "One or more validation errors occurred."
            };
            problem.Extensions["code"] = "validation_failed";
            return new BadRequestObjectResult(problem);
        };
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

    builder.Services.AddOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();
    builder.Services.AddApplicationCaching(builder.Configuration);
    builder.Services.AddApplicationServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
    });

    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? "Data Source=fakturio.db";

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite(connectionString));

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await DatabaseInitializer.InitializeAsync(db);
    }

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseFakturioRequestLogging();
    app.UseExceptionHandler();
    app.UseCors();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
