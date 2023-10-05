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
          "linkId": "note.id"
        },
        {
          "linkId": "note.subject"
        },
        {
          "linkId": "note.content",
          "item": [
            {
              "linkId": "documentReference.content.attachment"
            }
          ]
        },
        {
          "linkId": "note.author"
        }
      ]
    },
    {
      "linkId": "images",
      "item": [
        {
          "linkId": "image.id"
        },
        {
          "linkId": "image.author"
        },
        {
          "linkId": "image.subject"
        }
      ]
    }
  ]
}
""";

        var parser = new FhirJsonParser();
        return parser.Parse<QuestionnaireResponse>(json);
    }
}
