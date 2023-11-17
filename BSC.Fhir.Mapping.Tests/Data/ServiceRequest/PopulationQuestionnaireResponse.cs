using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class TestServiceRequest
{
    public static QuestionnaireResponse PopulationResponse(string serviceRequestId, string patientId)
    {
        return new()
        {
            Item =
            {
                new() { LinkId = "servicerequest.id", Answer = { new() { Value = new FhirString(serviceRequestId) } } },
                new()
                {
                    LinkId = "servicerequest.occurrence",
                    Item =
                    {
                        new()
                        {
                            LinkId = "servicerequest.occurrence.start",
                            Answer = { new() { Value = new FhirDateTime("2023-03-29T11:04:55Z") } }
                        }
                    }
                },
                new() { LinkId = "extensionTeam", Item = { new() { LinkId = "extensionTeam.value" } } },
                new() { LinkId = "extension", Item = { new() { LinkId = "extension.value" } } },
            }
        };
    }
}
