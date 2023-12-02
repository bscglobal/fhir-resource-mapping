using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class PartialUpdate
{
    public static QuestionnaireResponse CreateQuestionnaireResponse()
    {
        return new()
        {
            Status = QuestionnaireResponse.QuestionnaireResponseStatus.Completed,
            Item =
            {
                new()
                {
                    LinkId = "name",
                    Item =
                    {
                        new() { LinkId = "givenName" },
                        new() { LinkId = "familyName", Answer = { new() { Value = new FhirString("Smith") } } }
                    },
                },
                new() { LinkId = "gender" },
                new() { LinkId = "contacts", Item = { new() { LinkId = "contactRelationship" } } }
            }
        };
    }

    public static QuestionnaireResponse CreateQuestionnaireResponseWithoutGiven()
    {
        return new()
        {
            Status = QuestionnaireResponse.QuestionnaireResponseStatus.Completed,
            Item =
            {
                new()
                {
                    LinkId = "name",
                    Item =
                    {
                        new() { LinkId = "familyName", Answer = { new() { Value = new FhirString("Smith") } } }
                    }
                },
            }
        };
    }
}
