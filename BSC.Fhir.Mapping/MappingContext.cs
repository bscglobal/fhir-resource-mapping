using System.Reflection;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

public class ContextValue<T>
    where T : Base
{
    public T[] Value { get; set; }
    public string? Name { get; set; }

    public ContextValue(T[] value, string? name = null)
    {
        Value = value;
        Name = name;
    }

    public ContextValue(T value, string? name = null)
    {
        Value = new[] { value };
        Name = name;
    }
}

public class ContextValue : ContextValue<Base>
{
    public ContextValue(Base[] value, string? name = null)
        : base(value, name) { }

    public ContextValue(Base value, string? name = null)
        : base(value, name) { }
}

public class Context
{
    public Base Value { get; }
    public HashSet<PropertyInfo> DirtyFields { get; } = new();

    public Context(Base value)
    {
        Value = value;
    }
}

public class MappingContext
{
    private readonly Stack<Context> _extractionContext = new();
    private readonly Stack<Questionnaire.ItemComponent> _questionnaireItems = new();
    private readonly Stack<(
        QuestionnaireResponse.ItemComponent ResponseItem,
        Dictionary<string, ContextValue> Context
    )> _questionnaireResponseItems = new();
    private readonly Dictionary<string, ContextValue> _globalContext = new Dictionary<string, ContextValue>();

    public Context? CurrentExtractionContext => _extractionContext.TryPeek(out var context) ? context : null;
    public Questionnaire.ItemComponent QuestionnaireItem => _questionnaireItems.Peek();
    public QuestionnaireResponse.ItemComponent QuestionnaireResponseItem =>
        _questionnaireResponseItems.Peek().ResponseItem;

    public IReadOnlyDictionary<string, ContextValue> CurrentContext =>
        _questionnaireResponseItems.TryPeek(out var currentResponseItem) ? currentResponseItem.Context : _globalContext;

    public Questionnaire Questionnaire { get; private set; }
    public QuestionnaireResponse? QuestionnaireResponse { get; set; }

    public MappingContext(Questionnaire questionnaire, QuestionnaireResponse? questionnaireResponse = null)
    {
        QuestionnaireResponse = questionnaireResponse;
        Questionnaire = questionnaire;
    }

    public void SetCurrentExtractionContext(Base context)
    {
        _extractionContext.Push(new(context));
    }

    public void PopCurrentExtractionContext()
    {
        _extractionContext.Pop();
    }

    public void SetQuestionnaireItem(Questionnaire.ItemComponent item)
    {
        _questionnaireItems.Push(item);
    }

    public void PopQuestionnaireItem()
    {
        _questionnaireItems.Pop();
    }

    public void SetQuestionnaireResponseItem(QuestionnaireResponse.ItemComponent item)
    {
        var context = ResolveVariables();
        _questionnaireResponseItems.Push((item, context));
    }

    public void PopQuestionnaireResponseItem()
    {
        _questionnaireResponseItems.Pop();
    }

    public void AddContext(string name, ContextValue value)
    {
        var currentContext = _questionnaireResponseItems.TryPeek(out var currentResponseItem)
            ? currentResponseItem.Context
            : _globalContext;
        if (currentContext is not null)
        {
            currentContext.Add(name, value);
        }
    }

    public void RemoveContext(string name)
    {
        var currentContext = _questionnaireResponseItems.TryPeek(out var currentResponseItem)
            ? currentResponseItem.Context
            : _globalContext;
        if (currentContext is not null)
        {
            currentContext.Remove(name);
        }
    }

    private Dictionary<string, ContextValue> ResolveVariables()
    {
        Dictionary<string, ContextValue> context = _questionnaireResponseItems.TryPeek(out var oldResponseItem)
            ? new(oldResponseItem.Context)
            : new(_globalContext);

        var variableResults = QuestionnaireItem.VariableExpressionResult(this);

        foreach (var result in variableResults)
        {
            if (!string.IsNullOrEmpty(result.Name))
            {
                context.Add(result.Name, new(result.Result, result.Name));
            }
        }

        return context;
    }
}
