using System.Net;
using System.Net.Http.Headers;

namespace AskHR.Tests.Auth;

public class DocumentsAuthorizationTests(TestAuthApiFactory factory) : IClassFixture<TestAuthApiFactory>
{
    private const string DocumentsUrl = "/api/documents";

    private async Task<HttpStatusCode> GetDocumentsAs(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync(DocumentsUrl);
        return response.StatusCode;
    }

    [Fact]
    public async Task No_token_returns_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized, await GetDocumentsAs(null));

    [Fact]
    public async Task Employee_returns_403() =>
        Assert.Equal(HttpStatusCode.Forbidden,
            await GetDocumentsAs(factory.CreateToken("employee@askhr.demo", ["employee", "default-roles-askhr"])));

    [Fact]
    public async Task HrAdmin_returns_200() =>
        Assert.Equal(HttpStatusCode.OK,
            await GetDocumentsAs(factory.CreateToken("admin@askhr.demo", ["hr-admin", "default-roles-askhr"])));

    [Fact]
    public async Task Token_for_another_audience_returns_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            await GetDocumentsAs(factory.CreateToken("admin@askhr.demo", ["hr-admin"], audience: "account")));

    [Fact]
    public async Task Health_stays_anonymous() =>
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/health")).StatusCode);
}
