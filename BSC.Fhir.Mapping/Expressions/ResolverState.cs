using BSC.Fhir.Mapping.Core;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class ScopeTree
{
    private Scope _currentScope;
    private readonly INumericIdProvider _idProvider;

    public Questionnaire.ItemComponent? CurrentItem => _currentScope?.Item;
    public QuestionnaireResponse.ItemComponent? CurrentResponseItem => _currentScope?.ResponseItem;
    public Scope CurrentScope => _currentScope;

    public ScopeTree(
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse,
        INumericIdProvider idProvider
    )
    {
        _idProvider = idProvider;
        _currentScope = new(questionnaire, questionnaireResponse, idProvider);
    }

    public void PushScope(Questionnaire.ItemComponent item, QuestionnaireResponse.ItemComponent? responseItem = null)
    {
        _currentScope = new(_currentScope, item, responseItem, _idProvider);
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

    public static Scope? GetScope(string linkId, Scope scope)
    {
        if (scope.Item?.LinkId == linkId)
        {
            return scope;
        }

        foreach (var child in scope.Children)
        {
            if (GetScope(linkId, child) is Scope found)
            {
                return found;
            }
        }

        return null;
    }
}
