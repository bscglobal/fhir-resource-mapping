using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class PartialUpdate
{
    public static Dictionary<string, IReadOnlyCollection<Resource>> ResourceLoaderResponse(string patientId)
    {
        var patient = new Patient
        {
            Id = patientId,
            Name =
            {
                new() { Given = new[] { "John" }, Family = "Doe" }
            },
            Gender = AdministrativeGender.Male
        };

        return new() { { $"Patient?_id={patientId}", new[] { patient } } };
    }
}
