using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

using B = IReadOnlyCollection<Base>;

public class QuestionnaireExpression : IQuestionnaireExpression<B>
{
    private readonly HashSet<IQuestionnaireContext<B>> _dependencies = new(QuestionnaireContextComparer<B>.Default);
    private readonly HashSet<IQuestionnaireExpression<B>> _dependants = new(QuestionnaireContextComparer<B>.Default);

    public int Id { get; }
    public string? Name { get; }
    public string Expression { get; private set; }
    public string ExpressionLanguage { get; }
    public bool ResponseDependant { get; private set; } = false;
    public QuestionnaireContextType Type { get; }
    public Questionnaire.ItemComponent? QuestionnaireItem { get; }
    public QuestionnaireResponse.ItemComponent? QuestionnaireResponseItem { get; }
    public B? Value { get; private set; }

    public IEnumerable<IQuestionnaireExpression<B>> Dependants => _dependants.AsEnumerable();
    public IEnumerable<IQuestionnaireContext<B>> Dependencies => _dependencies.AsEnumerable();

    public QuestionnaireExpression(
        int id,
        Expression expression,
        QuestionnaireContextType type,
        Questionnaire.ItemComponent? questionnaireItem,
        QuestionnaireResponse.ItemComponent? questionnaireResponseItem
    )
    {
        Id = id;
        Expression = expression.Expression_;
        ExpressionLanguage = expression.Language;
        Name = expression.Name;
        Type = type;
        QuestionnaireItem = questionnaireItem;
        QuestionnaireResponseItem = questionnaireResponseItem;
    }

    public bool AddDependency(IQuestionnaireContext<B> dependency)
    {
        if (dependency.Id == Id)
        {
            return false;
        }

        _dependencies.Add(dependency);

        foreach (var dependant in _dependants)
        {
            if (!dependant.AddDependency(dependency))
            {
                return false;
            }
        }

        return true;
    }

    public bool AddDependant(IQuestionnaireExpression<B> dependant)
    {
        if (dependant.Id == Id)
        {
            return false;
        }

        _dependants.Add(dependant);

        foreach (var dependency in _dependencies)
        {
            if (!dependency.AddDependant(dependant))
            {
                return false;
            }
        }

        return true;
    }

    public void MakeResponseDependant()
    {
        ResponseDependant = true;

        foreach (var dep in _dependants)
        {
            dep.MakeResponseDependant();
        }
    }

    public void SetValue(B value)
    {
        Value = value;
    }

    public bool Resolved()
    {
        return Value is not null;
    }

    public bool DependenciesResolved()
    {
        return _dependencies.All(dep => dep.Resolved());
    }

    public void ReplaceExpression(string expression)
    {
        Expression = expression;
    }
}
