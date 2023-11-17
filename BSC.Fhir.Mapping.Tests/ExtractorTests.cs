using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Mocks;
using FluentAssertions;
using FluentAssertions.Equivalency;
using FluentAssertions.Extensions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Moq;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Tests;

public class ExtractorTests
{
    private readonly ITestOutputHelper _output;

    public ExtractorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static object[] DemographicsTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var relativeIds = (Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        return new object[]
        {
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
        };
    }

    private static object[] ServiceRequestTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var serviceRequestId = Guid.NewGuid().ToString();
        return new object[]
        {
            TestServiceRequest.CreateQuestionnaire(),
            TestServiceRequest.CreateQuestionnaireResponse(patientId, serviceRequestId),
            TestServiceRequest.ResourceLoaderResponse(serviceRequestId, patientId),
            new Dictionary<string, StructureDefinition> { { "ServiceRequest", TestServiceRequest.CreateProfile() } },
            new Dictionary<string, Resource>
            {
                {
                    "user",
                    new Practitioner { Id = Guid.NewGuid().ToString() }
                },
                {
                    "serviceRequest",
                    new ServiceRequest { Id = serviceRequestId }
                }
            },
            TestServiceRequest.ExtractionBundle(serviceRequestId, patientId)
        };
    }

    private static object[] NewGeneralNoteTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var compositionId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var imageIds = new string[4]
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();
        var noteId = Guid.NewGuid().ToString();
        return new object[]
        {
            GeneralNote.CreateQuestionnaire(),
            GeneralNote.CreateQuestionnaireResponse(compositionId, noteId, imageIds),
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
            GeneralNote.ExtractionBundle(compositionId, patientId, userId, noteId, imageIds)
        };
    }

    private static object[] UpdatedGeneralNoteTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var compositionId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var originalImageIds = new string[4]
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();
        var newImageIds = new[] { originalImageIds[0], originalImageIds[2], Guid.NewGuid().ToString() };
        var noteId = Guid.NewGuid().ToString();
        return new object[]
        {
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
            GeneralNote.ExtractionBundle(compositionId, patientId, userId, noteId, originalImageIds)
        };
    }

    public static IEnumerable<object[]> AllQuestionnaireTestCases()
    {
        return new[]
        {
            DemographicsTestCase(),
            ServiceRequestTestCase(),
            NewGeneralNoteTestCase(),
            UpdatedGeneralNoteTestCase()
        };
    }

    [Theory]
    [MemberData(nameof(AllQuestionnaireTestCases))]
    public async Task ExtractAsync_ReturnsCorrectBundle(
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
                .Using<FhirDateTime>(
                    ctx =>
                        ctx.Subject
                            .ToDateTimeOffset(2.Hours())
                            .Should()
                            .BeCloseTo(ctx.Expectation.ToDateTimeOffset(2.Hours()), 1.Seconds())
                )
                .WhenTypeIs<FhirDateTime>();
        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(
                mock => mock.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(resourceLoaderResponse);

        var profileLoaderMock = new Mock<IProfileLoader>();
        profileLoaderMock
            .Setup(x => x.LoadProfileAsync(It.IsAny<Canonical>(), It.IsAny<CancellationToken>()))
            .Returns<Canonical, CancellationToken>(
                (url, _) => Task.FromResult(profiles.TryGetValue(url.Value, out var profile) ? profile : null)
            );

        var scopeTreeCreatorMock = new Mock<IScopeTreeCreator>();
        scopeTreeCreatorMock
            .Setup(
                factory =>
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
                        questionnaireResponse,
                        launchContext,
                        resourceLoaderMock.Object,
                        resolvingContext,
                        new FhirPathMapping(new TestLogger<FhirPathMapping>(_output)),
                        new TestLogger<QuestionnaireParser>(_output)
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

        foreach (var expected in expectedResources)
        {
            actualResources.Select(a => a.Id).Should().Contain(expected.Id);
            var actual = actualResources.FirstOrDefault(r => r.Id == expected.Id);
            actual.Should().NotBeNull();
            // _output.WriteLine(expected.ToJson(new() { Pretty = true }));
            // _output.WriteLine(actual.ToJson(new() { Pretty = true }));
            actual
                .Should()
                .BeEquivalentTo(
                    expected,
                    equivalancyOptions,
                    "because {0} should be the same as {1}",
                    actual.ToJson(new() { Pretty = true }),
                    expected.ToJson(new() { Pretty = true })
                );
        }
    }
}
