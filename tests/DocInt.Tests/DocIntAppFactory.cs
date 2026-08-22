using DocInt.Api.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DocInt.Tests;

/// <summary>
/// Hosts the real Program in-memory. Subclasses override ConfigureFakes to replace
/// Azure adapters / engines; the base factory runs the app exactly as configured, which since
/// both Foundry endpoints became required means against endpoints that cannot resolve.
/// </summary>
public class DocIntAppFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// A test host has to supply both endpoints — they are required — and must never reach a real
    /// one. The .invalid TLD is reserved by RFC 2606 and resolves nowhere, so a value that escapes
    /// into a live call fails loudly on DNS instead of quietly reaching Azure.
    /// </summary>
    public const string DocumentIntelligenceEndpoint = "https://document-intelligence.invalid/";

    /// <inheritdoc cref="DocumentIntelligenceEndpoint"/>
    public const string OpenAIEndpoint = "https://openai.invalid/";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Hermetic by default. WebApplicationFactory boots in Development and reads
        // src/DocInt.Api/appsettings.Development.json, which is untracked and on a developer
        // machine normally holds the real Azure endpoints -- so without this, tests that mean to
        // exercise the offline path silently became live-Azure tests. Pinning to a name that
        // cannot resolve restores it. Only where a subclass has not asked for a value: subclasses
        // call base last, so GetSetting already sees their UseSetting.
        Default(builder, $"{FoundryOptions.SectionName}:DocumentIntelligenceEndpoint", DocumentIntelligenceEndpoint);
        Default(builder, $"{FoundryOptions.SectionName}:OpenAIEndpoint", OpenAIEndpoint);
        // Same reason, one layer up: the startup connectivity check must not dial anything from a
        // test unless that test is about the check. StartupConnectivityCheckTests turns it back on.
        Default(builder, $"{StartupProbeOptions.SectionName}:Enabled", "false");
        // And once more for the periodic monitor, which dials the same probes on a timer. Without
        // this it starts in every factory -- including ones that register a fake probe purely to
        // count the startup check's attempts -- so its first round lands as an extra ProbeAsync and
        // breaks those counts. A test gets the monitor only by asking for it.
        Default(builder, $"{DependencyCheckOptions.SectionName}:Enabled", "false");
        // The same untracked Development file also lowers the log level, and
        // StartupConfigurationLoggingTests reads that level back as its evidence that redaction
        // stays narrow -- an ordinary key keeps its value. Pin the tracked default for the same
        // reason as the endpoints: what is asserted must not depend on a developer's local file.
        Default(builder, "Serilog:MinimumLevel:Default", "Information");

        builder.ConfigureTestServices(ConfigureFakes);
    }

    /// <remarks>
    /// Absence is the only trigger, deliberately: an explicitly empty value is a subclass asking
    /// for a blank endpoint, which is now a boot failure worth testing. Treating "" as unset here
    /// would overwrite it with a working default and make that refusal untestable.
    /// </remarks>
    private static void Default(IWebHostBuilder builder, string key, string value)
    {
        if (builder.GetSetting(key) is null) builder.UseSetting(key, value);
    }

    protected virtual void ConfigureFakes(IServiceCollection services)
    {
    }
}
