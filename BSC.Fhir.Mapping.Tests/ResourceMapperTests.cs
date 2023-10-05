using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Tests.Data;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Tests;

public class ResourceMapperTests
{
    private const string ITEM_EXTRACTION_CONTEXT_EXTENSION_URL =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext";
    private const string ITEM_INITIAL_EXPRESSION =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression";
    private const string QUESTIONNAIRE_HIDDEN_URL = "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden";
    private const string ITEM_POPULATION_CONTEXT =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext";
    private const string QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html";
    private const string VARIABLE_EXTENSION_URL = "http://hl7.org/fhir/StructureDefinition/variable";

    [Fact]
    public async Task Extract_GivesCorrectBundle()
    {
        // Console.WriteLine();
        // Console.WriteLine("=================");
        // Console.WriteLine("Extract");
        // Console.WriteLine("=================");
        // Console.WriteLine();

        var demoQuestionnaire = Demographics.CreateQuestionnaire();
        var demoQuestionnaireResponse = Demographics.CreateQuestionnaireResponse();

        var bundle = await ResourceMapper.Extract(
            demoQuestionnaire,
            demoQuestionnaireResponse,
            new(demoQuestionnaire, demoQuestionnaireResponse)
        );

        // Console.WriteLine(bundle.ToJson(new FhirJsonSerializationSettings { Pretty = true }));

        Assert.True(true);
    }

    [Fact]
    public void Populate_GivesCorrectQuestionnaireResponseForDemo()
    {
        var familyName = "Smith";
        var demoQuestionnaire = Demographics.CreateQuestionnaire();
        var patient = new Patient
        {
            Id = Guid.NewGuid().ToString(),
            BirthDate = "2006-04-05",
            Name =
            {
                new() { Family = familyName, Given = new[] { "Jane", "Rebecca" } }
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
                new() { Family = familyName, Given = new[] { "John", "Mark" } }
            }
        };

        var response = ResourceMapper.Populate(demoQuestionnaire, patient, relative);

        // Console.WriteLine();
        // Console.WriteLine("=================");
        // Console.WriteLine("Populate");
        // Console.WriteLine("=================");
        // Console.WriteLine();
        //
        // Console.WriteLine(response.ToJson(new() { Pretty = true }));

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

        var actualPatientFamilyNameAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "patient.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "patient.name.family")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(familyName, actualPatientFamilyNameAnswer);

        var actualPatientGivenNamesAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "patient.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "patient.name.given")
            ?.Answer.Select(answer => answer.Value.ToString());
        Assert.Equivalent(new[] { "Jane", "Rebecca" }, actualPatientGivenNamesAnswer);

        var actualRelativeIdAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "relative")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.id")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(actualRelativeIdAnswer, relative.Id);

        var actualRelativePatientAnswer = (
            response.Item
                .SingleOrDefault(item => item.LinkId == "relative")
                ?.Item.SingleOrDefault(item => item.LinkId == "relative.patient")
                ?.Answer.FirstOrDefault()
                ?.Value as ResourceReference
        )?.Reference;
        Assert.Equal(actualRelativePatientAnswer, $"Patient/{patient.Id}");

        var actualRelativeRelationshipAnswer =
            response.Item
                .SingleOrDefault(item => item.LinkId == "relative")
                ?.Item.SingleOrDefault(item => item.LinkId == "relative.relationship")
                ?.Answer.FirstOrDefault()
                ?.Value as Coding;
        Assert.Equivalent(relative.Relationship.First().Coding.First(), actualRelativeRelationshipAnswer);

        var actualRelativeFamilyNameAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "relative")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name.family")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(familyName, actualRelativeFamilyNameAnswer);

        var actualRelativeGivenNamesAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "relative")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name.given")
            ?.Answer.Select(answer => answer.Value.ToString());
        Assert.Equivalent(new[] { "John", "Mark" }, actualRelativeGivenNamesAnswer);
    }

    [Fact]
    public void Populate_GivesCorrectQuestionnaireResponseForGeneralNote()
    {
        var questionnaire = GeneralNote.CreateQuestionnaire();
        var composition = GeneralNote.CreateComposition();
        var documentReference = NoteDocumentReference.CreateDocumentReference();

        // var response = ResourceMapper.Populate(questionnaire, documentReference, composition);

        // Console.WriteLine(questionnaire.ToJson(new() { Pretty = true }));
        // Console.WriteLine(response.ToJson(new() { Pretty = true }));
    }

    [Fact]
    public async Task Extract_GivesCorrectBundleForGeneralNote()
    {
        var questionnaire = GeneralNote.CreateQuestionnaire();
        var response = GeneralNote.CreateQuestionnaireResponse();
        var profileLoaderMock = new Mock<IProfileLoader>();
        profileLoaderMock
            .Setup(x => x.LoadProfileAsync(It.IsAny<Canonical>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeneralNote.CreateProfile());

        var extractionResult = await ResourceMapper.Extract(
            questionnaire,
            response,
            new(questionnaire, response)
            {
                { "patient", new(new Patient { Id = Guid.NewGuid().ToString() }, typeof(Patient), "patient") },
                {
                    "practitioner",
                    new(new Practitioner { Id = Guid.NewGuid().ToString() }, typeof(Practitioner), "practitioner")
                }
            },
            profileLoaderMock.Object
        );

        Console.WriteLine(extractionResult.ToJson(new() { Pretty = true }));
    }
}
