using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class TestServiceRequest
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
                    LinkId = "serviceRequest.note",
                    Item =
                    {
                        new()
                        {
                            LinkId = "serviceRequest.note.text",
                            Answer = { new() { Value = new FhirString("This is a very important note. Hi Mom!") } }
                        }
                    }
                },
                new()
                {
                    LinkId = "extension",
                    Item =
                    {
                        new()
                        {
                            LinkId = "extension.value",
                            Answer =
                            {
                                new QuestionnaireResponse.AnswerComponent
                                {
                                    Value = new FhirString("this is a care unit")
                                }
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
