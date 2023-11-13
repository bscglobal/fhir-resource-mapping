using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class TestServiceRequest
{
    public static Dictionary<string, IReadOnlyCollection<Resource>> ResourceLoaderResponse(
        string serviceRequestId,
        string patientId
    )
    {
        var serviceRequest = new ServiceRequest
        {
            Id = serviceRequestId,
            Subject = new ResourceReference($"Patient/{patientId}"),
            Occurrence = new Period() { Start = "2023-03-29T11:04:55Z" }
        };

        return new() { { $"ServiceRequest?_id={serviceRequestId}", new[] { serviceRequest } } };
    }
}
