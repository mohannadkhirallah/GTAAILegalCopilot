using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Gta.LegalCopilot.Application.Abstractions;
using Gta.LegalCopilot.Application.Common;
using Gta.LegalCopilot.Domain.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gta.LegalCopilot.Tests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "gta-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:RootPath", DataRoot);
        builder.UseSetting("AzureOpenAI:Endpoint", "");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(DataRoot)) Directory.Delete(DataRoot, recursive: true);
    }
}

public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DemoCases_AreListed()
    {
        var list = await _client.GetFromJsonAsync<List<DemoCaseInfo>>("/api/demo-cases", JsonDefaults.Options);
        Assert.Equal(["siemens", "dohaTech", "lusail"], list!.Select(x => x.Key));
    }

    [Fact]
    public async Task Stream_EmitsSectionsAndFinalMemo_AndDocxIsValid()
    {
        (await _client.PostAsync("/api/demo-cases/siemens/load", null)).EnsureSuccessStatusCode();

        var sse = await _client.GetStringAsync("/api/dossiers/DEMO-SIEMENS/memo/stream");
        Assert.Contains("event: analysis", sse);
        Assert.Contains("event: section_start", sse);
        Assert.Contains("event: memo", sse);
        Assert.Contains("event: done", sse);

        var memo = await _client.GetFromJsonAsync<AssembledMemo>("/api/dossiers/DEMO-SIEMENS/memo", JsonDefaults.Options);
        Assert.Equal("مذكرة رد وتعقيب موضوعي وشكلي على التظلم الضريبي", memo!.DocumentType);
        Assert.Equal("السيد / رئيس لجنة التظلم الضريبي المحترم،،،", memo.Metadata.Addressee);
        Assert.Contains("الحكم بعدم قبول التظلم شكلاً لفوات المواعيد وتخطي مرحلة الاعتراض الإداري الوجوبية", memo.FinalRequests[0]);
        Assert.Contains("على سبيل الاحتياط الكلي", memo.Sections[MemoSections.SubstantiveDefense]);
        Assert.Equal("DETERMINISTIC_TEMPLATE", memo.NarrativeSource);

        var docx = await _client.GetAsync("/api/dossiers/DEMO-SIEMENS/memo/docx");
        docx.EnsureSuccessStatusCode();
        using var doc = WordprocessingDocument.Open(new MemoryStream(await docx.Content.ReadAsByteArrayAsync()), false);
        var errors = new OpenXmlValidator().Validate(doc).Select(e => e.Description + " @ " + e.Path?.XPath).Distinct().ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors));
        Assert.Contains("رئيس لجنة التظلم الضريبي", doc.MainDocumentPart!.Document.Body!.InnerText);
    }

    [Fact]
    public async Task UploadJsonDossier_AssignsServerIdAndAnalyzes()
    {
        var demo = await (await _client.PostAsync("/api/demo-cases/dohaTech/load", null)).Content.ReadFromJsonAsync<DisputeDossier>(JsonDefaults.Options);
        var json = System.Text.Json.JsonSerializer.Serialize(demo! with { DossierId = "../../evil" }, JsonDefaults.Options);
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        part.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        form.Add(part, "files", "dossier.json");

        var res = await _client.PostAsync("/api/dossiers/upload", form);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<DisputeDossier>(JsonDefaults.Options);
        Assert.StartsWith("DOS-", created!.DossierId);

        var analysis = await _client.PostAsync($"/api/dossiers/{created.DossierId}/analysis", null);
        analysis.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task UploadPdf_WithoutAi_Returns503()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4 fake")), "files", "petition.pdf");
        var res = await _client.PostAsync("/api/dossiers/upload", form);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
    }

    [Fact]
    public async Task Upload_RejectsMismatchedSignature()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.ASCII.GetBytes("not a pdf")), "files", "petition.pdf");
        var res = await _client.PostAsync("/api/dossiers/upload", form);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task UnknownOrUnsafeId_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/dossiers/..%2F..%2Fetc")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync("/api/demo-cases/unknown/load", null)).StatusCode);
    }
}
