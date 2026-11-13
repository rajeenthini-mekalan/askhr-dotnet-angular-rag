using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AskHR.Application.Chat;
using AskHR.Tests.Auth;
using Microsoft.Extensions.AI;

namespace AskHR.Tests.Chat;

public class ChatEndpointTests(TestAuthApiFactory factory) : IClassFixture<TestAuthApiFactory>
{
    private const string ChatUrl = "/api/chat";

    private HttpClient ClientAs(params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken("employee@askhr.demo", roles));
        return client;
    }

    [Fact]
    public async Task No_token_returns_401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(ChatUrl, new { question = "How many leave days?" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_without_an_app_role_returns_403()
    {
        var response = await ClientAs("default-roles-askhr").PostAsJsonAsync(ChatUrl, new { question = "Hi" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("employee")]
    [InlineData("hr-admin")]
    public async Task Employee_and_hr_admin_get_the_model_answer(string role)
    {
        factory.ChatClient.ThrowOnCall = null;
        factory.ChatClient.Answer = "You get 14 days of annual leave.";

        var response = await ClientAs(role).PostAsJsonAsync(ChatUrl, new { question = "How many leave days?" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChatAnswer>();
        Assert.Equal("You get 14 days of annual leave.", body!.Answer);
        Assert.Equal("fake-model", body.Model);
        Assert.Equal(42, body.InputTokens);
        Assert.Equal(7, body.OutputTokens);
    }

    [Fact]
    public async Task System_prompt_and_trimmed_question_are_sent_to_the_model()
    {
        factory.ChatClient.ThrowOnCall = null;

        await ClientAs("employee").PostAsJsonAsync(ChatUrl, new { question = "  Can I work from home?  " });

        var messages = factory.ChatClient.LastMessages;
        Assert.Collection(messages,
            m => { Assert.Equal(ChatRole.System, m.Role); Assert.Equal(Prompts.System, m.Text); },
            m => { Assert.Equal(ChatRole.User, m.Role); Assert.Equal("Can I work from home?", m.Text); });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Empty_question_returns_400(string? question)
    {
        var response = await ClientAs("employee").PostAsJsonAsync(ChatUrl, new { question });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Too_long_question_returns_400()
    {
        var question = new string('a', AskQuestion.MaxQuestionLength + 1);
        var response = await ClientAs("employee").PostAsJsonAsync(ChatUrl, new { question });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Model_unreachable_returns_503()
    {
        factory.ChatClient.ThrowOnCall = new HttpRequestException("Connection refused");
        try
        {
            var response = await ClientAs("employee").PostAsJsonAsync(ChatUrl, new { question = "Hello?" });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            factory.ChatClient.ThrowOnCall = null;
        }
    }
}
