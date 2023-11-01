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
                /*new()
                {
                    LinkId = "patient.id",
                    Definition = "Patient.id",
                    Answer = { new QuestionnaireResponse.AnswerComponent { Value = new FhirString(patientId) } }
                },*/
                new()
                {
                    LinkId = "servicerequest.id",
                    Definition = "ServiceRequest.id",
                    Answer = { new QuestionnaireResponse.AnswerComponent { Value = new FhirString(servreqId) } }
                },
                new()
                {
                    LinkId = "servicerequest.occurrence",
                    Item =
                    {
                        new()
                        {
                            LinkId = "servicerequest.occurrence.start",
                            Answer =
                            {
                                new QuestionnaireResponse.AnswerComponent
                                {
                                    Value = new FhirDateTime("2021-01-01T00:00:00Z")
                                }
                            }
                        }
                    }
                }
            }
        };

        return response;
    }
}
