using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Logging;
using BSC.Fhir.Mapping.Tests.Data;
using BSC.Fhir.Mapping.Tests.Mocks;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
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
        FhirMappingLogging.LoggerFactory = new TestLoggerFactory(output);
    }

    [Fact]
    public async Task Populate_GivesCorrectQuestionnaireResponseForDemo()
    {
        var familyName = "Smith";
        var demoQuestionnaire = Demographics.CreateQuestionnaire();
        var patient = new Patient
        {
            Id = Guid.NewGuid().ToString(),
            BirthDate = "2006-04-05",
            Name =
            {
                new() { Family = familyName, Given = new[] { "Jane", "Rebecca" } },
                new() { Family = familyName, Given = new[] { "Elisabeth", "Charlotte" } }
            }
        };

        var relative = new RelatedPerson
        {
            Id = Guid.NewGuid().ToString(),
            Patient = new ResourceReference($"Patient/{patient.Id}"),
            Relationship =
            {
                new CodeableConcept
                {
                    Coding =
                    {
                        new Coding
                        {
                            System = "http://hl7.org/fhir/ValueSet/relatedperson-relationshiptype",
                            Code = "NOK",
                            Display = "next of kin"
                        }
                    }
                }
            },
            Name =
            {
                new() { Family = familyName, Given = new[] { "John", "Mark" } },
                new() { Family = familyName, Given = new[] { "Another", "Name" } }
            }
        };
        var relative2 = new RelatedPerson
        {
            Id = Guid.NewGuid().ToString(),
            Patient = new ResourceReference($"Patient/{patient.Id}"),
            Relationship =
            {
                new CodeableConcept
                {
                    Coding =
                    {
                        new Coding
                        {
                            System = "http://hl7.org/fhir/ValueSet/relatedperson-relationshiptype",
                            Code = "NOK",
                            Display = "next of kin"
                        }
                    }
                }
            },
            Name =
            {
                new() { Family = familyName, Given = new[] { "Elizabeth" } },
            }
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
                    { $"RelatedPerson?patient={patient.Id}", new[] { relative, relative2 } }
                }
            );

        var populator = new Populator(new NumericIdProvider(), resourceLoaderMock.Object);

        var response = await populator.Populate(
            demoQuestionnaire,
            new Dictionary<string, Resource> { { "patient", patient } }
        );

        var actualPatientIdAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "patient.id")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(patient.Id, actualPatientIdAnswer);

        var actualPatientBirthDateAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "patient.birthDate")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(patient.BirthDate, actualPatientBirthDateAnswer);

        // TODO: fix tests for names (repeating groups)

        // var actualPatientFamilyNameAnswer = response.Item
        //     .SingleOrDefault(item => item.LinkId == "patient.name")
        //     ?.Item.SingleOrDefault(item => item.LinkId == "patient.name.family")
        //     ?.Answer.FirstOrDefault()
        //     ?.Value.ToString();
        // Assert.Equal(familyName, actualPatientFamilyNameAnswer);
        //
        // var actualPatientGivenNamesAnswer = response.Item
        //     .SingleOrDefault(item => item.LinkId == "patient.name")
        //     ?.Item.SingleOrDefault(item => item.LinkId == "patient.name.given")
        //     ?.Answer.Select(answer => answer.Value.ToString());
        // Assert.Equivalent(new[] { "Jane", "Rebecca" }, actualPatientGivenNamesAnswer);

        var actualRelativeAnswers = response.Item.Where(item => item.LinkId == "relative").ToArray();
        // Console.WriteLine(
        //     JsonSerializer.Serialize(actualRelativeAnswers, new JsonSerializerOptions { WriteIndented = true })
        // );

        var actualRelative1IdAnswer = actualRelativeAnswers[0]?.Item
            .SingleOrDefault(item => item.LinkId == "relative.id")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();

        Assert.Equal(actualRelative1IdAnswer, relative.Id);

        var actualRelative2IdAnswer = actualRelativeAnswers[1]?.Item
            .SingleOrDefault(item => item.LinkId == "relative.id")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();

        Assert.Equal(actualRelative2IdAnswer, relative2.Id);

        var actualRelative1PatientAnswer = (
            actualRelativeAnswers[0].Item
                .SingleOrDefault(item => item.LinkId == "relative.patient")
                ?.Answer.FirstOrDefault()
                ?.Value as ResourceReference
        )?.Reference;
        Assert.Equal(actualRelative1PatientAnswer, $"Patient/{patient.Id}");

        var actualRelative2PatientAnswer = (
            actualRelativeAnswers[1]?.Item
                .SingleOrDefault(item => item.LinkId == "relative.patient")
                ?.Answer.FirstOrDefault()
                ?.Value as ResourceReference
        )?.Reference;
        Assert.Equal(actualRelative2PatientAnswer, $"Patient/{patient.Id}");

        var actualRelative1RelationshipAnswer =
            actualRelativeAnswers[0].Item
                .SingleOrDefault(item => item.LinkId == "relative.relationship")
                ?.Answer.FirstOrDefault()
                ?.Value as Coding;
        Assert.Equivalent(relative.Relationship.First().Coding.First(), actualRelative1RelationshipAnswer);

        var actualRelative2RelationshipAnswer =
            actualRelativeAnswers[1].Item
                .SingleOrDefault(item => item.LinkId == "relative.relationship")
                ?.Answer.FirstOrDefault()
                ?.Value as Coding;
        Assert.Equivalent(relative2.Relationship.First().Coding.First(), actualRelative2RelationshipAnswer);

        var actualRelative1FamilyNameAnswer = actualRelativeAnswers[0].Item
            .FirstOrDefault(item => item.LinkId == "relative.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name.family")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(familyName, actualRelative1FamilyNameAnswer);

        var actualRelative1GivenNamesAnswer = actualRelativeAnswers[0].Item
            .Where(item => item.LinkId == "relative.name")
            .Select(
                item =>
                    item.Item
                        .SingleOrDefault(item => item.LinkId == "relative.name.given")
                        ?.Answer.Select(answer => answer.Value.ToString())
            );
        Assert.Equivalent(
            new[] { new[] { "John", "Mark" }, new[] { "Another", "Name" } },
            actualRelative1GivenNamesAnswer
        );

        var actualRelative2FamilyNameAnswer = actualRelativeAnswers[1].Item
            .SingleOrDefault(item => item.LinkId == "relative.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name.family")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(familyName, actualRelative2FamilyNameAnswer);

        var actualRelative2GivenNamesAnswer = actualRelativeAnswers[1].Item
            .Where(item => item.LinkId == "relative.name")
            .Select(
                item =>
                    item.Item
                        .SingleOrDefault(item => item.LinkId == "relative.name.given")
                        ?.Answer.Select(answer => answer.Value.ToString())
            );
        Assert.Equivalent(new[] { new[] { "Elizabeth" } }, actualRelative2GivenNamesAnswer);
    }
}
