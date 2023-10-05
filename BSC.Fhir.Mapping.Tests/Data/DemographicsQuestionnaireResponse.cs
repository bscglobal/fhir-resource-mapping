using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class Demographics
{
    public static QuestionnaireResponse CreateQuestionnaireResponse()
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
