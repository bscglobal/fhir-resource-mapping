using BSC.Fhir.Mapping.Tests.Data.Common;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data.ExtractorTestCases;

public record SimpleResourceCreate()
    : ExtractorTestCase(
        CreateQuestionnaire(),
        CreateQuestionnaireResponse(),
        new(),
        new(),
        CreateLaunchContext(),
        CreateExpectedBundle()
    )
{
    private static Questionnaire CreateQuestionnaire()
    {
        return QuestionnaireCreator.Create(
            new[] { new LaunchContext("patient", "Patient", "Patient") },
            new FhirQuery("Patient?_id={{%patient.id}}"),
            null,
            Array.Empty<FhirExpression>(),
            new[]
            {
                QuestionnaireItemCreator.Create(
                    "patientBirthDate",
                    Questionnaire.QuestionnaireItemType.Date,
                    "Patient.birthDate",
                    true
                ),
            }
        );
    }

    private static QuestionnaireResponse CreateQuestionnaireResponse()
    {
        return new()
        {
            Item =
            {
                new() { LinkId = "patientBirthDate", Answer = { new() { Value = new Date("2021-01-01") } } }
            }
        };
    }

    private static Dictionary<string, Resource> CreateLaunchContext()
    {
        return new();
    }

    private static Bundle CreateExpectedBundle()
    {
        return new() { Entry = { new() { Resource = new Patient { BirthDateElement = new Date("2021-01-01") } } } };
    }
}
