using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Identity;
using DocInt.Api.Configuration;
using Microsoft.Extensions.Options;

namespace DocInt.Api.Engines;

public sealed class AzureLayoutAnalysisClient : ILayoutAnalysisClient
{
    private readonly DocumentIntelligenceClient _client;

    /// <remarks>
    /// No unconfigured branch: the endpoint is required, so ValidateOnStart refuses the boot before
    /// this is ever resolved. The nullable client and the IsConfigured seam that went with it were
    /// how the service used to degrade one surface at a time — a state no configuration can reach.
    /// </remarks>
    public AzureLayoutAnalysisClient(IOptions<FoundryOptions> options)
    {
        var o = options.Value;
        var endpoint = new Uri(o.DocumentIntelligenceEndpoint!);
        _client = FoundryCredential.UsesApiKey(o)
            ? new DocumentIntelligenceClient(endpoint, new AzureKeyCredential(o.ApiKey!))
            : new DocumentIntelligenceClient(endpoint, new DefaultAzureCredential());
    }

    public async Task<LayoutAnalysis> AnalyzeAsync(BinaryData content, CancellationToken ct)
    {
        var options = new AnalyzeDocumentOptions("prebuilt-layout", content)
        {
            OutputContentFormat = DocumentContentFormat.Markdown
        };
        var operation = await _client.AnalyzeDocumentAsync(WaitUntil.Completed, options, ct);
        var result = operation.Value;
        return new LayoutAnalysis(
            result.Content ?? "",
            result.Pages?.Count ?? 0,
            result.Warnings?.Select(w => $"{w.Code}: {w.Message}").ToArray() ?? []);
    }
}
