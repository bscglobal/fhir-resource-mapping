using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class Scope<T>
{
    public Questionnaire Questionnaire { get; }
    public QuestionnaireResponse? QuestionnaireResponse { get; set; }
    public Questionnaire.ItemComponent? Item { get; }
    public QuestionnaireResponse.ItemComponent? ResponseItem { get; }
    public List<IQuestionnaireContext<T>> Context { get; } = new();
    public Scope<T>? Parent { get; }
    public List<Scope<T>> Children { get; } = new();

    public Scope(Scope<T> parent)
    {
        parent.Children.Add(this);
        Parent = parent;
        Questionnaire = parent.Questionnaire;
    }

    public Scope(Questionnaire questionnaire, QuestionnaireResponse? questionnaireResponse = null)
    {
        Questionnaire = questionnaire;
        QuestionnaireResponse = questionnaireResponse;
    }

    public Scope(Questionnaire.ItemComponent item, Scope<T> parentScope)
        : this(parentScope)
    {
        Item = item;
    }

    public Scope(Questionnaire.ItemComponent item, Questionnaire questionnaire)
        : this(questionnaire)
    {
        Item = item;
    }

    public Scope(
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem,
        Scope<T> parentScope
    )
        : this(item, parentScope)
    {
        ResponseItem = responseItem;
    }

    public Scope(
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem,
        Questionnaire questionnaire,
        QuestionnaireResponse questionnaireResponse
    )
        : this(item, questionnaire)
    {
        QuestionnaireResponse = questionnaireResponse;
        ResponseItem = responseItem;
    }

    public IQuestionnaireContext<T>? ExtractionContext()
    {
        return GetContext(expr => expr.Type == QuestionnaireContextType.ExtractionContext, this);
    }

    public IQuestionnaireContext<T>? GetContext(int id)
    {
        return GetContext(expr => expr.Id == id, this);
    }

    public ResolvedContext<T>? GetResolvedContext(int id)
    {
        var context = GetContext(expr => expr.Resolved() && expr.Id == id, this);
        return context is not null ? new(context) : null;
    }

    public IQuestionnaireContext<T>? GetContext(string name)
    {
        return GetContext(expr => expr.Name == name, this);
    }

    public ResolvedContext<T>? GetResolvedContext(string name)
    {
        var context = GetContext(expr => expr.Resolved() && expr.Name == name, this);
        return context is not null ? new(context) : null;
    }

    private static IQuestionnaireContext<T>? GetContext(Func<IQuestionnaireContext<T>, bool> predicate, Scope<T> scope)
    {
        var expression = scope.Context.FirstOrDefault(predicate);

        if (expression is not null)
        {
            return expression;
        }

        if (scope.Parent is not null)
        {
            return GetContext(predicate, scope.Parent);
        }

        return null;
    }
}
