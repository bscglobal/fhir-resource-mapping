using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

public class ContextValue
{
    public Base[] Value { get; set; }
    public Type ValueType { get; set; }
    public string? Name { get; set; }

    public ContextValue(Base[] value, Type valueType, string? name = null)
    {
        Value = value;
        ValueType = valueType;
        Name = name;
    }

    public ContextValue(Base value, Type valueType, string? name = null)
    {
        Value = new[] { value };
        ValueType = valueType;
        Name = name;
    }
}

public class MappingContext : IDictionary<string, ContextValue>
{
    private readonly Dictionary<string, ContextValue> _namedExpressions = new();
    private readonly Stack<ContextValue> _context = new();

    public Base? CurrentContext => _context.TryPeek(out var context) ? context.Value.First() : null;
    public Questionnaire? Questionnaire { get; set; }

    public ContextValue this[string key]
    {
        get => _namedExpressions[key];
        set => _namedExpressions[key] = value;
    }

    public void SetCurrentContext(Base context)
    {
        _context.Push(new(context, context.GetType()));
    }

    public void RemoveContext()
    {
        _context.Pop();
    }

    public ICollection<string> Keys => _namedExpressions.Keys;

    public ICollection<ContextValue> Values => _namedExpressions.Values;

    public int Count => ((ICollection<KeyValuePair<string, ContextValue>>)_namedExpressions).Count;

    public bool IsReadOnly => ((ICollection<KeyValuePair<string, ContextValue>>)_namedExpressions).IsReadOnly;

    public void Add(string key, ContextValue value)
    {
        _namedExpressions.Add(key, value);
    }

    public void Add(KeyValuePair<string, ContextValue> item)
    {
        ((ICollection<KeyValuePair<string, ContextValue>>)_namedExpressions).Add(item);
    }

    public void Clear()
    {
        _namedExpressions.Clear();
    }

    public bool Contains(KeyValuePair<string, ContextValue> item)
    {
        return ((ICollection<KeyValuePair<string, ContextValue>>)_namedExpressions).Contains(item);
    }

    public bool ContainsKey(string key)
    {
        return _namedExpressions.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, ContextValue>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, ContextValue>>)_namedExpressions).CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<string, ContextValue>> GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<string, ContextValue>>)_namedExpressions).GetEnumerator();
    }

    public bool Remove(string key)
    {
        return _namedExpressions.Remove(key);
    }

    public bool Remove(KeyValuePair<string, ContextValue> item)
    {
        return ((ICollection<KeyValuePair<string, ContextValue>>)_namedExpressions).Remove(item);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out ContextValue value)
    {
        return _namedExpressions.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _namedExpressions.GetEnumerator();
    }
}
