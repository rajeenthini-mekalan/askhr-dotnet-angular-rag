using System.Security.Claims;
using System.Security.Cryptography;
using AskHR.Application.Abstractions;
using AskHR.Infrastructure.Persistence;
using AskHR.Tests.Fakes;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AskHR.Tests.Auth;

/// <summary>
/// Boots the real API but makes the JWT bearer handler trust a test RSA key instead of Keycloak's JWKS.
/// Everything else (audience check, lifetime check, "roles" claim mapping, policies) is the production code path,
/// so these tests need no running Keycloak. IChatClient is a <see cref="FakeChatClient"/>, so no Ollama is needed either.
/// The database is in-memory SQLite and Hangfire is replaced by <see cref="FakeIngestionQueue"/>, so no SQL Server either.
/// </summary>
public sealed class TestAuthApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "https://keycloak.test/realms/askhr";
    public const string Audience = "askhr-api";

    private readonly RsaSecurityKey _key = new(RSA.Create(2048)) { KeyId = "test-key" };

    private readonly SqliteTestDatabase _database = new();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // Read by Program before the host is built: no Hangfire workers and no dashboard in tests.
        builder.UseSetting("Ingestion:RunServer", "false");
        builder.UseSetting("Ingestion:Dashboard", "false");

        builder.ConfigureTestServices(services =>
        {
            // Swap SQL Server for SQLite. EF Core 9+ also keeps the UseSqlServer(...) callback as an
            // IDbContextOptionsConfiguration, which must go too, or the context would have two providers.
            services.RemoveAll<DbContextOptions<AskHrDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AskHrDbContext>>();
            services.AddDbContext<AskHrDbContext>(options => options.UseSqlite(_database.Connection));

            services.RemoveAll<IIngestionQueue>();
            services.AddSingleton<IIngestionQueue>(IngestionQueue);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Replace "download metadata from the Authority" with a fixed, in-memory configuration.
                var config = new OpenIdConnectConfiguration { Issuer = Issuer };
                config.SigningKeys.Add(_key);
                options.Configuration = config;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
                options.TokenValidationParameters.ValidIssuer = Issuer;
            });

            // Never call the real model from tests: replace the whole Ollama pipeline with a fake.
            services.RemoveAll<IChatClient>();
            services.AddSingleton<IChatClient>(ChatClient);

            // The Ollama health check would depend on a local Ollama; drop it so /health is deterministic in CI.
            // (OllamaHealthCheck has its own unit tests.)
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                foreach (var r in options.Registrations.Where(r => r.Name == "ollama").ToList())
                    options.Registrations.Remove(r);
            });
        });
    }

    public FakeChatClient ChatClient { get; } = new();

    public FakeIngestionQueue IngestionQueue { get; } = new();

    /// <summary>A context on the same in-memory database the API uses, for arranging and asserting.</summary>
    public AskHrDbContext CreateDbContext() => _database.CreateContext();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _database.Dispose();
    }

    /// <summary>Creates a signed access token shaped like a Keycloak one.</summary>
    public string CreateToken(string username, string[] roles, string audience = Audience)
    {
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("preferred_username", username),
        };
        claims.AddRange(roles.Select(r => new Claim("roles", r)));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256),
        });
    }
}
