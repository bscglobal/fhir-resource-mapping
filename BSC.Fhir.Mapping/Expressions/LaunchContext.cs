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
    public IReadOnlyCollection<Base> Value { get; }
    public int Id { get; }
    public QuestionnaireContextType Type => QuestionnaireContextType.LaunchContext;

    public IEnumerable<IQuestionnaireExpression<BaseList>> Dependants => _dependants.AsEnumerable();

    public LaunchContext(INumericIdProvider idProvider, string name, Resource value)
    {
        Id = idProvider.GetId();
        Name = name;
        Value = new[] { value };
    }

    public bool Resolved()
    {
        return true;
    }

    public bool AddDependant(IQuestionnaireExpression<BaseList> dependant)
    {
        _dependants.Add(dependant);

        return true;
    }
}
