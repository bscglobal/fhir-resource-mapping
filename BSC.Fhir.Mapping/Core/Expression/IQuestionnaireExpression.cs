using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IQuestionnaireExpression<T> : IQuestionnaireContext<T>
{
    public string Expression { get; }
    public string ExpressionLanguage { get; }
    public IEnumerable<IQuestionnaireContext<T>> Dependencies { get; }

    bool AddDependency(IQuestionnaireContext<T> dependency);
    void MakeResponseDependant();
    bool DependenciesResolved();
    void SetValue(T value);
    void ReplaceExpression(string expression);
}
