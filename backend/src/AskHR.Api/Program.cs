using AskHR.Api.Auth;
using AskHR.Api.Chat;
using AskHR.Api.Documents;
using AskHR.Application.Chat;
using AskHR.Infrastructure.Ai;
using AskHR.Infrastructure.Ingestion;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();   // "ollama" and "sql" checks are added below; Keycloak comes later

// ---------- AI: IChatClient backed by local Ollama (Infrastructure) + the use case (Application) ----------
builder.Services.AddAskHrAi();        // also adds the "ollama" health check
builder.Services.AddScoped<AskQuestion>();

// ---------- Documents: EF Core on SQL Server + upload/ingestion use cases + Hangfire (Infrastructure) ----------
builder.Services.AddAskHrPersistence();                       // connection string "AskHr" from user secrets / env vars
builder.Services.AddAskHrIngestion(builder.Configuration);    // "Ingestion" section: worker count, server, dashboard
var ingestion = builder.Configuration.GetSection(IngestionOptions.Section).Get<IngestionOptions>() ?? new();
var mapHangfireDashboard = builder.Environment.IsDevelopment() && ingestion.Dashboard;

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

        // Development only: let the browser-opened Hangfire dashboard pass its token in the URL once (see HangfireDashboard).
        if (mapHangfireDashboard)
            HangfireDashboard.AllowDashboardTokenFromQueryOrCookie(options.Events = new JwtBearerEvents());
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

api.MapDocumentEndpoints();   // /api/documents: upload, list, get, delete (HrAdmin policy)
api.MapChatEndpoints();       // POST /api/chat (Employee policy)

if (mapHangfireDashboard)
    app.MapAskHrHangfireDashboard();   // /hangfire, hr-admin only

app.Run();
