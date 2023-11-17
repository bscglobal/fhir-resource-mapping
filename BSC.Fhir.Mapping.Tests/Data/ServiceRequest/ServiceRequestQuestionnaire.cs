using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class TestServiceRequest
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
            Name = "ServiceRequestQuestionnaire",
            Title = "Service Request",
            SubjectType = new[] { ResourceType.ServiceRequest as ResourceType? },
            Status = PublicationStatus.Draft,
            Extension =
            {
                new()
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
                                Code = "serviceRequest",
                                Display = "ServiceRequest"
                            }
                        },
                        new() { Url = "type", Value = new FhirString("ServiceRequest") }
                    }
                },
                new()
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
                new()
                {
                    Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                    Value = new Expression
                    {
                        Language = Constants.FHIR_QUERY_MIME,
                        Expression_ = "ServiceRequest?_id={{%serviceRequest.id}}"
                    }
                }
            },
            Item =
            {
                new()
                {
                    LinkId = "servicerequest.id",
                    Definition = "ServiceRequest.id",
                    Type = Questionnaire.QuestionnaireItemType.String,
                    Extension =
                    {
                        new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                        new()
                        {
                            Url = ITEM_INITIAL_EXPRESSION,
                            Value = new Expression { Language = "text/fhirpath", Expression_ = "%serviceRequest.id" }
                        }
                    }
                },
                new()
                {
                    LinkId = "servicerequest.occurrence",
                    Definition = "ServiceRequest.occurrence",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Extension =
                    {
                        new() { Url = "FhirType", Value = new FhirString("Period") },
                        new()
                        {
                            Url = ITEM_POPULATION_CONTEXT,
                            Value = new Expression
                            {
                                Language = "text/fhirpath",
                                Expression_ = "%serviceRequest.occurrence"
                            }
                        }
                    },
                    Item =
                    {
                        new()
                        {
                            LinkId = "servicerequest.occurrence.start",
                            Definition = "ServiceRequest.occurrence.start",
                            Type = Questionnaire.QuestionnaireItemType.Date,
                            Extension =
                            {
                                new()
                                {
                                    Url = ITEM_INITIAL_EXPRESSION,
                                    Value = new Expression
                                    {
                                        Language = "text/fhirpath",
                                        Expression_ = "%serviceRequest.occurrence.start"
                                    }
                                }
                            }
                        }
                    }
                },
                new()
                {
                    LinkId = "extensionTeam",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Definition = "ServiceRequest#servicerequest.extension:team",
                    Item =
                    {
                        new()
                        {
                            Definition = "ServiceRequest.extension.value",
                            LinkId = "extensionTeam.value",
                            Type = Questionnaire.QuestionnaireItemType.String,
                            Extension =
                            {
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                            }
                        }
                    },
                },
                new()
                {
                    LinkId = "extension",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Definition = "ServiceRequest#servicerequest.extension:careUnit",
                    Item =
                    {
                        new()
                        {
                            Definition = "ServiceRequest.extension.value",
                            LinkId = "extension.value",
                            Type = Questionnaire.QuestionnaireItemType.Reference,
                            Extension =
                            {
                                new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                            }
                        }
                    },
                }
            }
        };

        return questionnaire;
    }
}
