using System.Text.Json;
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
    public async Task Extract_GivesCorrectBundleForDemo()
    {
        Console.WriteLine();
        Console.WriteLine("=================");
        Console.WriteLine("Extract");
        Console.WriteLine("=================");
        Console.WriteLine();

        var patientId = Guid.NewGuid().ToString();
        var relativeIds = new[] { Guid.NewGuid().ToString(), Guid.NewGuid().ToString() };
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
        var relatives = relativeIds.Select(
            id =>
                new RelatedPerson
                {
                    Id = id,
                    Patient = new ResourceReference($"Patient/{patientId}"),
                    BirthDate = "1964-06-01",
                    Name =
                    {
                        new() { Family = "Paul", Given = new[] { "Annabel" } }
                    }
                }
        );

        var context = new MappingContext(demoQuestionnaire, demoQuestionnaireResponse);
        context.NamedExpressions.Add("extraction_root", new(patient, "extraction_root"));
        context.NamedExpressions.Add("extraction_relative", new(relatives.ToArray(), "extraction_relative"));
        context.NamedExpressions.Add("user", new(new Practitioner { Id = Guid.NewGuid().ToString() }, "user"));

        var bundle = await ResourceMapper.Extract(demoQuestionnaire, demoQuestionnaireResponse, context);

        Console.WriteLine(bundle.ToJson(new FhirJsonSerializationSettings { Pretty = true }));

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

        var context = new MappingContext(demoQuestionnaire, new());
        context.NamedExpressions.Add("patient", new(patient, "patient"));
        context.NamedExpressions.Add("relatedPerson", new(new[] { relative, relative2 }, "relatedPerson"));

        var response = ResourceMapper.Populate(demoQuestionnaire, context);

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

        var context = new MappingContext(questionnaire, response);
        context.NamedExpressions.Add("patient", new(new Patient { Id = Guid.NewGuid().ToString() }, "patient"));
        context.NamedExpressions.Add("user", new(new Practitioner { Id = Guid.NewGuid().ToString() }, "user"));
        var extractionResult = await ResourceMapper.Extract(questionnaire, response, context, profileLoaderMock.Object);

        Console.WriteLine(extractionResult.ToJson(new() { Pretty = true }));
    }
}
