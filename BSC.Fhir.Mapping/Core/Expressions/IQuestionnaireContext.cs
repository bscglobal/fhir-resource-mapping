using BSC.Fhir.Mapping.Expressions;

namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IQuestionnaireContext<T>
{
    int Id { get; }
    string? Name { get; }
    T? Value { get; }
    QuestionnaireContextType Type { get; }
    IEnumerable<IQuestionnaireExpression<T>> Dependants { get; }
    Scope<T> Scope { get; }

    bool Resolved();
    void AddDependant(IQuestionnaireExpression<T> dependant);
    void RemoveDependant(IQuestionnaireExpression<T> dependency);
}
