using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class ServiceRequest
{
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

        return result;
    }
}
