using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Mocks;
using FluentAssertions;
using Hl7.Fhir.Model;
using Moq;
using Xunit.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Tests;

public class PopulatorTests
{
    private readonly ITestOutputHelper _output;

    public PopulatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static object[] DemographicsTestCase()
    {
        var patientId = Guid.NewGuid().ToString();
        var relativeIds = (Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
        var resources = Demographics.ResourceLoaderResponse(patientId, relativeIds);
        return new object[]
        {
            Demographics.CreateQuestionnaire(),
            resources,
            new Dictionary<string, Resource>
            {
                { "patient", resources.Values.Select(value => value.First()).First(value => value is Patient) }
            },
            Demographics.PopulationResponse(patientId, relativeIds)
        };
    }

    private static object[] ServiceRequestTestCase()
    {
        var serviceRequestId = Guid.NewGuid().ToString();
        var patientId = Guid.NewGuid().ToString();
        var resources = TestServiceRequest.ResourceLoaderResponse(serviceRequestId, patientId);
        return new object[]
        {
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
        };
    }

    private static object[] GeneralNoteTestCase()
    {
        var compositionId = Guid.NewGuid().ToString();
        var patientId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var noteId = Guid.NewGuid().ToString();
        var imageIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
        var resources = GeneralNote.ResourceLoaderResponse(compositionId, patientId, userId, imageIds, noteId);
        return new object[]
        {
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
        };
    }

    public static IEnumerable<object[]> AllTestCases()
    {
        return new object[][] { DemographicsTestCase(), ServiceRequestTestCase(), GeneralNoteTestCase() };
    }

    [Theory]
    [MemberData(nameof(AllTestCases))]
    public async Task PopulateAsync_ReturnsCorrectQuestionnaireResponse(
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
                        questionnaireResponse,
                        launchContext,
                        resourceLoaderMock.Object,
                        resolvingContext,
                        new FhirPathMapping(new TestLogger<FhirPathMapping>(_output)),
                        new TestLogger<QuestionnaireParser>(_output)
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
