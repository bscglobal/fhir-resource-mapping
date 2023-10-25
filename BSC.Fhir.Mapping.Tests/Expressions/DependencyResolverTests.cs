using System.Text.Json;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Tests.Data;
using FluentAssertions;
using Hl7.Fhir.Model;
using Moq;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Tests.Expressions;

public class DependencyResolverTests
{
    private readonly ITestOutputHelper _output;

    public DependencyResolverTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ParseQuestionnaire_ReturnsCorrectTree()
    {
        var idProvider = new NumericIdProvider();
        var questionnaire = Demographics.CreateQuestionnaire();
        var patient = new Patient();
        var resourceLoader = ResourceLoaderMock(new() { { "Patient", new[] { patient } } });
        var launchContext = DemoLaunchContext(idProvider);

        var resolver = new DependencyResolver(questionnaire, null, launchContext, resourceLoader.Object);
        await resolver.ParseQuestionnaireAsync();
    }

    private IReadOnlyCollection<LaunchContext> DemoLaunchContext(INumericIdProvider idProvider)
    {
        return new[] { new LaunchContext(idProvider, "patient", new Patient { Id = Guid.NewGuid().ToString() }) };
    }

    private Mock<IResourceLoader> ResourceLoaderMock(Dictionary<string, IReadOnlyCollection<Resource>> results)
    {
        var mock = new Mock<IResourceLoader>();

        mock.Setup(
                loader =>
                    loader.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .Returns<IReadOnlyCollection<string>, CancellationToken>(
                (urls, _) =>
                    Task.FromResult(
                        (IDictionary<string, IReadOnlyCollection<Resource>>)
                            results
                                .Where(resultKv => urls.Contains(resultKv.Key))
                                .ToDictionary(kv => kv.Key, kv => kv.Value)
                    )
            );

        return mock;
    }
}
