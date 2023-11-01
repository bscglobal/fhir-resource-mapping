using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class FhirPathExpression<T> : QuestionnaireExpression<T>
{
    public FhirPathExpression(
        int id,
        string? name,
        string expr,
        Scope scope,
        QuestionnaireContextType type,
        Questionnaire.ItemComponent? questionnaireItem,
        QuestionnaireResponse.ItemComponent? questionnaireResponseItem
    )
        : base(id, name, expr, Constants.FHIRPATH_MIME, scope, type, questionnaireItem, questionnaireResponseItem) { }

    public override IQuestionnaireExpression<T> Clone(dynamic? replacementFields = null)
    {
        if (replacementFields is null)
        {
            throw new ArgumentNullException(nameof(replacementFields));
        }

        return new FhirPathExpression<T>(
            replacementFields.Id,
            Name,
            Expression,
            replacementFields.Scope,
            Type,
            QuestionnaireItem,
            QuestionnaireResponseItem
        )
        {
            Value = Value,
            _dependencies = new HashSet<IQuestionnaireContext<T>>(
                _dependencies,
                QuestionnaireContextComparer<T>.Default
            ),
            _dependants = new HashSet<IQuestionnaireExpression<T>>(
                _dependants,
                QuestionnaireContextComparer<T>.Default
            ),
            ClonedFrom = this
        };
    }
}
