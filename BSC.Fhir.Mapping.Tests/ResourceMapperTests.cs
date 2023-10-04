using BSC.Fhir.Mapping.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Moq;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Tests;

public class ResourceMapperTests
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

    [Fact]
    public async Task Extract_GivesCorrectBundle()
    {
        // Console.WriteLine();
        // Console.WriteLine("=================");
        // Console.WriteLine("Extract");
        // Console.WriteLine("=================");
        // Console.WriteLine();

        var demoQuestionnaire = CreateQuestionnaire();
        var demoQuestionnaireResponse = CreateQuestionnaireResponse();

        var bundle = await ResourceMapper.Extract(demoQuestionnaire, demoQuestionnaireResponse, new());

        // Console.WriteLine(bundle.ToJson(new FhirJsonSerializationSettings { Pretty = true }));

        Assert.True(true);
    }

    [Fact]
    public void Populate_GivesCorrectQuestionnaireResponseForDemo()
    {
        var familyName = "Smith";
        var demoQuestionnaire = CreateQuestionnaire();
        var patient = new Patient
        {
            Id = Guid.NewGuid().ToString(),
            BirthDate = "2006-04-05",
            Name =
            {
                new() { Family = familyName, Given = new[] { "Jane", "Rebecca" } }
            }
        };

        var relative = new RelatedPerson
        {
            Id = Guid.NewGuid().ToString(),
            Patient = new ResourceReference($"Patient/{patient.Id}"),
            Relationship =
            {
                new CodeableConcept
                {
                    Coding =
                    {
                        new Coding
                        {
                            System = "http://hl7.org/fhir/ValueSet/relatedperson-relationshiptype",
                            Code = "NOK",
                            Display = "next of kin"
                        }
                    }
                }
            },
            Name =
            {
                new() { Family = familyName, Given = new[] { "John", "Mark" } }
            }
        };

        var response = ResourceMapper.Populate(demoQuestionnaire, patient, relative);

        // Console.WriteLine();
        // Console.WriteLine("=================");
        // Console.WriteLine("Populate");
        // Console.WriteLine("=================");
        // Console.WriteLine();
        //
        // Console.WriteLine(response.ToJson(new() { Pretty = true }));

        var actualPatientIdAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "patient.id")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(patient.Id, actualPatientIdAnswer);

        var actualPatientBirthDateAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "patient.birthDate")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(patient.BirthDate, actualPatientBirthDateAnswer);

        var actualPatientFamilyNameAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "patient.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "patient.name.family")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(familyName, actualPatientFamilyNameAnswer);

        var actualPatientGivenNamesAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "patient.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "patient.name.given")
            ?.Answer.Select(answer => answer.Value.ToString());
        Assert.Equivalent(new[] { "Jane", "Rebecca" }, actualPatientGivenNamesAnswer);

        var actualRelativeIdAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "relative")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.id")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(actualRelativeIdAnswer, relative.Id);

        var actualRelativePatientAnswer = (
            response.Item
                .SingleOrDefault(item => item.LinkId == "relative")
                ?.Item.SingleOrDefault(item => item.LinkId == "relative.patient")
                ?.Answer.FirstOrDefault()
                ?.Value as ResourceReference
        )?.Reference;
        Assert.Equal(actualRelativePatientAnswer, $"Patient/{patient.Id}");

        var actualRelativeRelationshipAnswer =
            response.Item
                .SingleOrDefault(item => item.LinkId == "relative")
                ?.Item.SingleOrDefault(item => item.LinkId == "relative.relationship")
                ?.Answer.FirstOrDefault()
                ?.Value as Coding;
        Assert.Equivalent(relative.Relationship.First().Coding.First(), actualRelativeRelationshipAnswer);

        var actualRelativeFamilyNameAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "relative")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name.family")
            ?.Answer.FirstOrDefault()
            ?.Value.ToString();
        Assert.Equal(familyName, actualRelativeFamilyNameAnswer);

        var actualRelativeGivenNamesAnswer = response.Item
            .SingleOrDefault(item => item.LinkId == "relative")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name")
            ?.Item.SingleOrDefault(item => item.LinkId == "relative.name.given")
            ?.Answer.Select(answer => answer.Value.ToString());
        Assert.Equivalent(new[] { "John", "Mark" }, actualRelativeGivenNamesAnswer);
    }

    [Fact]
    public void Populate_GivesCorrectQuestionnaireResponseForGeneralNote()
    {
        var questionnaire = CreateGeneralNoteQuestionnaire();
        var composition = CreateGeneralNoteComposition();
        var documentReference = CreateGeneralNoteDocumentReference();

        // var response = ResourceMapper.Populate(questionnaire, documentReference, composition);

        // Console.WriteLine(questionnaire.ToJson(new() { Pretty = true }));
        // Console.WriteLine(response.ToJson(new() { Pretty = true }));
    }

    [Fact]
    public async Task Extract_GivesCorrectBundleForGeneralNote()
    {
        var questionnaire = CreateGeneralNoteQuestionnaire();
        var response = CreateGeneralNoteQuestionnaireResponse();
        var profileLoaderMock = new Mock<IProfileLoader>();
        profileLoaderMock
            .Setup(x => x.LoadProfileAsync(It.IsAny<Canonical>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateCompositionProfile());

        var extractionResult = await ResourceMapper.Extract(questionnaire, response, new(), profileLoaderMock.Object);

        Console.WriteLine(extractionResult.ToJson(new() { Pretty = true }));
    }

    private static StructureDefinition CreateCompositionProfile()
    {
        return new()
        {
            Name = "composition-definition",
            Snapshot = new()
            {
                Element =
                {
                    new()
                    {
                        Path = "composition.section",
                        Slicing = new()
                        {
                            Discriminator = { ElementDefinition.DiscriminatorComponent.ForValueSlice("code") },
                            Rules = ElementDefinition.SlicingRules.Closed
                        },
                        Min = 2,
                        Max = "2"
                    },
                    new()
                    {
                        Path = "composition.section",
                        SliceName = "note",
                        Min = 1,
                        Max = "1"
                    },
                    new()
                    {
                        Path = "composition.section.code",
                        Min = 1,
                        Fixed = new CodeableConcept
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
                        }
                    }
                }
            }
        };
    }

    private static Questionnaire CreateGeneralNoteQuestionnaire()
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
                            LinkId = "noteSection.entry",
                            Definition = "Composition.section.title",
                            Type = Questionnaire.QuestionnaireItemType.Text,
                            Initial = { new() { Value = new FhirString("Image") } }
                        }
                    }
                },
                // new()
                // {
                //     LinkId = "noteDocumentReference",
                //     Type = Questionnaire.QuestionnaireItemType.Group,
                //     Extension =
                //     {
                //         new()
                //         {
                //             Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                //             Value = new Expression
                //             {
                //                 Name = "note",
                //                 Language = "application/x-fhir-query",
                //                 Expression_ =
                //                     "DocumentReference?_id={{%composition.section.where(title = 'Note').first().entry.first()}}"
                //             }
                //         }
                //     },
                //     Item =
                //     {
                //         new()
                //         {
                //             LinkId = "note.id",
                //             Definition = "DocumentReference.id",
                //             Text = "Note",
                //             Type = Questionnaire.QuestionnaireItemType.String,
                //             ReadOnly = true,
                //             Extension =
                //             {
                //                 new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                //             }
                //         },
                //         new()
                //         {
                //             LinkId = "note.subject",
                //             Definition = "DocumentReference.subject",
                //             Type = Questionnaire.QuestionnaireItemType.Reference,
                //             ReadOnly = true,
                //             Extension =
                //             {
                //                 new()
                //                 {
                //                     Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                //                     Value = new Expression { Language = "text/fhirpath", Expression_ = "%patient.id" }
                //                 },
                //                 new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                //             }
                //         },
                //         new()
                //         {
                //             LinkId = "note.content",
                //             Definition = "DocumentReference.content",
                //             Type = Questionnaire.QuestionnaireItemType.Group,
                //             Repeats = true,
                //             Item =
                //             {
                //                 new()
                //                 {
                //                     LinkId = "documentReference.content.attachment",
                //                     Definition = "DocumentReference.content.attachment",
                //                     Text = "Note",
                //                     Type = Questionnaire.QuestionnaireItemType.Attachment,
                //                 },
                //             }
                //         },
                //         new()
                //         {
                //             LinkId = "note.author",
                //             Definition = "DocumentReference.author",
                //             Text = "(internal use)",
                //             Type = Questionnaire.QuestionnaireItemType.Reference,
                //             Repeats = true,
                //             Extension =
                //             {
                //                 new()
                //                 {
                //                     Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                //                     Value = new Expression
                //                     {
                //                         Language = "text/fhirpath",
                //                         Expression_ = "%practitioner.id"
                //                     }
                //                 },
                //                 new() { Url = "allow-duplicates", Value = new FhirBoolean(false) },
                //                 new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                //             }
                //         },
                //     }
                // },
                // new()
                // {
                //     LinkId = "images",
                //     Text = "Images",
                //     Type = Questionnaire.QuestionnaireItemType.Group,
                //     Repeats = true,
                //     Extension =
                //     {
                //         new()
                //         {
                //             Url = ITEM_EXTRACTION_CONTEXT_EXTENSION_URL,
                //             Value = new Expression
                //             {
                //                 Name = "images",
                //                 Language = "application/x-fhir-query",
                //                 Expression_ = "DocumentReference?_has:Composition:entry:_id={{%compositionId}}"
                //             }
                //         }
                //     },
                //     Item =
                //     {
                //         new()
                //         {
                //             LinkId = "image.id",
                //             Definition = "DocumentReference.id",
                //             Text = "ImageId",
                //             Type = Questionnaire.QuestionnaireItemType.String,
                //             Extension =
                //             {
                //                 new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) },
                //             }
                //         },
                //         new()
                //         {
                //             LinkId = "image.author",
                //             Definition = "DocumentReference.subject",
                //             Type = Questionnaire.QuestionnaireItemType.Reference,
                //             Extension =
                //             {
                //                 new()
                //                 {
                //                     Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                //                     Value = new Expression
                //                     {
                //                         Language = "text/fhirpath",
                //                         Expression_ = "%practitioner.id"
                //                     }
                //                 },
                //                 new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                //             },
                //         },
                //         new()
                //         {
                //             LinkId = "image.subject",
                //             Definition = "DocumentReference.subject",
                //             Text = "(internal use)",
                //             Type = Questionnaire.QuestionnaireItemType.Reference,
                //             Extension =
                //             {
                //                 new()
                //                 {
                //                     Url = QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION,
                //                     Value = new Expression { Language = "text/fhirpath", Expression_ = "%patient.id" }
                //                 },
                //                 new() { Url = QUESTIONNAIRE_HIDDEN_URL, Value = new FhirBoolean(true) }
                //             },
                //         }
                //     }
                // }
            }
        };

        return questionnaire;
    }

    private static QuestionnaireResponse CreateGeneralNoteQuestionnaireResponse()
    {
        var json = """
            {
              "resourceType": "QuestionnaireResponse",
              "item": [
                {
                  "linkId": "composition.id",
                  "answer": [
                    {
                      "valueString": "2e100b95-aa0b-42f0-b129-b648383638ac"
                    }
                  ]
                },
                {
                  "linkId": "noteSection",
                  "item": [
                    {
                      "linkId": "noteSection.title",
                      "answer": [
                        {
                          "valueString": "Note"
                        }
                      ]
                    }
                  ]
                },
                {
                  "linkId": "imageSection",
                  "item": [
                    {
                      "linkId": "noteSection.entry",
                      "answer": [
                        {
                          "valueString": "Image"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
""";

        var parser = new FhirJsonParser();
        return parser.Parse<QuestionnaireResponse>(json);
    }

    private static DocumentReference CreateGeneralNoteDocumentReference()
    {
        var json = """
            {
                "resourceType": "DocumentReference",
                "id": "28dae626-f92c-4401-9ce1-b5acbc0dc11a",
                "meta": {
                    "versionId": "1",
                    "lastUpdated": "2023-07-25T07:52:58.927+00:00",
                    "tag": [
                        {
                            "system": "https://1beat.care/userteams",
                            "code": "CHBAH-PaedSurg"
                        }
                    ]
                },
                "status": "current",
                "type": {
                    "coding": [
                        {
                            "system": "http://terminology.hl7.org/CodeSystem/media-type",
                            "code": "text"
                        }
                    ]
                },
                "category": [
                    {
                        "coding": [
                            {
                                "system": "http://bscglobal.com/CodeSystem/free-text-type",
                                "code": "note"
                            }
                        ]
                    }
                ],
                "author": [
                    {
                        "reference": "Practitioner/4b27680a-fcfb-4bb3-9e48-3ddd6676fae0"
                    }
                ],
                "content": [
                    {
                        "attachment": {
                            "contentType": "text/plain; charset=utf-8",
                            "data": "VkdocGN5QnBjeUJoSUdkbGJtVnlZV3dnYm05MFpRPT0="
                        }
                    }
                ],
                "subject": {
                    "reference": "Patient/a2xb8b2c-hjkl-3nb5-2s57-5sft8636vlq1"
                }
            }
""";

        var parser = new FhirJsonParser();
        return parser.Parse<DocumentReference>(json);
    }

    private static Composition CreateGeneralNoteComposition()
    {
        var json = """
            {
                "resourceType": "Composition",
                "id": "2e100b95-aa0b-42f0-b129-b648383638ac",
                "meta": {
                    "versionId": "1",
                    "lastUpdated": "2023-07-25T07:52:59+00:00",
                    "tag": [
                        {
                            "system": "https://1beat.care/userteams",
                            "code": "CHBAH-PaedSurg"
                        }
                    ]
                },
                "status": "final",
                "type": {
                    "coding": [
                        {
                            "system": "http://loinc.org",
                            "code": "34109-9"
                        }
                    ],
                    "text": "Note"
                },
                "subject": {
                    "reference": "Patient/ff7da964-9829-4758-8cf1-91c15ad38c8b"
                },
                "date": "2023-07-25T07:52:58+00:00",
                "author": [
                    {
                        "reference": "Practitioner/4b27680a-fcfb-4bb3-9e48-3ddd6676fae0"
                    }
                ],
                "title": "General Note",
                "event": [
                    {
                        "period": {
                            "start": "2023-07-25",
                            "end": "2023-07-25"
                        }
                    }
                ],
                "section": [
                    {
                        "title": "Images",
                        "author": [
                            {
                                "reference": "Practitioner/4b27680a-fcfb-4bb3-9e48-3ddd6676fae0"
                            }
                        ],
                        "entry": [
                            {
                                "reference": "DocumentReference/d1c8b7d9-9fbb-40aa-8492-68055ca96383"
                            }
                        ]
                    },
                    {
                        "title": "Notes",
                        "author": [
                            {
                                "reference": "Practitioner/4b27680a-fcfb-4bb3-9e48-3ddd6676fae0"
                            }
                        ],
                        "entry": [
                            {
                                "reference": "DocumentReference/28dae626-f92c-4401-9ce1-b5acbc0dc11a"
                            }
                        ]
                    }
                ]
            }
""";

        var parser = new FhirJsonParser();
        return parser.Parse<Composition>(json);
    }

    private static Questionnaire CreateQuestionnaire()
    {
        var parser = new FhirJsonParser();
        var json = """
            {
              "resourceType": "Questionnaire",
              "id": "demographics",
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
                        "code": "patient"
                      }
                    },
                    {
                      "url": "type",
                      "valueCode": "Patient"
                    }
                  ],
                  "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-launchContext"
                },
                {
                  "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext",
                  "valueExpression": {
                    "language": "application/x-fhir-query",
                    "expression": "Patient?_id={{%25patient.id}}"
                  }
                }
              ],
              "url": "http://hl7.org/fhir/uv/sdc/Questionnaire/demographics",
              "version": "3.0.0",
              "name": "DemographicExample",
              "title": "Questionnaire - Demographics Example",
              "status": "draft",
              "experimental": true,
              "subjectType": ["Patient"],
              "date": "2022-10-01T05:09:13+00:00",
              "publisher": "HL7 International - FHIR Infrastructure Work Group",
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
                      "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                      "valueExpression": {
                        "language": "text/fhirpath",
                        "expression": "%patient.birthDate"
                      }
                    }
                  ],
                  "linkId": "patient.birthDate",
                  "definition": "Patient.birthDate",
                  "text": "Date of birth",
                  "type": "date",
                  "required": true
                },
                {
                  "extension": [
                    {
                      "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext",
                      "valueExpression": {
                        "name": "patientName",
                        "language": "text/fhirpath",
                        "expression": "%patient.name"
                      }
                    }
                  ],
                  "linkId": "patient.name",
                  "definition": "Patient.name",
                  "text": "Name(s)",
                  "type": "group",
                  "repeats": true,
                  "item": [
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                          "valueExpression": {
                            "language": "text/fhirpath",
                            "expression": "%patient.name.family"
                          }
                        }
                      ],
                      "linkId": "patient.name.family",
                      "definition": "Patient.name.family",
                      "text": "Family name",
                      "type": "string",
                      "required": true
                    },
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                          "valueExpression": {
                            "language": "text/fhirpath",
                            "expression": "%patient.name.given"
                          }
                        }
                      ],
                      "linkId": "patient.name.given",
                      "definition": "Patient.name.given",
                      "text": "Given name(s)",
                      "type": "string",
                      "required": true,
                      "repeats": true
                    }
                  ]
                },
                {
                  "extension": [
                    {
                      "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext",
                      "valueExpression": {
                        "name": "relative",
                        "language": "application/x-fhir-query",
                        "expression": "RelatedPerson?patient={{%patient.id}}"
                      }
                    },
                    {
                      "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext",
                      "valueExpression": {
                        "language": "application/x-fhir-query",
                        "expression": "RelatedPerson?patient={{%patient.id}}"
                      }
                    }
                  ],
                  "linkId": "relative",
                  "text": "Relatives, caregivers and other personal relationships",
                  "type": "group",
                  "repeats": true,
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
                            "expression": "%relatedPerson.id"
                          }
                        }
                      ],
                      "linkId": "relative.id",
                      "definition": "RelatedPerson.id",
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
                            "expression": "%relatedPerson.patient"
                          }
                        }
                      ],
                      "linkId": "relative.patient",
                      "definition": "RelatedPerson.patient",
                      "text": "(internal use)",
                      "type": "string",
                      "readOnly": true
                    },
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                          "valueExpression": {
                            "language": "text/fhirpath",
                            "expression": "%relatedPerson.relationship"
                          }
                        }
                      ],
                      "linkId": "relative.relationship",
                      "definition": "RelatedPerson.relationship",
                      "text": "Name(s)",
                      "type": "choice",
                      "required": true,
                      "repeats": true,
                      "answerValueSet": "http://hl7.org/fhir/ValueSet/relatedperson-relationshiptype"
                    },
                    {
                      "extension": [
                        {
                          "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext",
                          "valueExpression": {
                            "name": "relativeName",
                            "language": "text/fhirpath",
                            "expression": "%relatedPerson.name"
                          }
                        }
                      ],
                      "linkId": "relative.name",
                      "definition": "RelatedPerson.name",
                      "text": "Name(s)",
                      "type": "group",
                      "repeats": true,
                      "item": [
                        {
                          "extension": [
                            {
                              "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                              "valueExpression": {
                                "language": "text/fhirpath",
                                "expression": "%relatedPerson.name.family"
                              }
                            }
                          ],
                          "linkId": "relative.name.family",
                          "definition": "RelatedPerson.name.family",
                          "text": "Family name",
                          "type": "string",
                          "required": true
                        },
                        {
                          "extension": [
                            {
                              "url": "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression",
                              "valueExpression": {
                                "language": "text/fhirpath",
                                "expression": "%relatedPerson.name.given"
                              }
                            }
                          ],
                          "linkId": "relative.name.given",
                          "definition": "RelatedPerson.name.given",
                          "text": "Given name(s)",
                          "type": "string",
                          "required": true,
                          "repeats": true
                        }
                      ]
                    }
                  ]
                }
              ]
            }
""";

        var result = parser.Parse<Questionnaire>(json);

        return result;
    }

    private static QuestionnaireResponse CreateQuestionnaireResponse()
    {
        var patientId = Guid.NewGuid().ToString();
        var response = new QuestionnaireResponse
        {
            Status = QuestionnaireResponse.QuestionnaireResponseStatus.Completed,
            Item =
            {
                new()
                {
                    LinkId = "patient.id",
                    Definition = "Patient.id",
                    Answer = { new QuestionnaireResponse.AnswerComponent { Value = new FhirString(patientId) } }
                },
                new()
                {
                    LinkId = "patient.birthDate",
                    Definition = "Patient.birthDate",
                    Answer = { new() { Value = new Date(2023, 05, 07) } }
                },
                new()
                {
                    LinkId = "patient.name",
                    Definition = "Patient.name",
                    Item =
                    {
                        new()
                        {
                            LinkId = "patient.name.family",
                            Definition = "Patient.name.family",
                            Text = "Family name",
                            Answer = { new() { Value = new FhirString("Smith") } }
                        },
                        new()
                        {
                            LinkId = "patient.name.given",
                            Definition = "Patient.name.given",
                            Text = "Given name(s)",
                            Answer =
                            {
                                new() { Value = new FhirString("John") },
                                new() { Value = new FhirString("Mark") }
                            }
                        }
                    }
                },
                new()
                {
                    LinkId = "relative",
                    Text = "Relatives, caregivers and other personal relationships",
                    Item =
                    {
                        new()
                        {
                            LinkId = "relative.id",
                            Definition = "RelatedPerson.id",
                            Text = "(internal use)",
                            Answer = { new() { Value = new FhirString(Guid.NewGuid().ToString()) } }
                        },
                        new()
                        {
                            LinkId = "relative.patient",
                            Definition = "RelatedPerson.patient",
                            Answer = { new() { Value = new ResourceReference($"Patient/{patientId}") } }
                        },
                        new()
                        {
                            LinkId = "relative.relationship",
                            Definition = "RelatedPerson.relationship",
                            Text = "Name(s)",
                            Answer =
                            {
                                new()
                                {
                                    Value = new Coding
                                    {
                                        System = "http://hl7.org/fhir/ValueSet/relatedperson-relationshiptype",
                                        Code = "NOK",
                                        Display = "next of kin"
                                    }
                                }
                            }
                        },
                        new()
                        {
                            LinkId = "relative.name",
                            Definition = "RelatedPerson.name",
                            Text = "Name(s)",
                            Item =
                            {
                                new()
                                {
                                    LinkId = "relative.name.family",
                                    Definition = "RelatedPerson.name.family",
                                    Text = "Family name",
                                    Answer = { new() { Value = new FhirString("Smith") } }
                                },
                                new()
                                {
                                    LinkId = "relative.name.given",
                                    Definition = "RelatedPerson.name.given",
                                    Text = "Given name(s)",
                                    Answer =
                                    {
                                        new() { Value = new FhirString("Jane") },
                                        new() { Value = new FhirString("Rebecca") }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        return response;
    }
}
