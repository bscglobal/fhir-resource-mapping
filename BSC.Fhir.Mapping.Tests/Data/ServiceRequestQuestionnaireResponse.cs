using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class ServiceRequest
{
    public static QuestionnaireResponse CreateQuestionnaireResponse(string patientId, string servreqId)
    {
        var response = new QuestionnaireResponse
        {
            Status = QuestionnaireResponse.QuestionnaireResponseStatus.Completed,
            Item =
            {
                new()
                {
                    LinkId = "extension",
                    Item =
                    {
                        new()
                        {
                            LinkId = "extension.url",
                            Answer =
                            {
                                new QuestionnaireResponse.AnswerComponent
                                {
                                    Value = new FhirString("CareUnitExtension")
                                }
                            }
                        },
                        new()
                        {
                            LinkId = "extension.value",
                            Answer =
                            {
                                new QuestionnaireResponse.AnswerComponent { Value = new FhirString("extensionText") }
                            }
                        }
                    }
                },
                new()
                {
                    LinkId = "extensionTeam",
                    Item =
                    {
                        new()
                        {
                            LinkId = "extensionTeam.url",
                            Answer =
                            {
                                new QuestionnaireResponse.AnswerComponent { Value = new FhirString("TeamExtension") }
                            }
                        },
                        new()
                        {
                            LinkId = "extensionTeam.value",
                            Answer =
                            {
                                new QuestionnaireResponse.AnswerComponent
                                {
                                    Value = new FhirString("extension-team-Text")
                                }
                            }
                        }
                    }
                },
            }
        };

        return response;
    }
}
