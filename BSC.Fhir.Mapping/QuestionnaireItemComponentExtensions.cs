using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

public static class QuestionnaireItemComponentExtensions
{
    private const string QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html";
    private const string VARIABLE_EXPRESSION = "http://hl7.org/fhir/StructureDefinition/variable";

    public static EvaluationResult? CalculatedExpressionResult(
        this Questionnaire.ItemComponent questionnaireItem,
        MappingContext context
    )
    {
        return questionnaireItem.ExpressionResult(QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION, context).SingleOrDefault();
    }

    public static EvaluationResult[] VariableExpressionResult(
        this Questionnaire.ItemComponent questionnaireItem,
        MappingContext context
    )
    {
        return questionnaireItem.ExpressionResult(VARIABLE_EXPRESSION, context);
    }

    private static EvaluationResult[] ExpressionResult(
        this Questionnaire.ItemComponent questionnaireItem,
        string url,
        MappingContext ctx
    )
    {
        var extensions = questionnaireItem.GetExtensions(url).ToArray();
        var results = new List<EvaluationResult>(extensions.Length);

        foreach (var extension in extensions)
        {
            if (!(extension.Value is Expression expression))
            {
                continue;
            }

            if (expression.Language != "text/fhirpath")
            {
                continue;
            }

            // var result = FhirPathMapping.EvaluateExpr(expression.Expression_, ctx, expression.Name);
            EvaluationResult? result = null;
            if (result is not null)
            {
                results.Add(result);
            }
        }

        return results.ToArray();
    }
}
