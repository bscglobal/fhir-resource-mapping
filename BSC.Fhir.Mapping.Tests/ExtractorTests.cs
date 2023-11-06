using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Logging;
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
        FhirMappingLogging.LoggerFactory = new TestLoggerFactory(output);
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

        var extractor = new Extractor(resourceLoaderMock.Object, profileLoaderMock.Object);
        var bundle = await extractor.Extract(
            demoQuestionnaire,
            demoQuestionnaireResponse,
            new Dictionary<string, Resource> { { "patient", patient } }
        );

        _output.WriteLine(bundle.ToJson(new() { Pretty = true }));

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
}
