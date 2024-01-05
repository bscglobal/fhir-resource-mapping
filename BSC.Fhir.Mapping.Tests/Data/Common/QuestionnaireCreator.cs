using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data.Common;

public static class QuestionnaireCreator
{
    public static Questionnaire Create(
        IEnumerable<LaunchContext> launchContext,
        FhirExpression? extractionContext,
        FhirExpression? populationContext,
        IEnumerable<FhirExpression> variables,
        IEnumerable<Questionnaire.ItemComponent> items
    )
    {
        var questionnaire = new Questionnaire
        {
            Extension = launchContext
                .Select(
                    lc =>
                        new Extension
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
                                        Code = lc.Name,
                                        Display = lc.Display
                                    }
                                },
                                new() { Url = "type", Value = new Code(lc.Type) }
                            }
                        }
                )
                .ToList(),
            Item = items.ToList()
        };

        if (extractionContext is not null)
        {
            questionnaire.Extension.Add(
                new Extension
                {
                    Url = Constants.EXTRACTION_CONTEXT,
                    Value = new Expression
                    {
                        Language = extractionContext.Language,
                        Expression_ = extractionContext.Expression,
                        Name = extractionContext.Name
                    }
                }
            );
        }

        if (populationContext is not null)
        {
            questionnaire.Extension.Add(
                new Extension
                {
                    Url = Constants.EXTRACTION_CONTEXT,
                    Value = new Expression
                    {
                        Language = populationContext.Language,
                        Expression_ = populationContext.Expression,
                        Name = populationContext.Name
                    }
                }
            );
        }

        questionnaire.Extension.AddRange(
            variables.Select(
                v =>
                    new Extension
                    {
                        Url = Constants.VARIABLE_EXPRESSION,
                        Value = new Expression
                        {
                            Language = v.Language,
                            Expression_ = v.Expression,
                            Name = v.Name
                        }
                    }
            )
        );

        return questionnaire;
    }
}
