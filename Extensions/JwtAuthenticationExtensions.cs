using System.Text;
using Fakturio.Api.Authorization;
using Fakturio.Api.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Fakturio.Api.Extensions;

public static class JwtAuthenticationExtensions
{
    /// <summary>Known insecure placeholder — never allowed outside Development.</summary>
    public const string DevelopmentPlaceholderKey = "dev-only-change-me-fakturio-jwt-secret-key-32+";

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();

        ValidateJwtKey(jwt.Key, environment);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                };

                // SignalR (browser WebSocket) sends JWT as ?access_token=
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken)
                            && path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddSingleton<IAuthorizationHandler, AdminAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();

            options.AddPolicy("Admin", policy =>
                policy.Requirements.Add(new AdminRequirement()));
        });
        return services;
    }

    internal static void ValidateJwtKey(string? key, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must be configured and at least 32 characters long.");
        }

        if (!environment.IsDevelopment()
            && string.Equals(key, DevelopmentPlaceholderKey, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Jwt:Key must not use the development placeholder outside Development. "
                + "Set Jwt__Key / JWT_KEY to a unique secret (e.g. openssl rand -base64 48).");
        }
    }
}
