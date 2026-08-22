using DocInt.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DocInt.Tests;

/// <summary>
/// The live suite hosts the app through <see cref="LiveAppFactory"/> rather than the base factory,
/// because the base one pins both Foundry endpoints to a name that cannot resolve. That pinning is
/// what keeps the offline suite hermetic, and it silently neutered every live test: the host booted
/// against the wrong endpoints no matter what the environment said. It went unnoticed because the
/// live suite is env-gated and the endpoints were unreachable anyway.
/// </summary>
public class LiveAppFactoryTests
{
    [Fact]
    public void Live_factory_carries_the_environment_endpoints_into_configuration()
    {
        const string di = "https://live-factory-test.cognitiveservices.azure.com/";
        const string oai = "https://live-factory-test.openai.azure.com/";
        var priorDi = Environment.GetEnvironmentVariable("Foundry__DocumentIntelligenceEndpoint");
        var priorOai = Environment.GetEnvironmentVariable("Foundry__OpenAIEndpoint");
        try
        {
            Environment.SetEnvironmentVariable("Foundry__DocumentIntelligenceEndpoint", di);
            Environment.SetEnvironmentVariable("Foundry__OpenAIEndpoint", oai);

            using var factory = new LiveAppFactory();
            var options = factory.Services.GetRequiredService<IOptions<FoundryOptions>>().Value;

            Assert.Equal(di, options.DocumentIntelligenceEndpoint);
            Assert.Equal(oai, options.OpenAIEndpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Foundry__DocumentIntelligenceEndpoint", priorDi);
            Environment.SetEnvironmentVariable("Foundry__OpenAIEndpoint", priorOai);
        }
    }

    // The other half of the same contract, and the one that keeps the offline suite honest: an
    // endpoint exported into the environment -- or sitting in an untracked appsettings.Development
    // .json -- must not reach a host the base factory built. It cannot be blanked any more, because
    // a blank endpoint no longer boots, so it is pinned to a name that resolves nowhere instead.
    [Fact]
    public void Base_factory_pins_the_endpoints_somewhere_unreachable()
    {
        const string di = "https://should-be-ignored.cognitiveservices.azure.com/";
        var prior = Environment.GetEnvironmentVariable("Foundry__DocumentIntelligenceEndpoint");
        try
        {
            Environment.SetEnvironmentVariable("Foundry__DocumentIntelligenceEndpoint", di);

            using var factory = new DocIntAppFactory();
            var options = factory.Services.GetRequiredService<IOptions<FoundryOptions>>().Value;

            Assert.Equal(DocIntAppFactory.DocumentIntelligenceEndpoint,
                options.DocumentIntelligenceEndpoint);
            Assert.EndsWith(".invalid/", options.DocumentIntelligenceEndpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Foundry__DocumentIntelligenceEndpoint", prior);
        }
    }
}
