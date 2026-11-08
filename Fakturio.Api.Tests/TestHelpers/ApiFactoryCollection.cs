namespace Fakturio.Api.Tests.TestHelpers;

/// <summary>
/// One shared host for all HTTP integration tests — Serilog's bootstrap logger
/// freezes on the first host build and rejects parallel WebApplicationFactory hosts.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiFactoryCollection : ICollectionFixture<FakturioApiFactory>
{
    public const string Name = "ApiFactory";
}
