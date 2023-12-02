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
                    }
                }
            }
        };
    }
}
