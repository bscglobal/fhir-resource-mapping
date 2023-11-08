using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    public static QuestionnaireResponse CreateQuestionnaireResponse(string compositionId)
    {
        return new()
        {
            Item =
            {
                new() { LinkId = "composition.id", Answer = { new() { Value = new FhirString(compositionId) } } },
                new() { LinkId = "composition.date", },
                new()
                {
                    LinkId = "composition.event",
                    Item =
                    {
                        new()
                        {
                            LinkId = "composition.event.period",
                            Item =
                            {
                                new()
                                {
                                    LinkId = "composition.event.period.start",
                                    Answer = { new() { Value = new FhirDateTime("2023-10-29") } }
                                }
                            }
                        }
                    }
                },
                new()
                {
                    LinkId = "procedureCode",
                    Answer =
                    {
                        new()
                        {
                            Value = new Coding { System = "ProcedureCode", Code = "DEF002" }
                        }
                    }
                },
                new() { LinkId = "composition.extension" }
            }
        };
    }
}
