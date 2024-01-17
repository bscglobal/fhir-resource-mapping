using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Data.Common;
using BSC.Fhir.Mapping.Tests.Mocks;
using FluentAssertions;
using Hl7.Fhir.Model;
using Moq;
using Xunit.Abstractions;
using FhirPathExpression = BSC.Fhir.Mapping.Tests.Data.Common.FhirPathExpression;
using FhirQueryExpression = BSC.Fhir.Mapping.Tests.Data.Common.FhirQueryExpression;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Tests.Expressions;

public class QuestionnaireParserTests
{
    private readonly ITestOutputHelper _output;

    public QuestionnaireParserTests(ITestOutputHelper output)
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
        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            questionnaireResponse,
            launchContext,
            resourceLoader.Object,
            ResolvingContext.Population,
            new FhirPathMapping(new TestLogger<FhirPathMapping>(_output)),
            new TestLogger<QuestionnaireParser>(_output)
        );

        await parser.ParseQuestionnaireAsync();
    }

    [Fact]
    public async Task ParseQuestionnaireAsync_CreatesCorrectScope_ForResourceTypeExtractionContext()
    {
        var questionnaire = new Questionnaire
        {
            Extension =
            {
                new()
                {
                    Url = Constants.EXTRACTION_CONTEXT,
                    Value = new Code { Value = "Patient" }
                }
            },
            Item =
            {
                new()
                {
                    LinkId = "1",
                    Definition = "Patient.birthDate",
                    Initial = { new() { Value = new Date { Value = "2006-04-05" } } }
                }
            }
        };

        var idProvider = new NumericIdProvider();
        var resourceLoaderMock = new Mock<IResourceLoader>();
        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            new QuestionnaireResponse(),
            new Dictionary<string, Resource>(),
            resourceLoaderMock.Object,
            ResolvingContext.Extraction,
            new FhirPathMapping(new TestLogger<FhirPathMapping>(_output)),
            new TestLogger<QuestionnaireParser>(_output)
        );

        var scope = await parser.ParseQuestionnaireAsync();

        scope.Should().NotBeNull();
        scope.Context.Should().HaveCount(1);

        var context = scope.Context.First();
        context.Should().BeOfType<QuestionnaireContext>();
        context.Value.Should().BeEquivalentTo(new[] { new Patient() });
    }

    [Fact]
    public async Task ParseQuestionnaireAsync_ResolvesCorrectContextForVariables_DuringExtraction()
    {
        var questionnaire = QuestionnaireCreator.Create(
            new[] { new LaunchContext("patient", "Patient", "Patient") },
            variables: new[] { new FhirPathExpression("%patient.id", "patientId") },
            extractionContext: new FhirQueryExpression("Patient?_id={{patientId}}")
        );

        var launchPatient = new Patient { Id = "123" };

        var idProvider = new NumericIdProvider();
        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(loader =>
                loader.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<string, IReadOnlyCollection<Resource>>
                {
                    { "Patient?_id=123", new[] { new Patient { Id = "123" } } }
                }
            );

        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            new QuestionnaireResponse(),
            new Dictionary<string, Resource> { { "patient", launchPatient } },
            resourceLoaderMock.Object,
            ResolvingContext.Extraction,
            new FhirPathMapping(new TestLogger<FhirPathMapping>(_output)),
            new TestLogger<QuestionnaireParser>(_output)
        );

        var scope = await parser.ParseQuestionnaireAsync();

        scope.Should().NotBeNull();
        scope.Context.Should().HaveCount(4);

        scope.Context.Should().ContainSingle(ctx => ctx.Name == "patientId");

        var patientIdContext = scope.Context.First(ctx => ctx.Name == "patientId");
        patientIdContext.Value.Should().NotBeNull();
        patientIdContext.Value.Should().BeEquivalentTo(new[] { new Id("123") });
    }

    [Fact]
    public async Task ParseQuestionnaireAsync_ResolvesCorrectContextForVariables_DuringPopulation()
    {
        var questionnaire = QuestionnaireCreator.Create(
            new[] { new LaunchContext("patient", "Patient", "Patient") },
            variables: new[] { new FhirPathExpression("%patient.id", "patientId") },
            populationContext: new FhirQueryExpression("Patient?_id={{patientId}}")
        );

        var launchPatient = new Patient { Id = "123" };

        var idProvider = new NumericIdProvider();
        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(loader =>
                loader.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<string, IReadOnlyCollection<Resource>>
                {
                    { "Patient?_id=123", new[] { new Patient { Id = "123" } } }
                }
            );

        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            new QuestionnaireResponse(),
            new Dictionary<string, Resource> { { "patient", launchPatient } },
            resourceLoaderMock.Object,
            ResolvingContext.Population,
            new FhirPathMapping(new TestLogger<FhirPathMapping>(_output)),
            new TestLogger<QuestionnaireParser>(_output)
        );

        var scope = await parser.ParseQuestionnaireAsync();

        scope.Should().NotBeNull();
        scope.Context.Should().HaveCount(4);

        scope.Context.Should().ContainSingle(ctx => ctx.Name == "patientId");

        var patientIdContext = scope.Context.First(ctx => ctx.Name == "patientId");
        patientIdContext.Value.Should().NotBeNull();
        patientIdContext.Value.Should().BeEquivalentTo(new[] { new Id("123") });
    }

    private Mock<IResourceLoader> ResourceLoaderMock(Dictionary<string, IReadOnlyCollection<Resource>> results)
    {
        var mock = new Mock<IResourceLoader>();

        mock.Setup(loader =>
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
