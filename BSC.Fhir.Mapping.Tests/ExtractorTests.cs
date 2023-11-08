using System.Text;
using System.Text.Json;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Mocks;
using FluentAssertions;
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

    [Fact]
    public async Task Extract_GivesCorrectBundleForDemo()
    {
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
                Id = relativeIds.Item3,
                Patient = new ResourceReference($"Patient/{patientId}"),
                BirthDate = "1992-10-11",
                Name =
                {
                    new() { Family = "Wesley", Given = new[] { "Schuster" } }
                }
            },
        };

        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(
                loader =>
                    loader.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<string, IReadOnlyCollection<Resource>>
                {
                    { $"Patient?_id={patient.Id}", new[] { patient } },
                    { $"RelatedPerson?patient={patient.Id}", relatives.ToArray() }
                }
            );

        var profileLoaderMock = new Mock<IProfileLoader>();

        var expectedPatientNames = new[]
        {
            new HumanName { Family = "Smith", Given = new[] { "John", "Mark" } },
            new HumanName { Family = "Smith", Given = new[] { "Matthew", "William" } }
        };

        var expectedRelativeNames = new[]
        {
            new[]
            {
                new HumanName { Family = "Smith", Given = new[] { "Jane", "Rebecca" } }
            },
            new[]
            {
                new HumanName { Family = "Terry", Given = new[] { "Heidi", "Stacey" } }
            },
            new[]
            {
                new HumanName { Family = "Wesley", Given = new[] { "Schuster" } }
            },
            new[]
            {
                new HumanName { Family = "Green", Given = new[] { "Hugh" } }
            },
        };

        var extractor = new Extractor(
            resourceLoaderMock.Object,
            profileLoaderMock.Object,
            logger: new TestLogger<Extractor>(_output)
        );
        var bundle = await extractor.ExtractAsync(
            demoQuestionnaire,
            demoQuestionnaireResponse,
            new Dictionary<string, Resource> { { "patient", patient } }
        );

        // _output.WriteLine(bundle.ToJson(new() { Pretty = true }));

        var createdPatient = bundle.Entry.Select(e => e.Resource).OfType<Patient>().FirstOrDefault();

        createdPatient.Should().NotBeNull();

        if (createdPatient is null)
        {
            return;
        }

        createdPatient.Id.Should().Be(patient.Id);
        createdPatient.BirthDate.Should().Be(patient.BirthDate);
        createdPatient.Name.Should().BeEquivalentTo(expectedPatientNames);
        createdPatient.Gender.Should().Be(AdministrativeGender.Male);

        var relatedPersons = bundle.Entry.Select(e => e.Resource).OfType<RelatedPerson>().ToArray();

        relatedPersons.Should().HaveCount(4);

        relatedPersons[0].Id.Should().BeEquivalentTo(relatives[0].Id);
        relatedPersons[0].Name.Should().BeEquivalentTo(expectedRelativeNames[0]);
        relatedPersons[0].Patient.Should().BeEquivalentTo(relatives[0].Patient);
        relatedPersons[0].BirthDate.Should().BeEquivalentTo(relatives[0].BirthDate);

        relatedPersons[1].Id.Should().BeEquivalentTo(relatives[1].Id);
        relatedPersons[1].Name.Should().BeEquivalentTo(expectedRelativeNames[1]);
        relatedPersons[1].Patient.Should().BeEquivalentTo(relatives[1].Patient);
        relatedPersons[1].BirthDate.Should().BeEquivalentTo(relatives[1].BirthDate);

        relatedPersons[2].Id.Should().BeEquivalentTo(relatives[2].Id);
        relatedPersons[2].Name.Should().BeEquivalentTo(expectedRelativeNames[2]);
        relatedPersons[2].Patient.Should().BeEquivalentTo(relatives[2].Patient);
        relatedPersons[2].BirthDate.Should().BeEquivalentTo(relatives[2].BirthDate);

        relatedPersons[3].Id.Should().BeNullOrEmpty();
        relatedPersons[3].Name.Should().BeEquivalentTo(expectedRelativeNames[3]);
        relatedPersons[3].Patient.Should().BeEquivalentTo(new ResourceReference($"Patient/{patientId}"));
        relatedPersons[3].BirthDate.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Extract_GivesCorrectBundleForSR()
    {
        var patientId = Guid.NewGuid().ToString();
        var srID = Guid.NewGuid().ToString();
        var srQuestionnaire = TestServiceRequest.CreateQuestionnaire();
        var srQuestionnaireResponse = TestServiceRequest.CreateQuestionnaireResponse(patientId, srID);

        var serviceRequest = new ServiceRequest()
        {
            Id = srID,
            Subject = new ResourceReference($"Patient/{patientId}"),
            Occurrence = new Period() { Start = "2021-01-01T00:00:00Z" }
        };

        var profileLoaderMock = new Mock<IProfileLoader>();
        profileLoaderMock
            .Setup(x => x.LoadProfileAsync(It.IsAny<Canonical>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestServiceRequest.CreateProfile());

        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(
                mock => mock.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<string, IReadOnlyCollection<Resource>>
                {
                    { $"ServiceRequest?_id={serviceRequest.Id}", new[] { serviceRequest } }
                }
            );

        var extractor = new Extractor(
            resourceLoaderMock.Object,
            profileLoaderMock.Object,
            logger: new TestLogger<Extractor>(_output)
        );
        var bundle = await extractor.ExtractAsync(
            srQuestionnaire,
            srQuestionnaireResponse,
            new Dictionary<string, Resource>
            {
                {
                    "user",
                    new Practitioner { Id = Guid.NewGuid().ToString() }
                },
                {
                    "serviceRequest",
                    new ServiceRequest { Id = serviceRequest.Id }
                }
            }
        );

        var extractedServiceRequest = bundle.Entry.FirstOrDefault()?.Resource as ServiceRequest;

        Assert.NotNull(extractedServiceRequest);
        extractedServiceRequest.Extension.Should().HaveCount(2);

        extractedServiceRequest.Extension
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new Extension { Url = "CareUnitExtension", Value = new FhirString("this is a care unit") },
                    new Extension { Url = "TeamExtension", Value = new FhirString("extension-team-Text") },
                }
            );
    }

    [Fact]
    public async Task Extract_GivesCorrectBundleForGeneralNote()
    {
        var composition = new Composition { Id = Guid.NewGuid().ToString() };
        var note = new DocumentReference { Id = Guid.NewGuid().ToString() };
        var imageIds = new string[4]
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();
        var patientId = Guid.NewGuid().ToString();
        var userId = Guid.NewGuid().ToString();
        var questionnaire = GeneralNote.CreateQuestionnaire();
        var response = GeneralNote.CreateQuestionnaireResponse(composition.Id, note.Id, imageIds);
        var profileLoaderMock = new Mock<IProfileLoader>();
        profileLoaderMock
            .Setup(x => x.LoadProfileAsync(It.IsAny<Canonical>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeneralNote.CreateProfile());

        var resourceLoaderMock = new Mock<IResourceLoader>();
        resourceLoaderMock
            .Setup(
                mock => mock.GetResourcesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new Dictionary<string, IReadOnlyCollection<Resource>>
                {
                    { $"Composition?_id={composition.Id}", new[] { composition } }
                }
            );

        var extractor = new Extractor(
            resourceLoaderMock.Object,
            profileLoaderMock.Object,
            logger: new TestLogger<Extractor>(_output)
        );
        var bundle = await extractor.ExtractAsync(
            questionnaire,
            response,
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
                    new Composition { Id = composition.Id }
                }
            }
        );

        var extractedResources = bundle.Entry.Select(e => e.Resource);
        var now = DateTime.Now;
        var actualComposition = extractedResources.OfType<Composition>().FirstOrDefault();
        Assert.NotNull(actualComposition);

        actualComposition.Id.Should().Be(composition.Id);
        actualComposition.Date.Should().NotBeNull();
        DateTime.Parse(actualComposition.Date).Should().BeBefore(now).And.BeAfter(now.Subtract(new TimeSpan(0, 0, 1)));

        actualComposition.Event.Should().HaveCount(1);
        var actualEvent = actualComposition.Event.First();
        actualEvent.Period.Start.Should().Be("2023-10-29");
        actualEvent.Period.End.Should().Be("2023-10-29");

        actualComposition.Type.Coding.Should().HaveCount(1);
        actualComposition.Type.Coding
            .First()
            .Should()
            .BeEquivalentTo(new Coding { System = "ProcedureCode", Code = "DEF002" });

        var actualExtensions = actualComposition.AllExtensions();
        actualExtensions.Should().HaveCount(1);
        actualExtensions.First().Value.Should().BeEquivalentTo(new FhirString("extension test"));

        var documentReferences = extractedResources.OfType<DocumentReference>();

        documentReferences.Should().HaveCount(5);

        var actualNotes = documentReferences.Where(
            dr => dr.Category.Any(cat => cat.Coding.Any(coding => coding.Code == "note"))
        );
        actualNotes.Should().HaveCount(1);
        var actualNote = actualNotes.First();

        actualNote.Subject.Should().BeEquivalentTo(new ResourceReference($"Patient/{patientId}"));
        actualNote.Content
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new DocumentReference.ContentComponent
                    {
                        Attachment = new() { Data = Encoding.UTF8.GetBytes("Hello World") }
                    }
                }
            );
        actualNote.Author.Should().BeEquivalentTo(new[] { new ResourceReference($"Practitioner/{userId}") });
        actualNote.Category
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new CodeableConcept
                    {
                        Coding =
                        {
                            new() { System = "http://bscglobal.com/CodeSystem/free-text-type", Code = "note" }
                        }
                    }
                }
            );

        var actualImages = documentReferences.Where(
            dr => dr.Category.Any(cat => cat.Coding.Any(coding => coding.Code == "general-note-image"))
        );

        actualImages.Should().HaveCount(4);
        actualImages
            .Should()
            .BeEquivalentTo(
                imageIds.Select(
                    id =>
                        new DocumentReference
                        {
                            Id = id,
                            Author = { new ResourceReference($"Practitioner/{userId}") },
                            Category =
                            {
                                new()
                                {
                                    Coding =
                                    {
                                        new()
                                        {
                                            System = "http://bscglobal.com/CodeSystem/free-text-type",
                                            Code = "general-note-image"
                                        }
                                    }
                                }
                            },
                            Subject = new ResourceReference($"Patient/{patientId}")
                        }
                )
            );
    }
}
