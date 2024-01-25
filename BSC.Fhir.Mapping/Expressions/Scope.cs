using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class Scope : IClonable<Scope>
{
    public int Id { get; private set; }
    public int Level { get; }
    public Questionnaire Questionnaire { get; private init; }
    public QuestionnaireResponse QuestionnaireResponse { get; set; }
    public Questionnaire.ItemComponent? Item { get; private init; }
    public QuestionnaireResponse.ItemComponent? ResponseItem { get; private init; }
    public List<IQuestionnaireContext<BaseList>> Context { get; private init; } = new();
    public Scope? Parent { get; private init; }
    public List<Scope> Children { get; private init; } = new();
    public Base? DefinitionResolution { get; set; }

    public Scope? ClonedFrom { get; private init; }

    private readonly INumericIdProvider _idProvider;
    private ExtractionContext? _extractionContext;
    private bool _extractionContextSearched = false;

    public Scope(
        Questionnaire questionnaire,
        QuestionnaireResponse questionnaireResponse,
        INumericIdProvider idProvider
    )
    {
        Questionnaire = questionnaire;
        QuestionnaireResponse = questionnaireResponse;
        _idProvider = idProvider;
        Id = _idProvider.GetId();
    }

    public Scope(
        Scope parentScope,
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem,
        INumericIdProvider idProvider
    )
        : this(parentScope.Questionnaire, parentScope.QuestionnaireResponse, idProvider)
    {
        Parent = parentScope;
        Item = item;
        ResponseItem = responseItem;
        parentScope.Children.Add(this);
    }

    public IReadOnlyCollection<Scope> GetChildScope(Func<Scope, bool> predicate)
    {
        var scopes = new List<Scope>();

        if (predicate(this))
        {
            scopes.Add(this);
        }

        foreach (var child in Children)
        {
            scopes.AddRange(child.GetChildScope(predicate));
        }

        return scopes;
    }

    public IReadOnlyCollection<IQuestionnaireContext<BaseList>> AllContextInSubtree()
    {
        var list = new HashSet<IQuestionnaireContext<BaseList>>(QuestionnaireContextComparer<BaseList>.Default);

        AddContextDependenciesToList(Context, list);

        foreach (var ctx in Children.SelectMany(child => child.AllContextInSubtree()))
        {
            list.Add(ctx);
        }

        return list;
    }

    private static void AddContextDependenciesToList(
        IEnumerable<IQuestionnaireContext<BaseList>> contexts,
        HashSet<IQuestionnaireContext<BaseList>> allContext
    )
    {
        foreach (var ctx in contexts)
        {
            allContext.Add(ctx);
            if (ctx is IQuestionnaireExpression<BaseList> expr)
            {
                AddContextDependenciesToList(
                    expr.Dependencies.OfType<IQuestionnaireExpression<BaseList>>(),
                    allContext
                );
            }
        }
    }

    public Scope Clone(dynamic? replacementFields = null)
    {
        var newScope = new Scope(Questionnaire, QuestionnaireResponse, _idProvider)
        {
            Item = Item,
            ResponseItem = Item is not null
                ? new QuestionnaireResponse.ItemComponent { LinkId = Item.LinkId, Answer = ResponseItem?.Answer }
                : null,
            Parent = replacementFields?.Parent ?? Parent,
            ClonedFrom = this
        };
        newScope.Children.AddRange(Children.Select(child => child.Clone(new { Parent = newScope })));

        CopyContextToScope(newScope);

        return newScope;
    }

    private void CopyContextToScope(Scope scope)
    {
        var clonedContext = Context
            .OfType<IClonable<IQuestionnaireExpression<BaseList>>>()
            .Select(ctx => ctx.Clone(new { Id = _idProvider.GetId(), Scope = scope }));
        scope.Context.AddRange(clonedContext);
    }

    public bool HasRequiredAnswers()
    {
        var hasCalculated =
            Item?.Required == true && Context.Any(ctx => ctx.Type == QuestionnaireContextType.CalculatedExpression);
        var hasInitial = Item?.Required == true && Item?.Initial.Count > 0;

        var hasAnswers = ResponseItem?.Answer.Count > 0 || hasCalculated || hasInitial;

        return hasAnswers || Children.Any(child => child.HasRequiredAnswers());
    }

    public bool HasAnswers()
    {
        return ResponseItem?.Answer.Count > 0
            || Item?.Initial.Count > 0
            || Context.Any(ctx => ctx.Type == QuestionnaireContextType.CalculatedExpression)
            || Children.Any(child => child.HasAnswers());
    }

    public IQuestionnaireContext<BaseList>? ExtractionContext()
    {
        return GetContext(expr => expr.Type == QuestionnaireContextType.ExtractionContext, this);
    }

    public ExtractionContext? ExtractionContextValue()
    {
        if (_extractionContextSearched)
        {
            return _extractionContext;
        }

        if (DefinitionResolution is not null)
        {
            _extractionContext = new ExtractionContext(DefinitionResolution);
        }
        else if (!_extractionContextSearched)
        {
            var context = Context.FirstOrDefault(ctx => ctx.Type == QuestionnaireContextType.ExtractionContext);
            _extractionContextSearched = true;

            if (context is not null && context.Resolved())
            {
                _extractionContext = new(context.Value!.First());
            }
        }

        if (_extractionContext is null)
        {
            _extractionContext = Parent?._extractionContext;
        }

        return _extractionContext;
    }

    public IQuestionnaireContext<BaseList>? GetContext(int id)
    {
        return GetContext(expr => expr.Id == id, this);
    }

    public ResolvedContext<BaseList>? GetResolvedContext(int id)
    {
        var context = GetContext(expr => expr.Resolved() && expr.Id == id, this);
        return context is not null ? new(context) : null;
    }

    public IQuestionnaireContext<BaseList>? GetContext(string name)
    {
        return GetContext(expr => expr.Name == name, this);
    }

    public ResolvedContext<BaseList>? GetResolvedContext(string name)
    {
        var context = GetContext(expr => expr.Resolved() && expr.Name == name, this);

        return context is not null ? new(context) : null;
    }

    public IEnumerable<Scope> Path()
    {
        var path = new List<Scope> { this };
        var parent = Parent;

        while (parent is not null)
        {
            path.Add(parent);
            parent = parent.Parent;
        }

        path.Reverse();

        return path;
    }

    private static IQuestionnaireContext<BaseList>? GetContext(
        Func<IQuestionnaireContext<BaseList>, bool> predicate,
        Scope scope
    )
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
