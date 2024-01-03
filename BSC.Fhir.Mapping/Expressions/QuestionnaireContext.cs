using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class QuestionnaireContext : IQuestionnaireContext<BaseList>
{
    private readonly HashSet<IQuestionnaireExpression<BaseList>> _dependants =
        new(QuestionnaireContextComparer<BaseList>.Default);

    public string? Name { get; }
    public BaseList Value { get; }
    public int Id { get; }
    public Scope Scope { get; }
    public QuestionnaireContextType Type { get; }

    public IEnumerable<IQuestionnaireExpression<BaseList>> Dependants => _dependants.AsEnumerable();

    public QuestionnaireContext(int id, string? name, Resource value, Scope scope, QuestionnaireContextType type)
    {
        Id = id;
        Name = name;
        Value = new[] { value };
        Scope = scope;
        Type = type;
    }

    public bool Resolved()
    {
        return true;
    }

    public void AddDependant(IQuestionnaireExpression<BaseList> dependant)
    {
        _dependants.Add(dependant);
    }

    public void RemoveDependant(IQuestionnaireExpression<BaseList> dependant)
    {
        _dependants.Remove(dependant);
    }
}
