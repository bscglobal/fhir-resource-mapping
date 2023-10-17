using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class Demographics
{
    public static QuestionnaireResponse CreateQuestionnaireResponse(string patientId, string[] relativeIds)
    {
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
                                new() { Value = new FhirString("Matthew") },
                                new() { Value = new FhirString("William") }
                            }
                        }
                    }
                },
            }
        };

        var relatives = relativeIds.Select(
            id =>
                new QuestionnaireResponse.ItemComponent
                {
                    LinkId = "relative",
                    Item =
                    {
                        new()
                        {
                            LinkId = "relative.id",
                            Definition = "RelatedPerson.id",
                            Text = "(internal use)",
                            Answer = { new() { Value = new FhirString(id) } }
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
        );

        response.Item.AddRange(relatives);

        return response;
    }
}
