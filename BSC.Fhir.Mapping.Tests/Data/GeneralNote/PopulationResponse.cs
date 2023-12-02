using System.Text;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    public static QuestionnaireResponse PopulationResponse(
        string compositionId,
        string patientId,
        string noteId,
        string[] imageIds
    )
    {
        var response = new QuestionnaireResponse
        {
            Item =
            {
                new() { LinkId = "composition.id", Answer = { new() { Value = new FhirString(compositionId) } } },
                new() { LinkId = "composition.date", Answer = { new() { Value = new Date("2023-10-14") } } },
                new()
                {
                    LinkId = "composition.event",
                    Item =
                    {
                        new()
                        {
                            LinkId = "composition.event.period",
                            Item =
                            {
                                new()
                                {
                                    LinkId = "composition.event.period.start",
                                    Answer = { new() { Value = new Date("2023-10-23") } }
                                },
                                new()
                                {
                                    LinkId = "composition.event.period.end",
                                    Answer = { new() { Value = new Date("2023-10-23") } }
                                },
                            }
                        }
                    }
                },
                new()
                {
                    LinkId = "procedureCode",
                    Answer =
                    {
                        new()
                        {
                            Value = new Coding { System = "ProcedureCode", Code = "TEST0042" }
                        }
                    }
                },
                new()
                {
                    LinkId = "noteSection",
                    Item =
                    {
                        new() { LinkId = "noteSection.title", Answer = { new() { Value = new FhirString("Note") } } },
                        new() { LinkId = "noteSection.entry" },
                    }
                },
                new()
                {
                    LinkId = "imageSection",
                    Item =
                    {
                        new()
                        {
                            LinkId = "imageSection.title",
                            Answer = { new() { Value = new FhirString("Images") } }
                        },
                        new() { LinkId = "imageSection.entry" },
                    }
                },
                new()
                {
                    LinkId = "composition.extension",
                    Answer = { new() { Value = new FhirString("extension test") } }
                },
                new()
                {
                    LinkId = "note",
                    Item =
                    {
                        new() { LinkId = "note.id", Answer = { new() { Value = new FhirString(noteId) } } },
                        new() { LinkId = "note.author" },
                        new() { LinkId = "note.subject" },
                        new()
                        {
                            LinkId = "note.category",
                            Answer =
                            {
                                new()
                                {
                                    Value = new Coding
                                    {
                                        System = "http://bscglobal.com/CodeSystem/free-text-type",
                                        Code = "note"
                                    }
                                }
                            }
                        },
                        new()
                        {
                            LinkId = "note.content",
                            Item =
                            {
                                new()
                                {
                                    LinkId = "note.content.attachment",
                                    Answer =
                                    {
                                        new()
                                        {
                                            Value = new Attachment
                                            {
                                                Data = Encoding.UTF8.GetBytes("This is the original note")
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                },
            }
        };

        response.Item.AddRange(
            imageIds.Select(
                id =>
                    new QuestionnaireResponse.ItemComponent
                    {
                        LinkId = "image",
                        Item =
                        {
                            new() { LinkId = "image.id", Answer = { new() { Value = new FhirString(id) } } },
                            new() { LinkId = "image.author" },
                            new()
                            {
                                LinkId = "image.category",
                                Answer =
                                {
                                    new()
                                    {
                                        Value = new Coding
                                        {
                                            System = "http://bscglobal.com/CodeSystem/free-text-type",
                                            Code = "general-note-image"
                                        }
                                    }
                                }
                            },
                            new() { LinkId = "image.subject" },
                        }
                    }
            )
        );

        return response;
    }
}
