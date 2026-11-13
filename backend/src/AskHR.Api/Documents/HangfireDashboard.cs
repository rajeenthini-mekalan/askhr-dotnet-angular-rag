using AskHR.Api.Auth;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AskHR.Api.Documents;

/// <summary>
/// The Hangfire dashboard (/hangfire): queued, running, failed jobs with their exceptions. Development only, hr-admin only.
///
/// The catch: the dashboard is opened in a browser by typing a URL, and a browser never adds an
/// "Authorization: Bearer" header to a plain navigation. So, in Development only, the token may come once as
/// /hangfire?access_token=eyJ...; after it validates it is kept in an HttpOnly cookie limited to /hangfire, so the
/// dashboard's own CSS/JS/polling requests carry it too. The cookie dies with the token (5 minutes): open the URL
/// again with a fresh token. Never do this in production (tokens in URLs end up in logs and browser history);
/// there you would sign in to the dashboard through Keycloak (OIDC + cookie) instead.
/// </summary>
public static class HangfireDashboard
{
    public const string Path = "/hangfire";
    private const string QueryKey = "access_token";
    private const string CookieName = "askhr_hangfire_token";

    /// <summary>Adds the token-from-query/cookie handling to the JWT bearer events (Development only).</summary>
    public static void AllowDashboardTokenFromQueryOrCookie(JwtBearerEvents events)
    {
        events.OnMessageReceived = context =>
        {
            if (context.Request.Path.StartsWithSegments(Path) && string.IsNullOrEmpty(context.Request.Headers.Authorization))
            {
                context.Token = context.Request.Query.TryGetValue(QueryKey, out var fromQuery) && !string.IsNullOrEmpty(fromQuery)
                    ? fromQuery.ToString()
                    : context.Request.Cookies[CookieName];
            }
            return Task.CompletedTask;
        };

        events.OnTokenValidated = context =>
        {
            // Remember a valid token that came in the query string, for the dashboard's follow-up requests.
            if (context.Request.Path.StartsWithSegments(Path) &&
                context.Request.Query.TryGetValue(QueryKey, out var token) && !string.IsNullOrEmpty(token))
            {
                context.Response.Cookies.Append(CookieName, token.ToString(), new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = Path,
                    MaxAge = TimeSpan.FromMinutes(5),
                });
            }
            return Task.CompletedTask;
        };
    }

    public static void MapAskHrHangfireDashboard(this WebApplication app) =>
        app.MapHangfireDashboard(Path, new DashboardOptions
            {
                // Hangfire's default filter allows local requests only. We clear it and let ASP.NET Core
                // authorization decide instead (the RequireAuthorization below), like any other endpoint.
                Authorization = [],
                DashboardTitle = "AskHR jobs",
                DisplayStorageConnectionString = false,
            })
            .RequireAuthorization(Policies.HrAdmin);
}
