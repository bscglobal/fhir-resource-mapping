using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class PartialUpdate
{
    public static Bundle ExtractionBundle(string patientId)
    {
        return new Bundle
        {
            Type = Bundle.BundleType.Collection,
            Entry =
            {
                new()
                {
                    Resource = new Patient
                    {
                        Id = patientId,
                        Name =
                        {
                            new() { Given = new[] { "John" }, Family = "Smith" }
                        },
                        Gender = AdministrativeGender.Male
                    }
                }
            }
        };
    }
}
