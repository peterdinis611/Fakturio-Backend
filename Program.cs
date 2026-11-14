using System.Text.Json.Serialization;
using Fakturio.Api.Data;
using Fakturio.Api.Extensions;
using Fakturio.Api.Hubs;
using Fakturio.Api.Infrastructure;
using Fakturio.Api.Middleware;
using Fakturio.Api.Services;
using Fakturio.Api.Validation;
using FluentValidation;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("Application", "Fakturio.Api")
    .WriteTo.Console(outputTemplate: SerilogExtensions.DevelopmentOutputTemplate)
    .CreateBootstrapLogger();

try
{
    QuestPDF.Settings.License = LicenseType.Community;

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.AddFakturioSerilog();

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // Reverse proxies (Docker / nginx / Cloudflare) terminate TLS in front of the container.
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

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

    builder.Services.AddFakturioOpenApi();
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddValidatorsFromAssemblyContaining<CreateCustomerRequestValidator>();
    builder.Services.AddApplicationCaching(builder.Configuration);
    builder.Services.AddApplicationServices(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddFakturioRateLimiting(builder.Configuration);
    builder.Services.AddSignalR();

    var redisConnection =
        builder.Configuration["Cache:RedisConnection"]
        ?? builder.Configuration.GetConnectionString("Redis");
    var healthChecks = builder.Services.AddHealthChecks();
    if (!string.IsNullOrWhiteSpace(redisConnection)
        && builder.Configuration.GetValue("Cache:UseRedis", true))
    {
        healthChecks.AddRedis(redisConnection, name: "redis");
    }

    // SignalR negotiate uses credentials — AllowAnyOrigin is incompatible.
    var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? ["http://localhost:3000", "http://127.0.0.1:3000"];
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
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

        if (app.Environment.IsDevelopment())
        {
            var hasher = scope.ServiceProvider
                .GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<Fakturio.Api.Models.AppUser>>();
            var seedLogger = scope.ServiceProvider
                .GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger("DemoDataSeeder");
            await DemoDataSeeder.SeedAsync(db, hasher, seedLogger);
        }
    }

    app.UseForwardedHeaders();

    if (app.Environment.IsDevelopment())
    {
        app.MapFakturioOpenApi();
    }

    app.UseFakturioMiddleware();
    app.UseFakturioRequestLogging();
    app.UseExceptionHandler();
    app.UseCors();
    if (!builder.Configuration.GetValue("DisableHttpsRedirection", false))
        app.UseHttpsRedirection();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseMiddleware<UserIdLogContextMiddleware>();
    app.UseAuthorization();

    // Public logos only — invoice attachments must go through authenticated download APIs.
    var logosRoot = Path.Combine(app.Environment.ContentRootPath, "uploads", "logos");
    Directory.CreateDirectory(logosRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(logosRoot),
        RequestPath = "/uploads/logos",
        OnPrepareResponse = ctx =>
        {
            SecurityHeadersMiddleware.Apply(
                ctx.Context.Response.Headers,
                SecurityHeadersMiddleware.IsHttps(ctx.Context.Request));
            ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Context.Response.Headers["Content-Disposition"] = "inline";
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=86400";
        },
    });

    app.MapControllers();
    app.MapHealthChecks("/health").AllowAnonymous();
    app.MapHub<SignalHub>("/hubs/signal");

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
