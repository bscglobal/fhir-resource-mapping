using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class FhirPathExpression : QuestionnaireExpression<BaseList>
{
    public Base? SourceResource { get; private set; }

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

    public override IQuestionnaireExpression<BaseList> Clone(dynamic? replacementFields = null)
    {
        if (replacementFields is null)
        {
            throw new ArgumentNullException(nameof(replacementFields));
        }

        return new FhirPathExpression(
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
            _dependencies = new HashSet<IQuestionnaireContext<BaseList>>(
                _dependencies,
                QuestionnaireContextComparer<BaseList>.Default
            ),
            _dependants = new HashSet<IQuestionnaireExpression<BaseList>>(
                _dependants,
                QuestionnaireContextComparer<BaseList>.Default
            ),
            ClonedFrom = this
        };
    }

    public void SetValue(BaseList value, Base source)
    {
        SetValue(value);
        SourceResource = source;
    }
}
