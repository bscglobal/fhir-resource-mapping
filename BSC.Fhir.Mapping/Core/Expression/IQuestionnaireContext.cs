namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IQuestionnaireContext<T>
{
    public int Id { get; }
    public string? Name { get; }
    public T? Value { get; }
    public QuestionnaireContextType Type { get; }
    public IEnumerable<IQuestionnaireExpression<T>> Dependants { get; }

    public bool Resolved();
    public bool AddDependant(IQuestionnaireExpression<T> dependant);
}
