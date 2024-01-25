using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Data.Common;
using BSC.Fhir.Mapping.Tests.Mocks;
using FluentAssertions;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using FhirPathExpression = BSC.Fhir.Mapping.Tests.Data.Common.FhirPathExpression;
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

        var evaluator = new FhirPathMapping(new TestLogger<FhirPathMapping>(_output));
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new TestLogger(_output));

        var graphGenerator = new DependencyGraphGenerator(idProvider, evaluator, loggerFactoryMock.Object);

        var questionnaireResponse = new QuestionnaireResponse();
        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            questionnaireResponse,
            launchContext,
            resourceLoader.Object,
            ResolvingContext.Population,
            evaluator,
            new TestLogger<QuestionnaireParser>(_output),
            graphGenerator
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

        var evaluator = new FhirPathMapping(new TestLogger<FhirPathMapping>(_output));
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new TestLogger(_output));

        var graphGenerator = new DependencyGraphGenerator(idProvider, evaluator, loggerFactoryMock.Object);

        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            new QuestionnaireResponse(),
            new Dictionary<string, Resource>(),
            resourceLoaderMock.Object,
            ResolvingContext.Extraction,
            evaluator,
            new TestLogger<QuestionnaireParser>(_output),
            graphGenerator
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
            extractionContext: new FhirQuery("Patient?_id={{patientId}}")
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

        var evaluator = new FhirPathMapping(new TestLogger<FhirPathMapping>(_output));
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new TestLogger(_output));

        var graphGenerator = new DependencyGraphGenerator(idProvider, evaluator, loggerFactoryMock.Object);

        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            new QuestionnaireResponse(),
            new Dictionary<string, Resource> { { "patient", launchPatient } },
            resourceLoaderMock.Object,
            ResolvingContext.Extraction,
            evaluator,
            new TestLogger<QuestionnaireParser>(_output),
            graphGenerator
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
            variables: new[]
            {
                new FhirPathExpression("%patient.name", "patientName"),
                new FhirPathExpression("%patientName.family", "familyName")
            },
            populationContext: new FhirQuery("Patient?name={{%familyName}}")
        );

        var launchPatient = new Patient
        {
            Id = "123",
            Name =
            {
                new() { Given = new[] { "Emily" }, Family = "Dickinson" }
            }
        };

        var idProvider = new NumericIdProvider();
        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(loader =>
                loader.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<string, IReadOnlyCollection<Resource>>
                {
                    { "Patient?name=Dickinson", new[] { (Patient)launchPatient.DeepCopy() } }
                }
            );

        var evaluator = new FhirPathMapping(new TestLogger<FhirPathMapping>(_output));
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new TestLogger(_output));

        var graphGenerator = new DependencyGraphGenerator(idProvider, evaluator, loggerFactoryMock.Object);

        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            new QuestionnaireResponse(),
            new Dictionary<string, Resource> { { "patient", launchPatient } },
            resourceLoaderMock.Object,
            ResolvingContext.Population,
            evaluator,
            new TestLogger<QuestionnaireParser>(_output),
            graphGenerator
        );

        var scope = await parser.ParseQuestionnaireAsync();

        scope.Should().NotBeNull();
        scope.Context.Should().HaveCount(5);

        scope.Context.Should().ContainSingle(ctx => ctx.Name == "patientName");
        var patientNameContext = scope.Context.First(ctx => ctx.Name == "patientName");
        patientNameContext.Value.Should().NotBeNull();
        patientNameContext
            .Value.Should()
            .BeEquivalentTo(
                new[]
                {
                    new HumanName { Given = new[] { "Emily" }, Family = "Dickinson" }
                }
            );

        scope.Context.Should().ContainSingle(ctx => ctx.Name == "familyName");
        var familyNameContext = scope.Context.First(ctx => ctx.Name == "familyName");
        familyNameContext.Value.Should().NotBeNull();
        familyNameContext.Value.Should().BeEquivalentTo(new[] { new FhirString("Dickinson") });
    }

    [Fact]
    public async Task ParseQuestionnaireAsync_DoesCorrectFhirQuery_WhenUsingResourceReference()
    {
        var questionnaire = QuestionnaireCreator.Create(
            launchContext: new[] { new LaunchContext("composition", "Composition", "Composition") },
            variables: new FhirExpression[]
            {
                new FhirPathExpression(
                    "%composition.section[0].entry[0].reference.replaceMatches('.*\\/', '')",
                    "patientId"
                ),
                new FhirQuery("Patient?_id={{%patientId}}", "newPatient")
            }
        );

        var launchComposition = new Composition
        {
            Id = "123",
            Section = { new Composition.SectionComponent { Entry = { new ResourceReference("Patient/456") } } }
        };

        var idProvider = new NumericIdProvider();
        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(loader =>
                loader.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<string, IReadOnlyCollection<Resource>>
                {
                    { "Patient?_id=456", new[] { new Patient { Id = "456" } } }
                }
            );

        var evaluator = new FhirPathMapping(new TestLogger<FhirPathMapping>(_output));
        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new TestLogger(_output));

        var graphGenerator = new DependencyGraphGenerator(idProvider, evaluator, loggerFactoryMock.Object);

        var parser = new QuestionnaireParser(
            idProvider,
            questionnaire,
            new QuestionnaireResponse(),
            new Dictionary<string, Resource> { { "composition", launchComposition } },
            resourceLoaderMock.Object,
            ResolvingContext.Extraction,
            evaluator,
            new TestLogger<QuestionnaireParser>(_output),
            graphGenerator
        );

        var scope = await parser.ParseQuestionnaireAsync();

        scope.Should().NotBeNull();
        scope.Context.Should().HaveCount(4);

        scope.Context.Should().ContainSingle(ctx => ctx.Name == "patientId");
        var patientIdContext = scope.Context.First(ctx => ctx.Name == "patientId");
        patientIdContext.Value.Should().NotBeNull();
        patientIdContext.Value.Should().BeEquivalentTo(new[] { new FhirString("456") });

        scope.Context.Should().ContainSingle(ctx => ctx.Name == "newPatient");
        var newPatientContext = scope.Context.First(ctx => ctx.Name == "newPatient");
        newPatientContext.Should().BeOfType<FhirQueryExpression>();
        ((FhirQueryExpression)newPatientContext).Expression.Should().Be("Patient?_id=456");
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
