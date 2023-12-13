using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data.Common;

public static class QuestionnaireItemCreator
{
    public static Questionnaire.ItemComponent Create(
        string linkId,
        string definition,
        bool required,
        bool readOnly,
        bool hidden,
        Questionnaire.QuestionnaireItemType type,
        FhirExpression? extractionContext,
        FhirExpression? populationContext,
        FhirExpression? initialExpression,
        IEnumerable<FhirExpression> variables,
        IEnumerable<Questionnaire.ItemComponent> items
    )
    {
        var item = new Questionnaire.ItemComponent
        {
            LinkId = linkId,
            Definition = definition,
            Required = required,
            ReadOnly = readOnly,
            Type = type,
            Item = items.ToList()
        };

        if (extractionContext is not null)
        {
            item.Extension.Add(
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
            item.Extension.Add(
                new Extension
                {
                    Url = Constants.POPULATION_CONTEXT,
                    Value = new Expression
                    {
                        Language = populationContext.Language,
                        Expression_ = populationContext.Expression,
                        Name = populationContext.Name
                    }
                }
            );
        }

        if (initialExpression is not null)
        {
            item.Extension.Add(
                new()
                {
                    Url = Constants.INITIAL_EXPRESSION,
                    Value = new Expression
                    {
                        Language = initialExpression.Language,
                        Expression_ = initialExpression.Expression,
                        Name = initialExpression.Name
                    }
                }
            );
        }

        item.Extension.AddRange(
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

        return item;
    }
}
