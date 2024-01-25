using System.Text;
using System.Text.Json;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Data.Common;
using BSC.Fhir.Mapping.Tests.Data.ExtractorTestCases;
using BSC.Fhir.Mapping.Tests.Mocks;
using FluentAssertions;
using FluentAssertions.Equivalency;
using FluentAssertions.Extensions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit.Abstractions;
using FhirPathExpression = BSC.Fhir.Mapping.Tests.Data.Common.FhirPathExpression;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Tests;

public class ExtractorTests
{
    private readonly ITestOutputHelper _output;

    public ExtractorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Extractor_SimpleResource()
    {
        var testCase = new SimpleResourceCreate();
        await TestExtractor(
            testCase.Questionnaire,
            testCase.QuestionnaireResponse,
            testCase.ResourceLoaderResponse,
            testCase.Profiles,
            testCase.LaunchContext,
            testCase.ExpectedBundle
        );
    }

    [Fact]
    public async Task Extractor_DemographicsTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var relativeIds = (Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString());

        await TestExtractor(
            Demographics.CreateQuestionnaire(),
            Demographics.CreateQuestionnaireResponse(patientId, relativeIds),
            Demographics.ResourceLoaderResponse(patientId, relativeIds),
            new Dictionary<string, StructureDefinition>(),
            new Dictionary<string, Resource>
            {
                {
                    "patient",
                    new Patient { Id = patientId }
                }
            },
            Demographics.ExtractionBundle(patientId, relativeIds)
        );
    }

    [Fact]
    public async Task ServiceRequestTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var serviceRequestId = Guid.NewGuid().ToString();

        await TestExtractor(
            TestServiceRequest.CreateQuestionnaire(),
            TestServiceRequest.CreateQuestionnaireResponse(patientId, serviceRequestId),
            TestServiceRequest.ResourceLoaderResponse(serviceRequestId, patientId),
            new Dictionary<string, StructureDefinition> { { "ServiceRequest", TestServiceRequest.CreateProfile() } },
            new Dictionary<string, Resource>
            {
                {
                    "user",
                    new Practitioner { Id = Guid.NewGuid().ToString() }
                }
            },
            TestServiceRequest.ExtractionBundle(serviceRequestId, patientId)
        );
    }

    [Fact]
    public async Task Extractor_NewGeneralNoteTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var compositionId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var imageIds = new string[4]
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();
        var noteId = Guid.NewGuid().ToString();

        await TestExtractor(
            GeneralNote.CreateQuestionnaire(noteId),
            GeneralNote.CreateQuestionnaireResponse(compositionId, null, imageIds),
            GeneralNote.EmptyResourceLoaderResponse(compositionId),
            new Dictionary<string, StructureDefinition> { { "Composition", GeneralNote.CreateProfile() } },
            new Dictionary<string, Resource>
            {
                {
                    "user",
                    new Practitioner { Id = userId }
                },
                {
                    "patient",
                    new Patient { Id = patientId }
                }
            },
            GeneralNote.ExtractionBundle(compositionId, patientId, userId, noteId, null, imageIds)
        );
    }

    [Fact]
    public async Task Extractor_UpdatedGeneralNoteTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var compositionId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var originalImageIds = new string[4]
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();
        var newImageIds = new[] { originalImageIds[0], originalImageIds[2], Guid.NewGuid().ToString() };
        var noteId = Guid.NewGuid().ToString();

        await TestExtractor(
            GeneralNote.CreateQuestionnaire(),
            GeneralNote.CreateQuestionnaireResponse(compositionId, noteId, originalImageIds),
            GeneralNote.ResourceLoaderResponse(compositionId, patientId, userId, newImageIds, noteId),
            new Dictionary<string, StructureDefinition> { { "Composition", GeneralNote.CreateProfile() } },
            new Dictionary<string, Resource>
            {
                {
                    "user",
                    new Practitioner { Id = userId }
                },
                {
                    "patient",
                    new Patient { Id = patientId }
                },
                {
                    "composition",
                    new Composition { Id = compositionId }
                }
            },
            GeneralNote.ExtractionBundle(
                compositionId,
                patientId,
                userId,
                noteId,
                "This is text that should not be overwritten",
                originalImageIds
            )
        );
    }

    [Fact]
    public async Task Extractor_GeneralNoteWithNoImagesTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var compositionId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var originalImageIds = Array.Empty<string>();
        var newImageIds = Array.Empty<string>();
        var noteId = Guid.NewGuid().ToString();

        await TestExtractor(
            GeneralNote.CreateQuestionnaire(noteId),
            GeneralNote.CreateQuestionnaireResponse(compositionId, null, originalImageIds),
            GeneralNote.ResourceLoaderResponse(compositionId, patientId, userId, newImageIds, noteId),
            new Dictionary<string, StructureDefinition> { { "Composition", GeneralNote.CreateProfile() } },
            new Dictionary<string, Resource>
            {
                {
                    "user",
                    new Practitioner { Id = userId }
                },
                {
                    "patient",
                    new Patient { Id = patientId }
                },
                {
                    "composition",
                    new Composition { Id = compositionId }
                }
            },
            GeneralNote.ExtractionBundle(
                compositionId,
                patientId,
                userId,
                noteId,
                "This is text that should not be overwritten",
                originalImageIds
            )
        );
    }

    [Fact]
    public async Task ExtractAsync_PartialUpdateTestCase_WithEmptyItemsInQuestionnaireResponse()
    {
        var patientId = Guid.NewGuid().ToString();

        await TestExtractor(
            PartialUpdate.CreateQuestionnaire(),
            PartialUpdate.CreateQuestionnaireResponse(),
            PartialUpdate.ResourceLoaderResponse(patientId),
            new Dictionary<string, StructureDefinition>(),
            new Dictionary<string, Resource>
            {
                {
                    "patient",
                    new Patient { Id = patientId }
                }
            },
            PartialUpdate.ExtractionBundle(patientId)
        );
    }

    [Fact]
    public async Task ExtractAsync_PartialUpdateTestCase_WithNoItemsInQuestionnaireResponse()
    {
        var patientId = Guid.NewGuid().ToString();

        await TestExtractor(
            PartialUpdate.CreateQuestionnaire(),
            PartialUpdate.CreateQuestionnaireResponseWithoutGiven(),
            PartialUpdate.ResourceLoaderResponse(patientId),
            new Dictionary<string, StructureDefinition>(),
            new Dictionary<string, Resource>
            {
                {
                    "patient",
                    new Patient { Id = patientId }
                }
            },
            PartialUpdate.ExtractionBundle(patientId)
        );
    }

    [Fact]
    public async Task ExtractAsync_EmptyAnswer()
    {
        var questionnaire = QuestionnaireCreator.Create(
            new[]
            {
                new LaunchContext("composition", "Composition", "Composition"),
                new LaunchContext("user", "Practitioner", "Practitioner")
            },
            new FhirQuery("Composition?_id={{%composition.id}}"),
            items: new[]
            {
                QuestionnaireItemCreator.Create(
                    "compositionAuthor",
                    Questionnaire.QuestionnaireItemType.Reference,
                    "Composition.Author",
                    required: true,
                    calculatedExpression: new FhirPathExpression("%user.id")
                ),
                QuestionnaireItemCreator.Create(
                    "compositionSection",
                    Questionnaire.QuestionnaireItemType.Group,
                    "Composition.section",
                    items: new[]
                    {
                        QuestionnaireItemCreator.Create(
                            "compositionSectionTitle",
                            Questionnaire.QuestionnaireItemType.Text,
                            "Composition.section.title",
                            initial: new[] { new FhirString("Section Title") }
                        ),
                        QuestionnaireItemCreator.Create(
                            "compositionSectionEntry",
                            Questionnaire.QuestionnaireItemType.Reference,
                            "Composition.section.entry",
                            required: true,
                            calculatedExpression: new FhirPathExpression(
                                "%resource.item.where(linkId='note').item.where(linkId='noteId')"
                            ),
                            extensions: new[]
                            {
                                new Extension { Url = "referenceType", Value = new FhirString("DocumentReference") }
                            }
                        )
                    }
                ),
                QuestionnaireItemCreator.Create(
                    "note",
                    Questionnaire.QuestionnaireItemType.Group,
                    extractionContext: new FhirQuery(
                        "DocumentReference?_has:Composition:entry:_id={{%composition.id}}&category=http://bscglobal.com/CodeSystem/free-text-type|note"
                    ),
                    items: new[]
                    {
                        QuestionnaireItemCreator.Create(
                            "noteId",
                            Questionnaire.QuestionnaireItemType.Text,
                            "DocumentReference.id",
                            calculatedExpression: new FhirPathExpression("'123456'")
                        ),
                        QuestionnaireItemCreator.Create(
                            "noteContent",
                            Questionnaire.QuestionnaireItemType.Group,
                            "DocumentReference.content",
                            items: new[]
                            {
                                QuestionnaireItemCreator.Create(
                                    "noteContentAttachment",
                                    Questionnaire.QuestionnaireItemType.Attachment,
                                    "DocumentReference.content.attachment",
                                    extensions: new[]
                                    {
                                        new Extension { Url = "attachment-type", Value = new FhirString("Text") }
                                    }
                                )
                            }
                        ),
                        QuestionnaireItemCreator.Create(
                            "noteAuthor",
                            Questionnaire.QuestionnaireItemType.Reference,
                            "DocumentReference.author",
                            calculatedExpression: new FhirPathExpression("%user.id")
                        ),
                        QuestionnaireItemCreator.Create(
                            "noteCategory",
                            Questionnaire.QuestionnaireItemType.Text,
                            "DocumentReference.category",
                            initial: new[] { new Coding("http://bscglobal.com/CodeSystem/free-text-type", "note") }
                        )
                    }
                )
            }
        );

        var questionanireResponse = new QuestionnaireResponse
        {
            Item =
            {
                new()
                {
                    LinkId = "note",
                    Item =
                    {
                        new() { LinkId = "noteId" },
                        new()
                        {
                            LinkId = "noteContent",
                            Item =
                            {
                                new()
                                {
                                    LinkId = "noteContentAttachment",
                                    Answer =
                                    {
                                        new()
                                        {
                                            Value = new Attachment { Data = Encoding.UTF8.GetBytes("Well Hellow") }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var loaderResponse = new Dictionary<string, IReadOnlyCollection<Resource>>();
        var launchContext = new Dictionary<string, Resource>
        {
            {
                "user",
                new Practitioner { Id = "123" }
            }
        };

        var expectedExtractionBundle = new Bundle
        {
            Entry =
            {
                new()
                {
                    Resource = new Composition
                    {
                        Author = { new() { Reference = "Practitioner/123" } },
                        Section =
                        {
                            new() { Title = "Section Title", Entry = { new("DocumentReference/123456") } }
                        }
                    }
                },
                new()
                {
                    Resource = new DocumentReference
                    {
                        Id = "123456",
                        Content = { new() { Attachment = new() { Data = Encoding.UTF8.GetBytes("Well Hellow") } } },
                        Author = { new() { Reference = "Practitioner/123" } },
                        Category = { new CodeableConcept("http://bscglobal.com/CodeSystem/free-text-type", "note") }
                    }
                }
            }
        };

        await TestExtractor(
            questionnaire,
            questionanireResponse,
            loaderResponse,
            new Dictionary<string, StructureDefinition>(),
            launchContext,
            expectedExtractionBundle
        );
    }

    private async Task TestExtractor(
        Questionnaire questionnaire,
        QuestionnaireResponse response,
        Dictionary<string, IReadOnlyCollection<Resource>> resourceLoaderResponse,
        Dictionary<string, StructureDefinition> profiles,
        Dictionary<string, Resource> launchContext,
        Bundle expectedBundle
    )
    {
        EquivalencyAssertionOptions<T> equivalancyOptions<T>(EquivalencyAssertionOptions<T> options) =>
            options
                .Using<FhirDateTime>(ctx =>
                    ctx.Subject.ToDateTimeOffset(2.Hours())
                        .Should()
                        .BeCloseTo(ctx.Expectation.ToDateTimeOffset(2.Hours()), 1.Seconds())
                )
                .WhenTypeIs<FhirDateTime>();
        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(mock =>
                mock.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(resourceLoaderResponse);

        var profileLoaderMock = new Mock<IProfileLoader>();
        profileLoaderMock
            .Setup(x => x.LoadProfileAsync(It.IsAny<Canonical>(), It.IsAny<CancellationToken>()))
            .Returns<Canonical, CancellationToken>(
                (url, _) => Task.FromResult(profiles.TryGetValue(url.Value, out var profile) ? profile : null)
            );

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

        var expectedResources = expectedBundle.Entry.Select(e => e.Resource);
        var expectedIds = expectedResources.Select(r => r.Id);

        var extractor = new Extractor(
            resourceLoaderMock.Object,
            profileLoaderMock.Object,
            new TestLogger<Extractor>(_output),
            scopeTreeCreatorMock.Object
        );
        var actualBundle = await extractor.ExtractAsync(questionnaire, response, launchContext);

        var actualResources = actualBundle.Entry.Select(e => e.Resource);

        actualResources.Should().HaveSameCount(expectedResources);
        // _output.WriteLine(
        //     JsonSerializer.Serialize(actualResources, new JsonSerializerOptions { WriteIndented = true })
        // );

        foreach (var expected in expectedResources)
        {
            actualResources.Select(a => a.Id).Should().Contain(expected.Id);
            var actual = actualResources.FirstOrDefault(r => r.Id == expected.Id);
            actual.Should().NotBeNull();
            // _output.WriteLine(expected.ToJson(new() { Pretty = true }));
            // _output.WriteLine(actual.ToJson(new() { Pretty = true }));
            actual.Should().BeEquivalentTo(expected, equivalancyOptions);
        }
    }
}
