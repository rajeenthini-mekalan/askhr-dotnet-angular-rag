using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using AskHR.Application.Documents;
using AskHR.Tests.Auth;

namespace AskHR.Tests.Documents;

public class DocumentEndpointTests(TestAuthApiFactory factory) : IClassFixture<TestAuthApiFactory>
{
    private const string Url = "/api/documents";

    private HttpClient ClientAs(params string[] roles)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.CreateToken("admin@askhr.demo", roles));
        return client;
    }

    private HttpClient Admin => ClientAs("hr-admin");

    private static MultipartFormDataContent FileForm(string fileName, byte[] content, string contentType = "application/octet-stream")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private async Task<HttpResponseMessage> Upload(HttpClient client, string fileName, byte[] content) =>
        await client.PostAsync(Url, FileForm(fileName, content));

    [Fact]
    public async Task Upload_without_token_returns_401() =>
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Upload(factory.CreateClient(), "policy.txt", Utf8("Leave policy"))).StatusCode);

    [Fact]
    public async Task Employee_cannot_upload_403() =>
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Upload(ClientAs("employee"), "policy.txt", Utf8("Leave policy"))).StatusCode);

    [Theory]
    [InlineData("policy.docx", "PK fake word file")]
    [InlineData("policy.exe", "MZ")]
    [InlineData("policy", "no extension")]
    [InlineData("policy.pdf", "this is not a pdf")]   // right name, wrong signature
    public async Task Wrong_type_returns_400(string fileName, string content)
    {
        var response = await Upload(Admin, fileName, Utf8(content));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("\"file\"", await response.Content.ReadAsStringAsync());   // ValidationProblem keyed on the field
    }

    [Fact]
    public async Task Binary_content_with_a_txt_name_returns_400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(Admin, "policy.txt", [0x48, 0x00, 0x49])).StatusCode);

    [Fact]
    public async Task File_over_10_MB_returns_400()
    {
        var content = new byte[UploadRules.MaxBytes + 1];
        Array.Fill(content, (byte)'a');

        var response = await Upload(Admin, "big.txt", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("limit is 10 MB", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Empty_file_returns_400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(Admin, "empty.txt", [])).StatusCode);

    [Fact]
    public async Task Missing_file_field_returns_400()
    {
        var form = new MultipartFormDataContent { { new StringContent("x"), "notfile" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.PostAsync(Url, form)).StatusCode);
    }

    [Fact]
    public async Task Upload_returns_202_stores_the_document_as_Processing_and_queues_ingestion()
    {
        var response = await Upload(Admin, "leave-policy.txt", Utf8("Annual leave is 18 days."));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DocumentSummary>();
        Assert.NotNull(body);
        Assert.Equal("leave-policy.txt", body.Name);
        Assert.Equal("text/plain", body.ContentType);
        Assert.Equal("Processing", body.Status);
        Assert.Equal("admin@askhr.demo", body.UploadedBy);
        Assert.Equal(0, body.ChunkCount);
        Assert.EndsWith($"{Url}/{body.Id}", response.Headers.Location!.ToString());
        Assert.Contains(body.Id, factory.IngestionQueue.Enqueued);

        await using var db = factory.CreateDbContext();
        Assert.Equal(Utf8("Annual leave is 18 days."), db.DocumentFiles.Single(f => f.DocumentId == body.Id).Content);
    }

    [Fact]
    public async Task A_real_pdf_is_accepted()
    {
        var response = await Upload(Admin, "policy.pdf", PdfFactory.WithText("Sick leave is 10 days."));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("application/pdf", (await response.Content.ReadFromJsonAsync<DocumentSummary>())!.ContentType);
    }

    [Fact]
    public async Task List_and_get_show_an_uploaded_document()
    {
        var uploaded = await (await Upload(Admin, "list-me.txt", Utf8("Remote work policy")))
            .Content.ReadFromJsonAsync<DocumentSummary>();

        var list = await Admin.GetFromJsonAsync<List<DocumentSummary>>(Url);
        var one = await Admin.GetFromJsonAsync<DocumentSummary>($"{Url}/{uploaded!.Id}");

        Assert.Contains(list!, d => d.Id == uploaded.Id && d.Name == "list-me.txt" && d.Status == "Processing");
        Assert.Equal(uploaded.Id, one!.Id);
    }

    [Fact]
    public async Task Delete_removes_the_document_with_its_file_and_chunks()
    {
        var uploaded = await (await Upload(Admin, "delete-me.txt", Utf8("Temporary")))
            .Content.ReadFromJsonAsync<DocumentSummary>();
        await using (var arrange = factory.CreateDbContext())
        {
            arrange.Chunks.Add(new Domain.Documents.Chunk(uploaded!.Id, 0, "Temporary"));
            await arrange.SaveChangesAsync();
        }

        var response = await Admin.DeleteAsync($"{Url}/{uploaded!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync($"{Url}/{uploaded.Id}")).StatusCode);
        await using var db = factory.CreateDbContext();
        Assert.False(db.Chunks.Any(c => c.DocumentId == uploaded.Id));          // cascaded by the database
        Assert.False(db.DocumentFiles.Any(f => f.DocumentId == uploaded.Id));
    }

    [Fact]
    public async Task Delete_unknown_document_returns_404() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);

    [Fact]
    public async Task Employee_cannot_delete_403() =>
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientAs("employee").DeleteAsync($"{Url}/{Guid.NewGuid()}")).StatusCode);
}
