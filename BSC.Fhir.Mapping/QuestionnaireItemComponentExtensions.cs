using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

public static class QuestionnaireItemComponentExtensions
{
    private const string QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html";

    public static EvaluationResult? CalculatedExpressionResult(
        this Questionnaire.ItemComponent questionnaireItem,
        MappingContext context
    )
    {
        var extension = questionnaireItem.Extension.FirstOrDefault(
            e => e.Url == QUESTIONNAIRE_ITEM_CALCULATED_EXPRESSION
        );

        if (extension is null || !(extension.Value is Expression expression))
        {
            return null;
        }

        if (expression.Language != "text/fhirpath")
        {
            return null;
        }

        return FhirPathMapping.EvaluateExpr(expression.Expression_, context);
    }
}
