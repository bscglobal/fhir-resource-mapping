using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    public static QuestionnaireResponse CreateQuestionnaireResponse()
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
          "linkId": "noteSection.title",
          "answer": [
            {
              "valueString": "Image"
            }
          ]
        }
      ]
    },
    {
      "linkId": "composition.extension",
      "answer": [
        {
          "valueString": "extension test"
        }
      ]
    },
    {
      "linkId": "noteDocumentReference",
      "item": [
        {
          "linkId": "note.id",
          "answer": [ 
            {
              "valueString": "test id"
            }
          ]
        },
        {
          "linkId": "note.subject",
        },
        {
          "linkId": "note.content",
          "item": [
            {
              "linkId": "documentReference.content.attachment",
              "answer": [
                  {
                      "valueAttachment": {
                          "data": "aGVsbG8gd29ybGQ="
                      }
                  }
              ]
            }
          ]
        },
        {
          "linkId": "note.author"
        }
      ]
    },
    {
      "linkId": "image",
      "item": [
        {
          "linkId": "image.id",
          "answer": {
              "valueString": "image test id",
          }
        },
        {
          "linkId": "image.author"
        },
        {
          "linkId": "image.subject"
        }
      ]
    },
    {
      "linkId": "image",
      "item": [
        {
          "linkId": "image.id",
          "answer": {
              "valueString": "image test id 2",
          }
        },
        {
          "linkId": "image.author"
        },
        {
          "linkId": "image.subject"
        }
      ]
    },
  ]
}
""";

        var parser = new FhirJsonParser();
        return parser.Parse<QuestionnaireResponse>(json);
    }
}
