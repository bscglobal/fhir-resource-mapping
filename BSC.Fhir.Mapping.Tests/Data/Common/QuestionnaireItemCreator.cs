using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data.Common;

public static class QuestionnaireItemCreator
{
    public static Questionnaire.ItemComponent Create(
        string linkId,
        Questionnaire.QuestionnaireItemType type,
        string? definition = null,
        bool required = false,
        bool readOnly = false,
        bool hidden = false,
        bool repeats = false,
        FhirExpression? extractionContext = null,
        FhirExpression? populationContext = null,
        FhirExpression? initialExpression = null,
        FhirExpression? calculatedExpression = null,
        IEnumerable<FhirExpression>? variables = null,
        IEnumerable<DataType>? initial = null,
        IEnumerable<Questionnaire.ItemComponent>? items = null,
        string? answerValueSet = null,
        IEnumerable<Extension>? extensions = null
    )
    {
        var item = new Questionnaire.ItemComponent
        {
            LinkId = linkId,
            Definition = definition,
            Required = required,
            ReadOnly = readOnly,
            Repeats = repeats,
            Type = type,
            Item = items?.ToList() ?? new List<Questionnaire.ItemComponent>(),
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

        if (calculatedExpression is not null)
        {
            item.Extension.Add(
                new()
                {
                    Url = Constants.CALCULATED_EXPRESSION,
                    Value = new Expression
                    {
                        Language = calculatedExpression.Language,
                        Expression_ = calculatedExpression.Expression,
                        Name = calculatedExpression.Name
                    }
                }
            );
        }

        if (variables is not null)
        {
            item.Extension.AddRange(
                variables.Select(v => new Extension
                {
                    Url = Constants.VARIABLE_EXPRESSION,
                    Value = new Expression
                    {
                        Language = v.Language,
                        Expression_ = v.Expression,
                        Name = v.Name
                    }
                })
            );
        }

        if (initial is not null)
        {
            item.Initial = initial.Select(i => new Questionnaire.InitialComponent { Value = i }).ToList();
        }

        if (answerValueSet is not null)
        {
            item.AnswerValueSet = answerValueSet;
        }

        if (extensions is not null)
        {
            item.Extension.AddRange(extensions);
        }

        return item;
    }
}
