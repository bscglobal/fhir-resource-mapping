using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace BSC.Fhir.Mapping.Tests.Data;

public static class TestData
{
    public static Questionnaire CreateQuestionnaire()
    {
        var json = """
                {
                  "resourceType": "Questionnaire",
                  "extension": [
                    {
                      "extension": [
                        {
                          "url": "name",
                          "valueCoding": {
                            "system": "http://hl7.org/fhir/uv/sdc/CodeSystem/launchContext",
                            "code": "patient",
                            "display": "Patient"
                          }
                        },
                        {
                          "url": "type",
                          "valueString": "patient"
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
                            "code": "user",
                            "display": "User"
                          }
                        },
                        {
                          "url": "type",
                          "valueString": "Practitioner"
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
                            "code": "composition",
                            "display": "Composition"
                          }
                        },
                        {
                          "url": "type",
                          "valueString": "Composition"
                        }
                      ],
                      "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-launchContext"
                    },
                    {
                      "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext",
                      "valueExpression": {
                        "language": "application/x-fhir-query",
                        "expression": "Composition?_id={{%composition.Id}}"
                      }
                    },
                    {
                      "url": "http://hl7.org/fhir/StructureDefinition/variable",
                      "valueExpression": {
                        "name": "now",
                        "language": "text/fhirpath",
                        "expression": "now()"
                      }
                    }
                  ],
                  "identifier": [
                    {
                      "system": "formDefinitionId",
                      "value": "TEST001"
                    }
                  ],
                  "name": "GeneralNote",
                  "title": "General Note",
                  "status": "draft",
                  "subjectType": [
                    "Composition"
                  ],
                  "item": [
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                          "valueExpression": {
                            "language": "text/fhirpath",
                            "expression": "%composition.id"
                          }
                        },
                        {
                          "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                          "valueBoolean": true
                        },
                        {
                          "url": "internalUseOnly",
                          "valueBoolean": true
                        }
                      ],
                      "linkId": "composition.id",
                      "definition": "Composition.id",
                      "text": "(internal use)",
                      "type": "string",
                      "required": true,
                      "readOnly": true
                    },
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                          "valueExpression": {
                            "language": "text/fhirpath",
                            "expression": "%composition.date"
                          }
                        },
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html",
                          "valueExpression": {
                            "language": "text/fhirpath",
                            "expression": "now()"
                          }
                        },
                        {
                          "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                          "valueBoolean": true
                        }
                      ],
                      "linkId": "composition.date",
                      "definition": "Composition.date",
                      "type": "dateTime",
                      "readOnly": true
                    },
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext",
                          "valueExpression": {
                            "name": "event",
                            "language": "text/fhirpath",
                            "expression": "%composition.event"
                          }
                        }
                      ],
                      "linkId": "composition.event",
                      "definition": "Composition.event",
                      "type": "group",
                      "readOnly": true,
                      "item": [
                        {
                          "extension": [
                            {
                              "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext",
                              "valueExpression": {
                                "name": "eventPeriod",
                                "language": "text/fhirpath",
                                "expression": "%event.period"
                              }
                            },
                            {
                              "url": "http://hl7.org/fhir/StructureDefinition/variable",
                              "valueExpression": {
                                "name": "eventStart",
                                "language": "text/fhirpath",
                                "expression": "%context.item.where(linkId='composition.event.period.start').first()"
                              }
                            }
                          ],
                          "linkId": "composition.event.period",
                          "definition": "Composition.event.period",
                          "type": "group",
                          "item": [
                            {
                              "extension": [
                                {
                                  "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                                  "valueExpression": {
                                    "language": "text/fhirpath",
                                    "expression": "%eventPeriod.start"
                                  }
                                },
                                {
                                  "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-itemControl",
                                  "valueCodeableConcept": {
                                    "coding": [
                                      {
                                        "system": "value set????",
                                        "code": "calendar"
                                      }
                                    ]
                                  }
                                }
                              ],
                              "linkId": "composition.event.period.start",
                              "definition": "Composition.event.period.start",
                              "text": "Date",
                              "type": "date",
                              "required": true
                            },
                            {
                              "extension": [
                                {
                                  "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                                  "valueExpression": {
                                    "language": "text/fhirpath",
                                    "expression": "%eventPeriod.end"
                                  }
                                },
                                {
                                  "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html",
                                  "valueExpression": {
                                    "language": "text/fhirpath",
                                    "expression": "%eventStart"
                                  }
                                },
                                {
                                  "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                                  "valueBoolean": true
                                }
                              ],
                              "linkId": "composition.event.period.end",
                              "definition": "Composition.event.period.end",
                              "type": "dateTime"
                            }
                          ]
                        }
                      ]
                    },
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-itemControl",
                          "valueCodeableConcept": {
                            "coding": [
                              {
                                "code": "dropdown-search-single"
                              }
                            ]
                          }
                        },
                        {
                          "url": "answerSetCodeSearch",
                          "valueString": "ProcedureCode"
                        }
                      ],
                      "linkId": "procedureCode",
                      "definition": "Composition.type",
                      "text": "procedure code",
                      "type": "choice",
                      "repeats": false
                    },
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                          "valueBoolean": true
                        }
                      ],
                      "linkId": "noteSection",
                      "definition": "Composition#composition.section:note",
                      "type": "group",
                      "item": [
                        {
                          "extension": [
                            {
                              "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                              "valueBoolean": true
                            }
                          ],
                          "linkId": "noteSection.title",
                          "definition": "Composition.section.title",
                          "type": "text",
                          "initial": [
                            {
                              "valueString": "Note"
                            }
                          ]
                        },
                        {
                          "extension": [
                            {
                              "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                              "valueBoolean": true
                            },
                            {
                              "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html",
                              "valueExpression": {
                                "language": "text/fhirpath",
                                "expression": "%resource.item.where(linkId='noteDocumentReference').first().item.where(linkId='note.id').first()"
                              }
                            }
                          ],
                          "linkId": "noteSection.entry",
                          "definition": "Composition.section.entry",
                          "type": "reference"
                        }
                      ]
                    },
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext",
                          "valueExpression": {
                            "name": "note",
                            "language": "application/x-fhir-query",
                            "expression": "DocumentReference?_id={{%composition.section.where(title = 'Note').first().entry.first()}}"
                          }
                        }
                      ],
                      "linkId": "noteDocumentReference",
                      "type": "group",
                      "item": [
                        {
                          "extension": [
                            {
                              "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                              "valueBoolean": true
                            },
                            {
                              "url": "internalUseOnly",
                              "valueBoolean": true
                            }
                          ],
                          "linkId": "note.id",
                          "definition": "DocumentReference.id",
                          "text": "",
                          "type": "string",
                          "readOnly": true
                        },
                        {
                          "extension": [
                            {
                              "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                              "valueExpression": {
                                "language": "text/fhirpath",
                                "expression": "%patient.id"
                              }
                            },
                            {
                              "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                              "valueBoolean": true
                            }
                          ],
                          "linkId": "note.subject",
                          "definition": "DocumentReference.subject",
                          "type": "reference",
                          "readOnly": true
                        },
                        {
                          "linkId": "note.content",
                          "definition": "DocumentReference.content",
                          "type": "group",
                          "repeats": false,
                          "item": [
                            {
                              "extension": [
                                {
                                  "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-itemControl",
                                  "valueCodeableConcept": {
                                    "coding": [
                                      {
                                        "system": "value set????",
                                        "code": "long-text"
                                      }
                                    ]
                                  }
                                },
                                {
                                  "url": "attachment-type",
                                  "valueString": "Text"
                                }
                              ],
                              "linkId": "documentReference.content.attachment",
                              "definition": "DocumentReference.content.attachment",
                              "text": "Note",
                              "type": "attachment"
                            }
                          ]
                        },
                        {
                          "extension": [
                            {
                              "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html",
                              "valueExpression": {
                                "language": "text/fhirpath",
                                "expression": "%user.id"
                              }
                            },
                            {
                              "url": "allow-duplicates",
                              "valueBoolean": false
                            },
                            {
                              "url": "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden",
                              "valueBoolean": true
                            }
                          ],
                          "linkId": "note.author",
                          "definition": "DocumentReference.author",
                          "text": "(internal use)",
                          "type": "reference",
                          "repeats": true
                        }
                      ]
                    }
                  ]
                }
            """;

        var parser = new FhirJsonParser();
        var result = parser.Parse<Questionnaire>(json);

        return result;
    }
}
