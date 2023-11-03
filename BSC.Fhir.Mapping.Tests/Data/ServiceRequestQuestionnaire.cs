using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class ServiceRequest
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
        var parser = new FhirJsonParser();
        var json = """
            {
              "resourceType": "Questionnaire",
              "id": "servreq",
              "meta": {
                "profile": [
                  "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-extr-defn"
                ]
              },
              "extension": [
                {
                  "extension": [
                    {
                      "url": "name",
                      "valueCoding": {
                        "system": "http://hl7.org/fhir/uv/sdc/CodeSystem/launchContext",
                        "code": "servicerequest"
                      }
                    },
                    {
                      "url": "type",
                      "valueCode": "ServiceRequest"
                    }
                  ],
                  "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-launchContext"
                },
                {
                  "extension": [
                    {
                      "url": "name",
                      "valueCoding": {
                        "system": "http://hl7.org/fhir/uv/sdc/CodeSystem/launchContext",
                        "code": "user"
                      }
                    },
                    {
                      "url": "type",
                      "valueCode": "Practitioner"
                    }
                  ],
                  "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-launchContext"
                },
                {
                  "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext",
                  "valueExpression": {
                    "language": "application/x-fhir-query",
                    "expression": "Patient?_id={{%patient.id}}"
                  }
                }
              ],
              "url": "http://hl7.org/fhir/uv/sdc/Questionnaire/demographics",
              "version": "3.0.0",
              "name": "ServReq example",
              "title": "Questionnaire - ServReq Example",
              "status": "draft",
              "experimental": true,
              "subjectType": ["ServiceRequest"],
              "date": "2022-10-01T05:09:13+00:00",
              "publisher": "1beat",
              "contact": [
                {
                  "telecom": [
                    {
                      "system": "url",
                      "value": "http://hl7.org/Special/committees/fiwg"
                    }
                  ]
                }
              ],
              "description": "A sample questionnaire using context-based population and extraction",
              "jurisdiction": [
                {
                  "coding": [
                    {
                      "system": "http://unstats.un.org/unsd/methods/m49/m49.htm",
                      "code": "001"
                    }
                  ]
                }
              ],
              "item": [
                {
                  "extension": [
                    {
                      "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                      "valueBoolean": true
                    },
                    {
                      "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                      "valueExpression": {
                        "language": "text/fhirpath",
                        "expression": "%patient.id"
                      }
                    }
                  ],
                  "linkId": "patient.id",
                  "definition": "Patient.id",
                  "text": "(internal use)",
                  "type": "string",
                  "readOnly": true
                },
{
                  "extension": [
                    {
                      "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                      "valueBoolean": true
                    },
                    {
                      "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                      "valueExpression": {
                        "language": "text/fhirpath",
                        "expression": "%servicerequest.id"
                      }
                    }
                  ],
                  "linkId": "servicerequest.id",
                  "definition": "ServiceRequest.id",
                  "text": "(internal use)",
                  "type": "string",
                  "readOnly": true
                },
                {
                  "linkId": "servicerequest.occurrence",
                  "definition": "ServiceRequest.occurrence",
                  "text": "Dates",
                  "type": "group",
                  "extension":[{
                  "url": "FhirType",
                      "valueString": "Period"
                      }],
                    "item": [
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                          "valueExpression": {
                            "language": "text/fhirpath",
                            "expression": "%servicerequest.occurrence.start"
                          }
                        }
                      ],
                  "linkId": "servicerequest.occurrence.start",
                  "definition": "ServiceRequest.occurrence.start",
                  "text": "Date start",
                  "type": "date",
                  "required": true
                }]}
 
              ]
            }
""";

        var result = parser.Parse<Questionnaire>(json);

        result.Item.Add(
            new()
            {
                LinkId = "extensionTeam",
                Type = Questionnaire.QuestionnaireItemType.Group,
                Definition = "ServiceRequest#servicerequest.extension:team",
                Item =
                {
                    new()
                    {
                        Definition = "ServiceRequest.extension.url",
                        LinkId = "extensionTeam.url",
                        Type = Questionnaire.QuestionnaireItemType.Text,
                        Initial = { new() { Value = new FhirString("TeamExtension") } }
                    },
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
            }
        );
        result.Item.Add(
            new()
            {
                LinkId = "extension",
                Type = Questionnaire.QuestionnaireItemType.Group,
                Definition = "ServiceRequest#servicerequest.extension:careUnit",
                Item =
                {
                    new()
                    {
                        Definition = "ServiceRequest.extension.url",
                        LinkId = "extension.url",
                        Type = Questionnaire.QuestionnaireItemType.Text,
                        Initial = { new() { Value = new FhirString("CareUnitExtension") } }
                    },
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
        );
        return result;
    }
}
