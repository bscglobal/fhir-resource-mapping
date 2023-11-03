using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IQuestionnaireExpression<T> : IQuestionnaireContext<T>, IClonable<IQuestionnaireExpression<T>>
{
    string Expression { get; }
    string ExpressionLanguage { get; }
    IEnumerable<IQuestionnaireContext<T>> Dependencies { get; }
    Questionnaire.ItemComponent? QuestionnaireItem { get; }
    QuestionnaireResponse.ItemComponent? QuestionnaireResponseItem { get; }

    void AddDependency(IQuestionnaireContext<T> dependency);
    void RemoveDependency(IQuestionnaireContext<T> dependency);
    void MakeResponseDependant();
    bool DependenciesResolved();
    bool HasDependency(Func<IQuestionnaireContext<T>, bool> predicate);
    void SetValue(T? value);
    void ReplaceExpression(string expression);
}
