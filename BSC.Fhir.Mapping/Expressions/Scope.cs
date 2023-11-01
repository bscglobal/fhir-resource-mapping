using System.Text.Json;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class Scope : IClonable<Scope>
{
    public int Level { get; }
    public Questionnaire Questionnaire { get; private init; }
    public QuestionnaireResponse? QuestionnaireResponse { get; set; }
    public Questionnaire.ItemComponent? Item { get; private init; }
    public QuestionnaireResponse.ItemComponent? ResponseItem { get; private init; }
    public List<IQuestionnaireContext<BaseList>> Context { get; private init; } = new();
    public Scope? Parent { get; private init; }
    public List<Scope> Children { get; private init; } = new();

    public Scope? ClonedFrom { get; private init; }

    private readonly INumericIdProvider _idProvider;

    public Scope(Scope parent, INumericIdProvider idProvider)
    {
        parent.Children.Add(this);
        Parent = parent;
        Questionnaire = parent.Questionnaire;

        Level = Parent?.Level + 1 ?? 0;
        _idProvider = idProvider;
    }

    public Scope(
        INumericIdProvider idProvider,
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse = null
    )
    {
        Questionnaire = questionnaire;
        QuestionnaireResponse = questionnaireResponse;
        _idProvider = idProvider;
    }

    public Scope(Questionnaire.ItemComponent item, Scope parentScope, INumericIdProvider idProvider)
        : this(parentScope, idProvider)
    {
        Item = item;
    }

    public Scope(Questionnaire.ItemComponent item, INumericIdProvider idProvider, Questionnaire questionnaire)
        : this(idProvider, questionnaire)
    {
        Item = item;
    }

    public Scope(
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem,
        Scope parentScope,
        INumericIdProvider idProvider
    )
        : this(item, parentScope, idProvider)
    {
        ResponseItem = responseItem;
    }

    public Scope(
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem,
        INumericIdProvider idProvider,
        Questionnaire questionnaire,
        QuestionnaireResponse questionnaireResponse
    )
        : this(item, idProvider, questionnaire)
    {
        QuestionnaireResponse = questionnaireResponse;
        ResponseItem = responseItem;
        _idProvider = idProvider;
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
        var newScope = new Scope(_idProvider, Questionnaire, QuestionnaireResponse)
        {
            Item = Item,
            QuestionnaireResponse = QuestionnaireResponse,
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

    public IQuestionnaireContext<BaseList>? ExtractionContext()
    {
        return GetContext(expr => expr.Type == QuestionnaireContextType.ExtractionContext, this);
    }

    public Resource? ExtractionContextValue()
    {
        return GetContext(expr => expr.Type == QuestionnaireContextType.ExtractionContext, this)
                ?.Value?.FirstOrDefault() as Resource;
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

        if (name == "patientName")
        {
            // TreeDebugging.PrintTree(this);
            Console.WriteLine("Debug: scope level - {0}", Level);
        }

        return context is not null ? new(context) : null;
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
