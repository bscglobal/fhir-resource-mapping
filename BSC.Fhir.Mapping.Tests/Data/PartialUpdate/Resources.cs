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
            Gender = AdministrativeGender.Male,
            Contact =
            {
                new()
                {
                    Relationship =
                    {
                        new()
                        {
                            Text = "parent",
                            Coding =
                            {
                                new()
                                {
                                    System = "http://terminology.hl7.org/3.1.0/CodeSystem-v3-RoleCode",
                                    Code = "parent"
                                }
                            }
                        }
                    }
                }
            }
        };

        return new() { { $"Patient?_id={patientId}", new[] { patient } } };
    }
}
