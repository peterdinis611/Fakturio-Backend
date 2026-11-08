using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fakturio.Api.Data;
using Fakturio.Api.Dtos;

namespace Fakturio.Api.Tests.TestHelpers;

public static class DemoAuth
{
    public static readonly Guid DemoCustomerStudioId =
        Guid.Parse("a3333333-3333-3333-3333-333333333301");

    public static async Task<AuthResponse> LoginAsDemoAsync(HttpClient client)
    {
        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            DemoDataSeeder.Email,
            DemoDataSeeder.Password));

        loginRes.EnsureSuccessStatusCode();
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options)
            ?? throw new InvalidOperationException("Login returned empty body.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return auth;
    }

    public static async Task<AuthResponse> RegisterAndLoginAsync(
        HttpClient client,
        string email,
        string password = "Test1234!")
    {
        var registerRes = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email,
            password,
            "Test",
            "User"));
        registerRes.EnsureSuccessStatusCode();

        var loginRes = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginRes.EnsureSuccessStatusCode();
        var auth = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(TestJson.Options)
            ?? throw new InvalidOperationException("Login returned empty body.");

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return auth;
    }
}
