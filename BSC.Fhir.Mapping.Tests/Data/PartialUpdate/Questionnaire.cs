using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class PartialUpdate
{
    public static Questionnaire CreateQuestionnaire()
    {
        return new()
        {
            Id = Guid.NewGuid().ToString(),
            Name = "PartialUpdateQuestionnaire",
            Title = "Partial Update Questionnaire",
            SubjectType = new[] { ResourceType.Patient as ResourceType? },
            Status = PublicationStatus.Draft,
            Extension =
            {
                new()
                {
                    Url = Constants.LAUNCH_CONTEXT,
                    Extension =
                    {
                        new()
                        {
                            Url = "name",
                            Value = new Coding
                            {
                                System = "http://hl7.org/fhir/uv/sdc/CodeSystem/launchContext",
                                Code = "patient",
                                Display = "Patient"
                            }
                        },
                        new() { Url = "type", Value = new FhirString("Patient") }
                    }
                },
                new()
                {
                    Url = Constants.EXTRACTION_CONTEXT,
                    Value = new Expression
                    {
                        Language = Constants.FHIR_QUERY_MIME,
                        Expression_ = "Patient?_id={{%patient.id}}"
                    }
                }
            },
            Item =
            {
                new()
                {
                    LinkId = "name",
                    Definition = "Patient.name",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Item =
                    {
                        new()
                        {
                            LinkId = "givenName",
                            Definition = "Patient.name.given",
                            Type = Questionnaire.QuestionnaireItemType.String,
                        },
                        new()
                        {
                            LinkId = "familyName",
                            Definition = "Patient.name.family",
                            Type = Questionnaire.QuestionnaireItemType.String,
                        },
                    }
                },
                new()
                {
                    LinkId = "gender",
                    Definition = "Patient.gender",
                    AnswerValueSet = "https://1beat.care/fhir/ValueSet/gender",
                    Type = Questionnaire.QuestionnaireItemType.Choice
                },
                new()
                {
                    LinkId = "contacts",
                    Definition = "Patient.contact",
                    Type = Questionnaire.QuestionnaireItemType.Group,
                    Item =
                    {
                        new()
                        {
                            LinkId = "contactRelationship",
                            Definition = "Patient.contact.relationship",
                            Type = Questionnaire.QuestionnaireItemType.Choice,
                            AnswerValueSet = "https://1beat.care/fhir/ValueSet/contact-relationship"
                        }
                    }
                }
            },
        };
    }
}
