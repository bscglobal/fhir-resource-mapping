using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace BSC.Fhir.Mapping.Tests.Data;

public class NoteDocumentReference
{
    public static DocumentReference CreateDocumentReference()
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
}
