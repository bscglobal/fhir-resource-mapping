using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class QuestionnaireExpression<T> : IQuestionnaireExpression<T>
{
    protected bool _resolutionAttempted = false;
    protected HashSet<IQuestionnaireContext<T>> _dependencies { get; init; } =
        new(QuestionnaireContextComparer<T>.Default);
    protected HashSet<IQuestionnaireExpression<T>> _dependants { get; init; } =
        new(QuestionnaireContextComparer<T>.Default);

    public int Id { get; }
    public string? Name { get; }
    public string Expression { get; protected set; }
    public string ExpressionLanguage { get; }
    public bool ResponseDependant { get; private set; } = false;
    public QuestionnaireContextType Type { get; }
    public Questionnaire.ItemComponent? QuestionnaireItem { get; }
    public QuestionnaireResponse.ItemComponent? QuestionnaireResponseItem { get; }
    public T? Value { get; protected set; }
    public Scope Scope { get; }
    public IQuestionnaireExpression<T>? ClonedFrom { get; protected init; }

    public IEnumerable<IQuestionnaireExpression<T>> Dependants => _dependants.AsEnumerable();
    public IEnumerable<IQuestionnaireContext<T>> Dependencies => _dependencies.AsEnumerable();

    public QuestionnaireExpression(
        int id,
        string? name,
        string expr,
        string exprLanguage,
        Scope scope,
        QuestionnaireContextType type,
        Questionnaire.ItemComponent? questionnaireItem,
        QuestionnaireResponse.ItemComponent? questionnaireResponseItem
    )
    {
        Id = id;
        Expression = expr;
        ExpressionLanguage = exprLanguage;
        Name = name;
        QuestionnaireItem = questionnaireItem;
        QuestionnaireResponseItem = questionnaireResponseItem;
        Scope = scope;
        Type = type;
    }

    public void AddDependency(IQuestionnaireContext<T> dependency)
    {
        _dependencies.Add(dependency);
        dependency.AddDependant(this);
    }

    public void AddDependant(IQuestionnaireExpression<T> dependant)
    {
        _dependants.Add(dependant);
    }

    public void RemoveDependency(IQuestionnaireContext<T> dependency)
    {
        _dependencies.Remove(dependency);

        dependency.RemoveDependant(this);
    }

    public void RemoveDependant(IQuestionnaireExpression<T> dependant)
    {
        _dependants.Remove(dependant);
    }

    public bool HasDependency(Func<IQuestionnaireContext<T>, bool> predicate)
    {
        return Dependencies.Any(
            dep => predicate(dep) || (dep is IQuestionnaireExpression<T> expr && expr.HasDependency(predicate))
        );
    }

    public void MakeResponseDependant()
    {
        ResponseDependant = true;

        foreach (var dep in _dependants)
        {
            dep.MakeResponseDependant();
        }
    }

    public virtual void SetValue(T? value)
    {
        _resolutionAttempted = true;
        Value = value;
    }

    public bool Resolved()
    {
        return Value is not null || _resolutionAttempted;
    }

    public bool DependenciesResolved()
    {
        return _dependencies.All(dep => dep.Resolved());
    }

    public void ReplaceExpression(string expression)
    {
        Expression = expression;
    }

    public virtual IQuestionnaireExpression<T> Clone(dynamic? replacementFields = null)
    {
        if (replacementFields is null)
        {
            throw new ArgumentNullException(nameof(replacementFields));
        }

        return new QuestionnaireExpression<T>(
            replacementFields.Id,
            Name,
            Expression,
            ExpressionLanguage,
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
