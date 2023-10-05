using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    public static Composition CreateComposition()
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
}
