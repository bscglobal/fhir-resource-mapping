using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class LaunchContext : IQuestionnaireContext<BaseList>
{
    private readonly HashSet<IQuestionnaireExpression<BaseList>> _dependants =
        new(QuestionnaireContextComparer<BaseList>.Default);

    public string Name { get; }
    public BaseList Value { get; }
    public int Id { get; }
    public Scope<BaseList> Scope { get; }
    public QuestionnaireContextType Type => QuestionnaireContextType.LaunchContext;

    public IEnumerable<IQuestionnaireExpression<BaseList>> Dependants => _dependants.AsEnumerable();

    public LaunchContext(int id, string name, Resource value, Scope<BaseList> scope)
    {
        Id = id;
        Name = name;
        Value = new[] { value };
        Scope = scope;
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
