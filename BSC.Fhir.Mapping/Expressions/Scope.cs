using System.Text.Json;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class Scope<T> : IClonable<Scope<T>>
{
    public int Level { get; }
    public Questionnaire Questionnaire { get; private init; }
    public QuestionnaireResponse? QuestionnaireResponse { get; set; }
    public Questionnaire.ItemComponent? Item { get; private init; }
    public QuestionnaireResponse.ItemComponent? ResponseItem { get; private init; }
    public List<IQuestionnaireContext<T>> Context { get; private init; } = new();
    public Scope<T>? Parent { get; private init; }
    public List<Scope<T>> Children { get; private init; } = new();

    public Scope<T>? ClonedFrom { get; private init; }

    private readonly INumericIdProvider _idProvider;

    public Scope(Scope<T> parent, INumericIdProvider idProvider)
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

    public Scope(Questionnaire.ItemComponent item, Scope<T> parentScope, INumericIdProvider idProvider)
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
        Scope<T> parentScope,
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

    public IReadOnlyCollection<IQuestionnaireContext<T>> AllContextInSubtree()
    {
        var list = new HashSet<IQuestionnaireContext<T>>(QuestionnaireContextComparer<T>.Default);

        AddContextDependenciesToList(Context, list);

        foreach (var ctx in Children.SelectMany(child => child.AllContextInSubtree()))
        {
            list.Add(ctx);
        }

        return list;
    }

    private static void AddContextDependenciesToList(
        IEnumerable<IQuestionnaireContext<T>> contexts,
        HashSet<IQuestionnaireContext<T>> allContext
    )
    {
        foreach (var ctx in contexts)
        {
            allContext.Add(ctx);
            if (ctx is IQuestionnaireExpression<T> expr)
            {
                AddContextDependenciesToList(expr.Dependencies.OfType<IQuestionnaireExpression<T>>(), allContext);
            }
        }
    }

    public Scope<T> Clone(dynamic? replacementFields = null)
    {
        var newScope = new Scope<T>(_idProvider, Questionnaire, QuestionnaireResponse)
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

    private void CopyContextToScope(Scope<T> scope)
    {
        var clonedContext = Context
            .OfType<IClonable<IQuestionnaireExpression<T>>>()
            .Select(ctx => ctx.Clone(new { Id = _idProvider.GetId(), Scope = scope }));
        scope.Context.AddRange(clonedContext);
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

        if (name == "patientName")
        {
            // TreeDebugging.PrintTree(this);
            Console.WriteLine("Debug: scope level - {0}", Level);
        }

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
