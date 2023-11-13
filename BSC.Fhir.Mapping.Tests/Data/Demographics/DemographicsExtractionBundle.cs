using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class Demographics
{
    public static Bundle ExtractionBundle(string patientId, (string, string, string) relativeIds)
    {
        return new Bundle
        {
            Type = Bundle.BundleType.Collection,
            Entry =
            {
                new()
                {
                    Resource = new RelatedPerson
                    {
                        Id = relativeIds.Item3,
                        Name =
                        {
                            new HumanName { Family = "Wesley", Given = new[] { "Schuster" } },
                        },
                        Patient = new ResourceReference($"Patient/{patientId}"),
                        BirthDate = "1992-10-11"
                    }
                },
                new()
                {
                    Resource = new RelatedPerson
                    {
                        Id = relativeIds.Item2,
                        Name =
                        {
                            new HumanName { Family = "Terry", Given = new[] { "Heidi", "Stacey" } },
                        },
                        Patient = new ResourceReference($"Patient/{patientId}"),
                        BirthDate = "1972-02-28"
                    }
                },
                new()
                {
                    Resource = new RelatedPerson
                    {
                        Id = relativeIds.Item1,
                        Name =
                        {
                            new HumanName { Family = "Smith", Given = new[] { "Jane", "Rebecca" } },
                        },
                        Patient = new ResourceReference($"Patient/{patientId}"),
                        BirthDate = "1964-06-01"
                    }
                },
                new()
                {
                    Resource = new RelatedPerson
                    {
                        Name =
                        {
                            new HumanName { Family = "Green", Given = new[] { "Hugh" } },
                        },
                        Patient = new ResourceReference($"Patient/{patientId}")
                    }
                },
                new()
                {
                    Resource = new Patient
                    {
                        Id = patientId,
                        Name =
                        {
                            new HumanName { Family = "Smith", Given = new[] { "John", "Mark" } },
                            new HumanName { Family = "Smith", Given = new[] { "Matthew", "William" } }
                        },
                        BirthDate = "2006-04-05",
                        Gender = AdministrativeGender.Male,
                        Active = true
                    }
                },
            }
        };
    }
}
