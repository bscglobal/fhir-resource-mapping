using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    private const string ITEM_EXTRACTION_CONTEXT_EXTENSION_URL =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext";
    private const string ITEM_INITIAL_EXPRESSION =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression";
    private const string QUESTIONNAIRE_HIDDEN_URL = "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden";
    private const string ITEM_POPULATION_CONTEXT =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext";
    private const string QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html";
    private const string VARIABLE_EXTENSION_URL = "http://hl7.org/fhir/StructureDefinition/variable";

    public static Questionnaire CreateQuestionnaire()
    {
        var questionnaire = new Questionnaire
        {
            Id = Guid.NewGuid().ToString(),
            Name = "GeneralNote",
            Title = "General Note",
            SubjectType = new[] { ResourceType.Composition as ResourceType? },
            Status = PublicationStatus.Draft,
            Extension =
            {
                new Extension
                {
                    Url = VARIABLE_EXTENSION_URL,
                    Value = new Expression
                    {
                        Name = "compositionId",
                        Language = "text/fhirpath",
                        Expression_ = "%resource.item.where(linkId = 'compostion.id').first().answer"
                    }
                },
                new Extension
                {
                    Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                    Value = new Expression
                    {
                        Name = "composition",
                        Language = "application/x-fhir-query",
                        Expression_ = "Composition?_id={{%compositionId}}"
                    }
                },
                new Extension
                {
                    Url = ITEM_POPULATION_CONTEXT,
                    Value = new Expression
                    {
                        Language = "application/x-fhir-query",
                        Expression_ = "Composition?_id={{%compositionId}}"
                    }
                },
            },
            Item =
            {
                new()
                {
                    LinkId = "composition.id",
                    Definition = "Composition.id",
                    ReadOnly = true,
                    Type = Questionnaire.QuestionnaireItemType.String,
                    Text = "(internal use)",
                    Extension =
                    {
                        new()
                        {
                            Url = ITEM_INITIAL_EXPRESSION,
                            Value = new Expression { Language = "text/fhirpath", Expression_ = "%composition.id" }
                        },
                        new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                    }
                },
                new()
                {
                    LinkId = "noteSection",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Definition = "Composition#composition.section:note",
                    Item =
                    {
                        new()
                        {
                            Definition = "Composition.section.title",
                            LinkId = "noteSection.title",
                            // Extension =
                            // {
                            //     new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                            //     new()
                            //     {
                            //         Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                            //         Value = new Expression
                            //         {
                            //             Language = "text/fhirpath",
                            //             Expression_ =
                            //                 "%resource.item.where(LinkId = 'noteDocumentReference').first().item.where(LinkId = 'note.id').first()"
                            //         }
                            //     }
                            // },
                            Type = Questionnaire.QuestionnaireItemType.Text,
                            Initial = { new() { Value = new FhirString("Note") } }
                        }
                    }
                },
                new()
                {
                    LinkId = "imageSection",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Definition = "composition.section",
                    Item =
                    {
                        new()
                        {
                            LinkId = "noteSection.title",
                            Definition = "Composition.section.title",
                            Type = Questionnaire.QuestionnaireItemType.Text,
                            Initial = { new() { Value = new FhirString("Image") } }
                        }
                    }
                },
                new()
                {
                    LinkId = "composition.extension",
                    Type = Questionnaire.QuestionnaireItemType.Text,
                    Definition = "Composition#composition.extraField",
                    Initial = { new() { Value = new FhirString("extension test") } }
                },
                new()
                {
                    LinkId = "noteDocumentReference",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Extension =
                    {
                        new()
                        {
                            Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                            Value = new Expression
                            {
                                Name = "note",
                                Language = "application/x-fhir-query",
                                Expression_ =
                                    "DocumentReference?_id={{%composition.section.where(title = 'Note').first().entry.first()}}"
                            }
                        }
                    },
                    Item =
                    {
                        new()
                        {
                            LinkId = "note.id",
                            Definition = "DocumentReference.id",
                            Text = "Note",
                            Type = Questionnaire.QuestionnaireItemType.String,
                            ReadOnly = true,
                            Extension =
                            {
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                            }
                        },
                        new()
                        {
                            LinkId = "note.subject",
                            Definition = "DocumentReference.subject",
                            Type = Questionnaire.QuestionnaireItemType.Reference,
                            ReadOnly = true,
                            Extension =
                            {
                                new()
                                {
                                    Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                                    Value = new Expression { Language = "text/fhirpath", Expression_ = "%patient.id" }
                                },
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                            }
                        },
                        new()
                        {
                            LinkId = "note.content",
                            Definition = "DocumentReference.content",
                            Type = Questionnaire.QuestionnaireItemType.Group,
                            Repeats = true,
                            Item =
                            {
                                new()
                                {
                                    LinkId = "documentReference.content.attachment",
                                    Definition = "DocumentReference.content.attachment",
                                    Text = "Note",
                                    Type = Questionnaire.QuestionnaireItemType.Attachment,
                                },
                            }
                        },
                        new()
                        {
                            LinkId = "note.author",
                            Definition = "DocumentReference.author",
                            Text = "(internal use)",
                            Type = Questionnaire.QuestionnaireItemType.Reference,
                            Repeats = true,
                            Extension =
                            {
                                new()
                                {
                                    Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                                    Value = new Expression
                                    {
                                        Language = "text/fhirpath",
                                        Expression_ = "%practitioner.id"
                                    }
                                },
                                new() { Url = "allow-duplicates", Value = new FhirBoolean(false) },
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                            }
                        },
                    }
                },
                new()
                {
                    LinkId = "images",
                    Text = "Images",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Repeats = true,
                    Extension =
                    {
                        new()
                        {
                            Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                            Value = new Expression
                            {
                                Name = "images",
                                Language = "application/x-fhir-query",
                                Expression_ = "DocumentReference?_has:Composition:entry:_id={{%compositionId}}"
                            }
                        }
                    },
                    Item =
                    {
                        new()
                        {
                            LinkId = "image.id",
                            Definition = "DocumentReference.id",
                            Text = "ImageId",
                            Type = Questionnaire.QuestionnaireItemType.String,
                            Extension =
                            {
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                            }
                        },
                        new()
                        {
                            LinkId = "image.author",
                            Definition = "DocumentReference.subject",
                            Type = Questionnaire.QuestionnaireItemType.Reference,
                            Extension =
                            {
                                new()
                                {
                                    Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                                    Value = new Expression
                                    {
                                        Language = "text/fhirpath",
                                        Expression_ = "%practitioner.id"
                                    }
                                },
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                            },
                        },
                        new()
                        {
                            LinkId = "image.subject",
                            Definition = "DocumentReference.subject",
                            Text = "(internal use)",
                            Type = Questionnaire.QuestionnaireItemType.Reference,
                            Extension =
                            {
                                new()
                                {
                                    Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                                    Value = new Expression { Language = "text/fhirpath", Expression_ = "%patient.id" }
                                },
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                            },
                        }
                    }
                }
            }
        };

        return questionnaire;
    }
}
