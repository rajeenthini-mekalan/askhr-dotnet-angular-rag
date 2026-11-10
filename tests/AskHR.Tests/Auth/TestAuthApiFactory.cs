using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace AskHR.Tests.Auth;

/// <summary>
/// Boots the real API but makes the JWT bearer handler trust a test RSA key instead of Keycloak's JWKS.
/// Everything else (audience check, lifetime check, "roles" claim mapping, policies) is the production code path,
/// so these tests need no running Keycloak.
/// </summary>
public sealed class TestAuthApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "https://keycloak.test/realms/askhr";
    public const string Audience = "askhr-api";

    private readonly RsaSecurityKey _key = new(RSA.Create(2048)) { KeyId = "test-key" };

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Replace "download metadata from the Authority" with a fixed, in-memory configuration.
                var config = new OpenIdConnectConfiguration { Issuer = Issuer };
                config.SigningKeys.Add(_key);
                options.Configuration = config;
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
                options.TokenValidationParameters.ValidIssuer = Issuer;
            });
        });
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
