using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Mocks;
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
        var patientId = Guid.NewGuid().ToString();
        var relativeIds = (Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        var demoQuestionnaire = Demographics.CreateQuestionnaire();
        var demoQuestionnaireResponse = Demographics.CreateQuestionnaireResponse(patientId, relativeIds);
        var familyName = "Smith";
        var patient = new Patient
        {
            Id = patientId,
            BirthDate = "2006-04-05",
            Name =
            {
                new() { Family = familyName, Given = new[] { "Jane", "Rebecca" } },
                new() { Family = familyName, Given = new[] { "Elisabeth", "Charlotte" } }
            }
        };
        var relatives = new[]
        {
            new RelatedPerson
            {
                Id = relativeIds.Item1,
                Patient = new ResourceReference($"Patient/{patientId}"),
                BirthDate = "1964-06-01",
                Name =
                {
                    new() { Family = "Paul", Given = new[] { "Annabel" } }
                }
            },
            new RelatedPerson
            {
                Id = relativeIds.Item2,
                Patient = new ResourceReference($"Patient/{patientId}"),
                BirthDate = "1972-02-28",
                Name =
                {
                    new() { Family = "Rutherford", Given = new[] { "Annette" } }
                }
            },
            new RelatedPerson
            {
                Id = relativeIds.Item2,
                Patient = new ResourceReference($"Patient/{patientId}"),
                BirthDate = "1992-10-11",
                Name =
                {
                    new() { Family = "Wesley", Given = new[] { "Schuster" } }
                }
            },
        };
        var resourceLoader = ResourceLoaderMock(
            new()
            {
                { $"Patient?_id={patientId}", new[] { patient } },
                { $"RelatedPerson?patient={patientId}", relatives.ToArray() }
            }
        );

        var launchContext = new Dictionary<string, Resource>
        {
            { "patient", patient },
            {
                "user",
                new Practitioner { Id = Guid.NewGuid().ToString() }
            },
        };

        var questionnaireResponse = new QuestionnaireResponse();
        var resolver = new DependencyResolver(
            idProvider,
            questionnaire,
            questionnaireResponse,
            launchContext,
            resourceLoader.Object,
            ResolvingContext.Population,
            new TestLogger(_output)
        );
        await resolver.ParseQuestionnaireAsync();
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
