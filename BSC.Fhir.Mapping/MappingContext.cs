using System.Collections;
using System.Diagnostics.CodeAnalysis;
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
    private readonly Stack<QuestionnaireResponse.ItemComponent> _questionnaireResponseItems = new();

    public Context? CurrentContext => _extractionContext.TryPeek(out var context) ? context : null;
    public Questionnaire.ItemComponent QuestionnaireItem => _questionnaireItems.Peek();
    public QuestionnaireResponse.ItemComponent QuestionnaireResponseItem => _questionnaireResponseItems.Peek();

    public Dictionary<string, ContextValue> NamedExpressions { get; } = new();

    public Questionnaire Questionnaire { get; private set; }
    public QuestionnaireResponse? QuestionnaireResponse { get; set; }

    // somethign
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
        _questionnaireResponseItems.Push(item);
    }

    public void PopQuestionnaireResponseItem()
    {
        _questionnaireResponseItems.Pop();
    }
}
