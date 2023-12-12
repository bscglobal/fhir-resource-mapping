using System.Text;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    public static Dictionary<string, IReadOnlyCollection<Resource>> EmptyResourceLoaderResponse(string compositionId)
    {
        return new()
        {
            { $"Composition?_id={compositionId}", Array.Empty<Resource>() },
            {
                $"DocumentReference?_has:Composition:entry:_id={compositionId}&category=http://bscglobal.com/CodeSystem/free-text-type|note",
                Array.Empty<Resource>()
            },
            {
                $"DocumentReference?_has:Composition:entry:_id={compositionId}&category=http://bscglobal.com/CodeSystem/free-text-type|general-note-image",
                Array.Empty<Resource>()
            },
        };
    }

    public static Dictionary<string, IReadOnlyCollection<Resource>> ResourceLoaderResponse(
        string compositionId,
        string patientId,
        string userId,
        string[] imageIds,
        string noteId
    )
    {
        return new()
        {
            { $"Composition?_id={compositionId}", Composition(compositionId, imageIds, noteId) },
            {
                $"DocumentReference?_has:Composition:entry:_id={compositionId}&category=http://bscglobal.com/CodeSystem/free-text-type|note",
                NoteReference(noteId, patientId, userId)
            },
            {
                $"DocumentReference?_has:Composition:entry:_id={compositionId}&category=http://bscglobal.com/CodeSystem/free-text-type|general-note-image",
                ImageReferences(imageIds, patientId, userId)
            },
        };
    }

    private static IReadOnlyCollection<Resource> Composition(
        string compositionId,
        string[] existingImageIds,
        string noteId
    )
    {
        return new Resource[]
        {
            new Composition
            {
                Id = compositionId,
                Date = "2023-10-14",
                Event =
                {
                    new()
                    {
                        Period = new() { Start = "2023-10-23", End = "2023-10-23" }
                    }
                },
                Type = new()
                {
                    Coding =
                    {
                        new() { System = "ProcedureCode", Code = "TEST0042" }
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
                        Entry = { new($"DocumentReference/{noteId}") },
                        Text = new("This is text that should not be overwritten")
                    },
                    new()
                    {
                        Title = "Image",
                        Code = new()
                        {
                            Coding =
                            {
                                new()
                                {
                                    System = "https://1beat.care/fhir/coding-system",
                                    Code = "54321",
                                    Display = "Images"
                                }
                            }
                        },
                        Entry = existingImageIds.Select(id => new ResourceReference($"DocumentReference/{id}")).ToList()
                    },
                },
                Extension =
                {
                    new() { Url = "Composition#composition.extraField", Value = new FhirString("extension test") }
                }
            },
        };
    }

    private static IReadOnlyCollection<Resource> NoteReference(string noteId, string patientId, string userId)
    {
        return new[]
        {
            new DocumentReference
            {
                Id = noteId,
                Category =
                {
                    new()
                    {
                        Coding =
                        {
                            new() { System = "http://bscglobal.com/CodeSystem/free-text-type", Code = "note" }
                        }
                    }
                },
                Subject = new($"Patient/{patientId}"),
                Content =
                {
                    new() { Attachment = new() { Data = Encoding.UTF8.GetBytes("This is the original note") } }
                },
                Author = { new($"Practitioner/{userId}") }
            }
        };
    }

    private static IReadOnlyCollection<Resource> ImageReferences(string[] imageIds, string patientId, string userId)
    {
        return imageIds
            .Select(
                id =>
                    new DocumentReference
                    {
                        Id = id,
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
                        Subject = new($"Patient/{patientId}"),
                        Author = { new($"Practitioner/{userId}") }
                    }
            )
            .ToArray();
    }
}
