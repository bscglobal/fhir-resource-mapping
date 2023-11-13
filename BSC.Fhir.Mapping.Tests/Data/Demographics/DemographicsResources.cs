using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class Demographics
{
    public static Dictionary<string, IReadOnlyCollection<Resource>> ResourceLoaderResponse(
        string patientId,
        (string, string, string) relativeIds
    )
    {
        var patient = new Patient
        {
            Id = patientId,
            BirthDate = "2006-04-05",
            Name =
            {
                new() { Family = "Smith", Given = new[] { "Jane", "Rebecca" } },
                new() { Family = "Stanton", Given = new[] { "Elisabeth", "Charlotte" } }
            }
        };
        var relatives = new[]
        {
            new RelatedPerson
            {
                Id = relativeIds.Item1,
                Patient = new ResourceReference($"Patient/{patientId}"),
                BirthDate = "1964-06-01",
                Name =
                {
                    new() { Family = "Paul", Given = new[] { "Annabel" } }
                }
            },
            new RelatedPerson
            {
                Id = relativeIds.Item2,
                Patient = new ResourceReference($"Patient/{patientId}"),
                BirthDate = "1972-02-28",
                Name =
                {
                    new() { Family = "Rutherford", Given = new[] { "Annette" } }
                }
            },
            new RelatedPerson
            {
                Id = relativeIds.Item3,
                Patient = new ResourceReference($"Patient/{patientId}"),
                BirthDate = "1992-10-11",
                Name =
                {
                    new() { Family = "Wesley", Given = new[] { "Schuster" } }
                }
            },
        };
        return new Dictionary<string, IReadOnlyCollection<Resource>>
        {
            { $"Patient?_id={patient.Id}", new[] { patient } },
            { $"RelatedPerson?patient={patient.Id}", relatives.ToArray() }
        };
    }
}
