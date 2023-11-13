using System.Text;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    public static Bundle ExtractionBundle(
        string compositionId,
        string patientId,
        string userId,
        string noteId,
        IReadOnlyCollection<string> imageIds
    )
    {
        List<Bundle.EntryComponent> entries =
            new()
            {
                new()
                {
                    Resource = new Composition
                    {
                        Id = compositionId,
                        Date = DateTime.Now.ToString("o"),
                        Event =
                        {
                            new()
                            {
                                Period = new() { Start = "2023-10-29", End = "2023-10-29" }
                            }
                        },
                        Type = new()
                        {
                            Coding =
                            {
                                new() { System = "ProcedureCode", Code = "DEF002" }
                            }
                        },
                        Section =
                        {
                            new()
                            {
                                Title = "Note",
                                Code = new()
                                {
                                    Coding =
                                    {
                                        new()
                                        {
                                            System = "https://1beat.care/fhir/coding-system",
                                            Code = "12345",
                                            Display = "Note"
                                        }
                                    }
                                },
                                Entry = { new($"DocumentReference/{noteId}") }
                            }
                        },
                        Extension =
                        {
                            new()
                            {
                                Url = "Composition#composition.extraField",
                                Value = new FhirString("extension test")
                            }
                        }
                    }
                },
                new()
                {
                    Resource = new DocumentReference
                    {
                        Id = noteId,
                        Subject = new ResourceReference($"Patient/{patientId}"),
                        Content = { new() { Attachment = new() { Data = Encoding.UTF8.GetBytes("Hello World") } } },
                        Author = { new ResourceReference($"Practitioner/{userId}") },
                        Category =
                        {
                            new()
                            {
                                Coding =
                                {
                                    new() { System = "http://bscglobal.com/CodeSystem/free-text-type", Code = "note" }
                                }
                            }
                        }
                    }
                },
            };

        entries.AddRange(
            imageIds.Select(
                id =>
                    new Bundle.EntryComponent
                    {
                        Resource = new DocumentReference
                        {
                            Id = id,
                            Author = { new ResourceReference($"Practitioner/{userId}") },
                            Category =
                            {
                                new()
                                {
                                    Coding =
                                    {
                                        new()
                                        {
                                            System = "http://bscglobal.com/CodeSystem/free-text-type",
                                            Code = "general-note-image"
                                        }
                                    }
                                }
                            },
                            Subject = new ResourceReference($"Patient/{patientId}")
                        }
                    }
            )
        );

        return new() { Type = Bundle.BundleType.Collection, Entry = entries };
    }
}
