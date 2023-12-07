using System.Text;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public static partial class GeneralNote
{
    public static QuestionnaireResponse CreateQuestionnaireResponse(
        string compositionId,
        string noteId,
        IReadOnlyCollection<string> imageIds
    )
    {
        var response = BaseResponse(compositionId).AddNote(noteId).AddImages(imageIds);

        return response;
    }

    private static QuestionnaireResponse AddNote(this QuestionnaireResponse response, string noteId)
    {
        response.Item.Add(
            new QuestionnaireResponse.ItemComponent
            {
                LinkId = "note",
                Item =
                {
                    new() { LinkId = "note.id", Answer = { new() { Value = new FhirString(noteId) } } },
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
                                    new() { Value = new Attachment { Data = Encoding.UTF8.GetBytes("Hello World") } }
                                }
                            }
                        }
                    }
                }
            }
        );

        return response;
    }

    private static QuestionnaireResponse AddImages(
        this QuestionnaireResponse response,
        IReadOnlyCollection<string> imageIds
    )
    {
        response.Item.AddRange(
            imageIds.Select(
                id =>
                    new QuestionnaireResponse.ItemComponent
                    {
                        LinkId = "image",
                        Item =
                        {
                            new() { LinkId = "image.id", Answer = { new() { Value = new FhirString(id) } } }
                        }
                    }
            )
        );

        return response;
    }

    private static QuestionnaireResponse BaseResponse(string compositionId) =>
        new()
        {
            Item =
            {
                new() { LinkId = "composition.id", Answer = { new() { Value = new FhirString(compositionId) } } },
                new() { LinkId = "composition.date", },
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
                                    Answer = { new() { Value = new FhirDateTime("2023-10-29") } }
                                }
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
                            Value = new Coding { System = "ProcedureCode", Code = "DEF002" }
                        }
                    }
                },
                new() { LinkId = "composition.extension" },
            }
        };
}
