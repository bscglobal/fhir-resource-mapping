using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class TestServiceRequest
{
    public static Bundle ExtractionBundle(string serviceRequestId, string patientId)
    {
        return new Bundle
        {
            Type = Bundle.BundleType.Collection,
            Entry =
            {
                new()
                {
                    Resource = new ServiceRequest
                    {
                        Note = { new() { Text = new("This is a very important note. Hi Mom!") } },
                        Extension =
                        {
                            new() { Url = "CareUnitExtension", Value = new FhirString("this is a care unit") },
                            new() { Url = "TeamExtension", Value = new FhirString("extension-team-Text") },
                        }
                    }
                }
            }
        };
    }
}
