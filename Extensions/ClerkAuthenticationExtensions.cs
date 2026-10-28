using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Fakturio.Api.Extensions;

public static class ClerkAuthenticationExtensions
{
    public static IServiceCollection AddClerkJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authority = configuration["Clerk:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
        {
            // Auth endpoints return 401 until Clerk:Authority is configured.
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateLifetime = false,
                        ValidateIssuerSigningKey = false,
                    };
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            context.Fail("Clerk:Authority is not configured.");
                            return Task.CompletedTask;
                        },
                    };
                });
            services.AddAuthorization();
            return services;
        }

        var authorizedParties = configuration.GetSection("Clerk:AuthorizedParties").Get<string[]>()
            ?? [];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority.TrimEnd('/');
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false,
                    NameClaimType = ClaimTypes.NameIdentifier,
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (authorizedParties.Length == 0)
                            return Task.CompletedTask;

                        var azp = context.Principal?.FindFirstValue("azp");
                        if (string.IsNullOrEmpty(azp) ||
                            !authorizedParties.Contains(azp, StringComparer.OrdinalIgnoreCase))
                        {
                            context.Fail("Invalid azp claim.");
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();
        return services;
    }
}
