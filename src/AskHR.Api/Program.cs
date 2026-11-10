using AskHR.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();   // SQL Server, Keycloak and Ollama checks are added in later milestones

// ---------- Authentication: Keycloak access tokens (JWT) ----------
var auth = builder.Configuration.GetSection(AuthOptions.Section).Get<AuthOptions>()
           ?? throw new InvalidOperationException($"Missing '{AuthOptions.Section}' configuration.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Authority = the realm URL. The handler downloads /.well-known/openid-configuration from it
        // to learn the issuer and the signing keys (JWKS), and refreshes them when Keycloak rotates keys.
        options.Authority = auth.Authority;
        options.Audience = auth.Audience;                    // token must carry aud: askhr-api
        options.RequireHttpsMetadata = auth.RequireHttpsMetadata; // false only for local http Keycloak

        // Keep claim names exactly as Keycloak sends them ("sub", "roles", ...) instead of the
        // legacy WS-Fed URIs (http://schemas.xmlsoap.org/...) that .NET maps them to by default.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "preferred_username";
        // The realm sends realm roles as a flat "roles" array (a protocol mapper in askhr-realm.json),
        // so pointing RoleClaimType at it makes User.IsInRole("hr-admin") and RequireRole work.
        options.TokenValidationParameters.RoleClaimType = "roles";
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.HrAdmin, policy => policy.RequireRole(Roles.HrAdmin))
    .AddPolicy(Policies.Employee, policy => policy.RequireRole(Roles.Employee, Roles.HrAdmin));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").AllowAnonymous();

var api = app.MapGroup("/api").RequireAuthorization();   // every /api endpoint needs a valid token by default

// Placeholder until M3: proves the role check end to end.
api.MapGet("/documents", () => Results.Ok(Array.Empty<object>()))
   .RequireAuthorization(Policies.HrAdmin)
   .WithName("ListDocuments");

app.Run();
