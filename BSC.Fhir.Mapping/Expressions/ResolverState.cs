using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class ResolverState
{
    private Scope<IReadOnlyCollection<Base>> _currentScope;

    private readonly HashSet<QuestionnaireExpression> _allExpressions = new();

    public Questionnaire.ItemComponent? CurrentItem => _currentScope?.Item;
    public QuestionnaireResponse.ItemComponent? CurrentResponseItem => _currentScope?.ResponseItem;
    public Scope<IReadOnlyCollection<Base>> CurrentScope => _currentScope;

    public ResolverState(Questionnaire questionnaire, QuestionnaireResponse? questionnaireResponse)
    {
        _currentScope = new(questionnaire, questionnaireResponse);
    }

    public void PushScope(Questionnaire.ItemComponent item)
    {
        _currentScope = new(item, _currentScope);
    }

    public void PushScope(Questionnaire.ItemComponent item, QuestionnaireResponse.ItemComponent responseItem)
    {
        _currentScope = new(item, responseItem, _currentScope);
    }

    public bool PopScope()
    {
        if (_currentScope.Parent is null)
        {
            return false;
        }

        _currentScope = _currentScope.Parent;
        return true;
    }

    public void AddExpressionToScope(QuestionnaireExpression expression)
    {
        _currentScope.Context.Add(expression);
        _allExpressions.Add(expression);
    }
}
