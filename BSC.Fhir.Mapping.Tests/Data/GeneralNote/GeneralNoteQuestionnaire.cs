using BSC.Fhir.Mapping.Expressions;
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
    private const string LAUNCH_CONTEXT_EXTENSION_URL =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-launchContext";

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
                    Url = LAUNCH_CONTEXT_EXTENSION_URL,
                    Extension =
                    {
                        new()
                        {
                            Url = "name",
                            Value = new Coding
                            {
                                System = "http://hl7.org/fhir/uv/sdc/CodeSystem/launchContext",
                                Code = "patient",
                                Display = "Patient"
                            }
                        },
                        new() { Url = "type", Value = new FhirString("patient") }
                    }
                },
                new Extension
                {
                    Url = LAUNCH_CONTEXT_EXTENSION_URL,
                    Extension =
                    {
                        new()
                        {
                            Url = "name",
                            Value = new Coding
                            {
                                System = "http://hl7.org/fhir/uv/sdc/CodeSystem/launchContext",
                                Code = "user",
                                Display = "User"
                            }
                        },
                        new() { Url = "type", Value = new FhirString("Practitioner") }
                    }
                },
                new Extension
                {
                    Url = LAUNCH_CONTEXT_EXTENSION_URL,
                    Extension =
                    {
                        new()
                        {
                            Url = "name",
                            Value = new Coding
                            {
                                System = "http://hl7.org/fhir/uv/sdc/CodeSystem/launchContext",
                                Code = "composition",
                                Display = "Composition"
                            }
                        },
                        new() { Url = "type", Value = new FhirString("Composition") }
                    }
                },
                new Extension
                {
                    Url = VARIABLE_EXTENSION_URL,
                    Value = new Expression
                    {
                        Name = "compositionId",
                        Language = "text/fhirpath",
                        Expression_ = "%composition.id"
                    }
                },
                new Extension
                {
                    Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                    Value = new Expression
                    {
                        Language = "application/x-fhir-query",
                        Expression_ = "Composition?_id={{%compositionId}}"
                    }
                },
                new Extension
                {
                    Url = VARIABLE_EXTENSION_URL,
                    Value = new Expression
                    {
                        Name = "note",
                        Language = "application/x-fhir-query",
                        Expression_ =
                            "DocumentReference?_has:Composition:entry:_id={{%composition.id}}&category=http://bscglobal.com/CodeSystem/free-text-type|note"
                    }
                },
                new Extension
                {
                    Url = VARIABLE_EXTENSION_URL,
                    Value = new Expression
                    {
                        Name = "images",
                        Language = "application/x-fhir-query",
                        Expression_ =
                            "DocumentReference?_has:Composition:entry:_id={{%composition.id}}&category=http://bscglobal.com/CodeSystem/free-text-type|general-note-image"
                    }
                }
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
                    LinkId = "composition.date",
                    Definition = "Composition.date",
                    ReadOnly = true,
                    Type = Questionnaire.QuestionnaireItemType.DateTime,
                    Extension =
                    {
                        new()
                        {
                            Url = Constants.INITIAL_EXPRESSION,
                            Value = new Expression { Language = "text/fhirpath", Expression_ = "%composition.date" }
                        },
                        new()
                        {
                            Url = Constants.CALCULATED_EXPRESSION,
                            Value = new Expression { Language = "text/fhirpath", Expression_ = "now()" }
                        },
                        new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                    }
                },
                new()
                {
                    LinkId = "composition.event",
                    Definition = "Composition.event",
                    ReadOnly = true,
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Extension =
                    {
                        new()
                        {
                            Url = Constants.POPULATION_CONTEXT,
                            Value = new Expression
                            {
                                Name = "event",
                                Language = "text/fhirpath",
                                Expression_ = "%composition.event"
                            }
                        }
                    },
                    Item =
                    {
                        new()
                        {
                            LinkId = "composition.event.period",
                            Definition = "Composition.event.period",
                            Type = Questionnaire.QuestionnaireItemType.Group,
                            Extension =
                            {
                                new()
                                {
                                    Url = Constants.POPULATION_CONTEXT,
                                    Value = new Expression
                                    {
                                        Name = "eventPeriod",
                                        Language = "text/fhirpath",
                                        Expression_ = "%event.period"
                                    }
                                },
                                new()
                                {
                                    Url = Constants.VARIABLE_EXPRESSION,
                                    Value = new Expression
                                    {
                                        Name = "eventStart",
                                        Language = "text/fhirpath",
                                        Expression_ =
                                            "%context.item.where(linkId='composition.event.period.start').first()"
                                    }
                                }
                            },
                            Item =
                            {
                                new()
                                {
                                    LinkId = "composition.event.period.start",
                                    Definition = "Composition.event.period.start",
                                    Type = Questionnaire.QuestionnaireItemType.Date,
                                    Required = true,
                                    Text = "Date",
                                    Extension =
                                    {
                                        new()
                                        {
                                            Url = Constants.INITIAL_EXPRESSION,
                                            Value = new Expression
                                            {
                                                Name = "event",
                                                Language = "text/fhirpath",
                                                Expression_ = "%eventPeriod.start"
                                            }
                                        }
                                    },
                                },
                                new()
                                {
                                    LinkId = "composition.event.period.end",
                                    Definition = "Composition.event.period.end",
                                    Type = Questionnaire.QuestionnaireItemType.DateTime,
                                    Extension =
                                    {
                                        new()
                                        {
                                            Url = Constants.INITIAL_EXPRESSION,
                                            Value = new Expression
                                            {
                                                Language = "text/fhirpath",
                                                Expression_ = "%eventPeriod.end"
                                            }
                                        },
                                        new()
                                        {
                                            Url = Constants.CALCULATED_EXPRESSION,
                                            Value = new Expression
                                            {
                                                Language = "text/fhirpath",
                                                Expression_ = "%eventStart"
                                            }
                                        },
                                        new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                                    },
                                }
                            }
                        }
                    }
                },
                new()
                {
                    LinkId = "procedureCode",
                    Type = Questionnaire.QuestionnaireItemType.Choice,
                    Text = "procedure code",
                    Repeats = false,
                    Definition = "Composition.type",
                    Extension = new List<Extension>()
                    {
                        new()
                        {
                            Url = "http://hl7.org/fhir/StructureDefinition/questionnaire-itemControl",
                            Value = new CodeableConcept()
                            {
                                Coding = new() { new() { Code = "dropdown-search-single" } }
                            }
                        },
                        new() { Url = "answerSetCodeSearch", Value = new FhirString("ProcedureCode") },
                        new()
                        {
                            Url = Constants.INITIAL_EXPRESSION,
                            Value = new Expression { Language = "text/fhirpath", Expression_ = "%composition.type" }
                        }
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
                            Type = Questionnaire.QuestionnaireItemType.Text,
                            Initial = { new() { Value = new FhirString("Note") } }
                        },
                        new()
                        {
                            Definition = "Composition.section.entry",
                            LinkId = "noteSection.entry",
                            Type = Questionnaire.QuestionnaireItemType.Reference,
                            Extension =
                            {
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                                new()
                                {
                                    Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                                    Value = new Expression
                                    {
                                        Language = "text/fhirpath",
                                        Expression_ =
                                            "%resource.item.where(linkId='note').first().item.where(linkId='note.id').first()"
                                    }
                                },
                                new() { Url = "referenceType", Value = new FhirString("DocumentReference") }
                            }
                        }
                    },
                },
                // new()
                // {
                //     LinkId = "imageSection",
                //     Type = Questionnaire.QuestionnaireItemType.Group,
                //     Definition = "composition.section",
                //     Item =
                //     {
                //         new()
                //         {
                //             LinkId = "imageSection.title",
                //             Definition = "Composition.section.title",
                //             Type = Questionnaire.QuestionnaireItemType.Text,
                //             Initial = { new() { Value = new FhirString("Image") } }
                //         },
                //         new()
                //         {
                //             LinkId = "imageSection.entry",
                //             Definition = "Composition.section.entry",
                //             Type = Questionnaire.QuestionnaireItemType.Reference,
                //             Extension =
                //             {
                //                 new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                //                 new()
                //                 {
                //                     Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                //                     Value = new Expression
                //                     {
                //                         Language = "text/fhirpath",
                //                         Expression_ =
                //                             "%resource.item.where(linkId='image').select(item.where(linkId='image.id').first())"
                //                     }
                //                 }
                //             }
                //         }
                //     }
                // },
                new()
                {
                    LinkId = "composition.extension",
                    Type = Questionnaire.QuestionnaireItemType.Text,
                    Definition = "Composition#composition.extraField",
                    Initial = { new() { Value = new FhirString("extension test") } }
                },
                new()
                {
                    LinkId = "note",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Extension =
                    {
                        new()
                        {
                            Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                            Value = new Expression { Language = "text/fhirpath", Expression_ = "%note" }
                        },
                        new()
                        {
                            Url = Constants.POPULATION_CONTEXT,
                            Value = new Expression
                            {
                                Name = "popNote",
                                Language = "text/fhirpath",
                                Expression_ = "%note"
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
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                                new()
                                {
                                    Url = ITEM_INITIAL_EXPRESSION,
                                    Value = new Expression { Language = "text/fhirpath", Expression_ = "%popNote.id" }
                                },
                            }
                        },
                        new()
                        {
                            LinkId = "note.category",
                            Definition = "DocumentReference.category",
                            Type = Questionnaire.QuestionnaireItemType.Choice,
                            ReadOnly = true,
                            Initial =
                            {
                                new()
                                {
                                    Value = new Coding
                                    {
                                        System = "http://bscglobal.com/CodeSystem/free-text-type",
                                        Code = "note"
                                    }
                                },
                            },
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
                            Extension =
                            {
                                new()
                                {
                                    Url = Constants.POPULATION_CONTEXT,
                                    Value = new Expression
                                    {
                                        Name = "noteContent",
                                        Language = "text/fhirpath",
                                        Expression_ = "%popNote.content"
                                    }
                                }
                            },
                            Item =
                            {
                                new()
                                {
                                    LinkId = "note.content.attachment",
                                    Definition = "DocumentReference.content.attachment",
                                    Text = "Note",
                                    Type = Questionnaire.QuestionnaireItemType.Attachment,
                                    Extension =
                                    {
                                        new() { Url = "attachment-type", Value = new FhirString("Text") },
                                        new()
                                        {
                                            Url = ITEM_INITIAL_EXPRESSION,
                                            Value = new Expression
                                            {
                                                Language = "text/fhirpath",
                                                Expression_ = "%noteContent.attachment"
                                            }
                                        },
                                    }
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
                                    Value = new Expression { Language = "text/fhirpath", Expression_ = "%user.id" }
                                },
                                new() { Url = "allow-duplicates", Value = new FhirBoolean(false) },
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                            }
                        },
                    }
                },
                new()
                {
                    LinkId = "image",
                    Text = "Image",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Repeats = true,
                    Extension =
                    {
                        new()
                        {
                            Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                            Value = new Expression { Language = "text/fhirpath", Expression_ = "%images" }
                        },
                        new()
                        {
                            Url = Constants.POPULATION_CONTEXT,
                            Value = new Expression
                            {
                                Name = "image",
                                Language = "text/fhirpath",
                                Expression_ = "%images"
                            }
                        },
                        new()
                        {
                            Url = Constants.EXTRACTION_CONTEXT_ID,
                            Value = new Expression
                            {
                                Language = "text/fhirpath",
                                Expression_ = "%context.item.where(linkId='image.id').answer.value"
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
                                new()
                                {
                                    Url = ITEM_INITIAL_EXPRESSION,
                                    Value = new Expression { Language = "text/fhirpath", Expression_ = "%image.id" }
                                },
                            }
                        },
                        new()
                        {
                            LinkId = "image.author",
                            Definition = "DocumentReference.author",
                            Type = Questionnaire.QuestionnaireItemType.Reference,
                            Extension =
                            {
                                new()
                                {
                                    Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                                    Value = new Expression { Language = "text/fhirpath", Expression_ = "%user.id" }
                                },
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                            },
                        },
                        new()
                        {
                            LinkId = "image.category",
                            Definition = "DocumentReference.category",
                            Text = "(internal use)",
                            Type = Questionnaire.QuestionnaireItemType.Choice,
                            Repeats = true,
                            Initial =
                            {
                                new()
                                {
                                    Value = new Coding
                                    {
                                        System = "http://bscglobal.com/CodeSystem/free-text-type",
                                        Code = "general-note-image"
                                    }
                                },
                            },
                            Extension =
                            {
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                            }
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
