using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class FhirQueryExpression<T> : QuestionnaireExpression<T>
{
    public FhirQueryExpression(
        int id,
        string? name,
        string expr,
        Scope scope,
        QuestionnaireContextType type,
        Questionnaire.ItemComponent? questionnaireItem,
        QuestionnaireResponse.ItemComponent? questionnaireResponseItem
    )
        : base(id, name, expr, Constants.FHIR_QUERY_MIME, scope, type, questionnaireItem, questionnaireResponseItem) { }
}
