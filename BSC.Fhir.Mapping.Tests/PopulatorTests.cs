using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Data.Common;
using BSC.Fhir.Mapping.Tests.Mocks;
using FluentAssertions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using FhirPathExpression = BSC.Fhir.Mapping.Tests.Data.Common.FhirPathExpression;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Tests;

public class PopulatorTests
{
    private readonly ITestOutputHelper _output;

    public PopulatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task DemographicsTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var relativeIds = (Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        var resources = Demographics.ResourceLoaderResponse(patientId, relativeIds);
        await PopulateTest(
            Demographics.CreateQuestionnaire(),
            resources,
            new Dictionary<string, Resource>
            {
                { "patient", resources.Values.Select(value => value.First()).First(value => value is Patient) }
            },
            Demographics.PopulationResponse(patientId, relativeIds)
        );
    }

    [Fact]
    public async Task ServiceRequestTestCase()
    {
        var serviceRequestId = Guid.NewGuid().ToString();
        var patientId = Guid.NewGuid().ToString();
        var resources = TestServiceRequest.ResourceLoaderResponse(serviceRequestId, patientId);

        await PopulateTest(
            TestServiceRequest.CreateQuestionnaire(),
            resources,
            new Dictionary<string, Resource>
            {
                {
                    "serviceRequest",
                    resources.Values.Select(value => value.First()).First(value => value is ServiceRequest)
                }
            },
            TestServiceRequest.PopulationResponse(serviceRequestId, patientId)
        );
    }

    [Fact]
    public async Task GeneralNoteTestCase()
    {
        var compositionId = Guid.NewGuid().ToString();
        var patientId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var noteId = Guid.NewGuid().ToString();
        var imageIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        var resources = GeneralNote.ResourceLoaderResponse(compositionId, patientId, userId, imageIds, noteId);

        await PopulateTest(
            GeneralNote.CreateQuestionnaire(),
            resources,
            new Dictionary<string, Resource>
            {
                { "composition", resources.Values.Select(value => value.First()).First(value => value is Composition) },
                {
                    "patient",
                    new Patient { Id = patientId }
                },
                {
                    "user",
                    new Practitioner { Id = userId }
                }
            },
            GeneralNote.PopulationResponse(compositionId, patientId, noteId, imageIds)
        );
    }

    private async Task PopulateTest(
        Questionnaire questionnaire,
        Dictionary<string, IReadOnlyCollection<Resource>> resourceLoaderResponse,
        Dictionary<string, Resource> launchContext,
        QuestionnaireResponse expectedResponse
    )
    {
        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(loader =>
                loader.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(resourceLoaderResponse);

        var idProvider = new NumericIdProvider();
        var evaluator = new FhirPathMapping(new TestLogger<FhirPathMapping>(_output));

        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new TestLogger(_output));

        var graphGenerator = new DependencyGraphGenerator(idProvider, evaluator, loggerFactoryMock.Object);

        var scopeTreeCreatorMock = new Mock<IScopeTreeCreator>();
        scopeTreeCreatorMock
            .Setup(factory =>
                factory.CreateScopeTreeAsync(
                    It.IsAny<Questionnaire>(),
                    It.IsAny<QuestionnaireResponse>(),
                    It.IsAny<Dictionary<string, Resource>>(),
                    It.IsAny<ResolvingContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                Questionnaire,
                QuestionnaireResponse,
                Dictionary<string, Resource>,
                ResolvingContext,
                CancellationToken
            >(
                (questionnaire, questionnaireResponse, launchContext, resolvingContext, cancellationToken) =>
                    new QuestionnaireParser(
                        idProvider,
                        questionnaire,
                        questionnaireResponse,
                        launchContext,
                        resourceLoaderMock.Object,
                        resolvingContext,
                        evaluator,
                        new TestLogger<QuestionnaireParser>(_output),
                        graphGenerator
                    ).ParseQuestionnaireAsync(cancellationToken)
            );

        var populator = new Populator(
            new NumericIdProvider(),
            resourceLoaderMock.Object,
            new TestLogger<Populator>(_output),
            scopeTreeCreatorMock.Object
        );

        var actualResponse = await populator.PopulateAsync(questionnaire, launchContext);

        // _output.WriteLine(expectedResponse.ToJson(new() { Pretty = true }));
        // _output.WriteLine(actualResponse.ToJson(new() { Pretty = true }));
        CompareItems(actualResponse.Item, expectedResponse.Item);
    }

    [Fact]
    public async Task PopulateAsync_MapsCodeableConceptFieldToList()
    {
        var questionnaire = QuestionnaireCreator.Create(
            new[] { new LaunchContext("observation", "Observation", "Observation") },
            null,
            null,
            Array.Empty<FhirExpression>(),
            new[]
            {
                QuestionnaireItemCreator.Create(
                    "1",
                    Questionnaire.QuestionnaireItemType.Group,
                    "Observation.component",
                    populationContext: new FhirPathExpression("%observation.component", "component"),
                    items: new[]
                    {
                        QuestionnaireItemCreator.Create(
                            "1.1",
                            Questionnaire.QuestionnaireItemType.Group,
                            "Observation.component.value",
                            populationContext: new FhirPathExpression("%component.value", "value"),
                            items: new[]
                            {
                                QuestionnaireItemCreator.Create(
                                    "1.1.1",
                                    Questionnaire.QuestionnaireItemType.Choice,
                                    "Observation.component.value.coding",
                                    repeats: true,
                                    initialExpression: new FhirPathExpression("%value.coding"),
                                    answerValueSet: "http://example.org/ValueSet"
                                )
                            }
                        )
                    }
                )
            }
        );

        var expectedResponse = new QuestionnaireResponse
        {
            Item = new List<QuestionnaireResponse.ItemComponent>
            {
                new()
                {
                    LinkId = "1",
                    Item = new List<QuestionnaireResponse.ItemComponent>
                    {
                        new()
                        {
                            LinkId = "1.1",
                            Item = new List<QuestionnaireResponse.ItemComponent>
                            {
                                new()
                                {
                                    LinkId = "1.1.1",
                                    Answer = new List<QuestionnaireResponse.AnswerComponent>
                                    {
                                        new()
                                        {
                                            Value = new Coding
                                            {
                                                Code = "code1",
                                                System = "http://example.org/ValueSet"
                                            },
                                        },
                                        new()
                                        {
                                            Value = new Coding
                                            {
                                                Code = "code2",
                                                System = "http://example.org/ValueSet"
                                            },
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var observationId = Guid.NewGuid().ToString();
        var observation = new Observation
        {
            Id = observationId,
            Component = new List<Observation.ComponentComponent>
            {
                new()
                {
                    Value = new CodeableConcept
                    {
                        Coding = new List<Coding>
                        {
                            new() { Code = "code1", System = "http://example.org/ValueSet" },
                            new() { Code = "code2", System = "http://example.org/ValueSet" }
                        }
                    }
                }
            }
        };
        var resourceLoaderMock = new Mock<IResourceLoader>();

        var idProvider = new NumericIdProvider();
        var logger = new TestLogger<Populator>(_output);

        var evaluator = new FhirPathMapping(new TestLogger<FhirPathMapping>(_output));

        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(factory => factory.CreateLogger(It.IsAny<string>())).Returns(new TestLogger(_output));

        var graphGenerator = new DependencyGraphGenerator(idProvider, evaluator, loggerFactoryMock.Object);

        var scopeTreeCreatorMock = new Mock<IScopeTreeCreator>();
        scopeTreeCreatorMock
            .Setup(factory =>
                factory.CreateScopeTreeAsync(
                    It.IsAny<Questionnaire>(),
                    It.IsAny<QuestionnaireResponse>(),
                    It.IsAny<Dictionary<string, Resource>>(),
                    It.IsAny<ResolvingContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<
                Questionnaire,
                QuestionnaireResponse,
                Dictionary<string, Resource>,
                ResolvingContext,
                CancellationToken
            >(
                (questionnaire, questionnaireResponse, launchContext, resolvingContext, cancellationToken) =>
                    new QuestionnaireParser(
                        new NumericIdProvider(),
                        questionnaire,
                        new(),
                        launchContext,
                        resourceLoaderMock.Object,
                        resolvingContext,
                        evaluator,
                        new TestLogger<QuestionnaireParser>(_output),
                        graphGenerator
                    ).ParseQuestionnaireAsync(cancellationToken)
            );

        var populator = new Populator(
            new NumericIdProvider(),
            resourceLoaderMock.Object,
            new TestLogger<Populator>(_output),
            scopeTreeCreatorMock.Object
        );

        var actualResponse = await populator.PopulateAsync(
            questionnaire,
            new Dictionary<string, Resource> { { "observation", observation } }
        );

        // _output.WriteLine(actualResponse.ToJson(new() { Pretty = true }));
        CompareItems(actualResponse.Item, expectedResponse.Item);
    }

    private static void CompareItems(
        IEnumerable<QuestionnaireResponse.ItemComponent> items,
        IEnumerable<QuestionnaireResponse.ItemComponent> expectedItems
    )
    {
        foreach (var group in items.GroupBy(i => i.LinkId))
        {
            var expected = expectedItems.Where(i => i.LinkId == group.Key);

            group.Should().BeEquivalentTo(expected);
        }
    }
}
