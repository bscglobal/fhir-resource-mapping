using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

public class MappingContext : IDictionary<string, Base[]>
{
    private readonly Dictionary<string, Base[]> _namedExpressions = new();
    private readonly Stack<Base> _context = new();

    public Base? CurrentContext => _context.TryPeek(out var context) ? context : null;
    public Questionnaire? Questionnaire { get; set; }

    public Base[] this[string key]
    {
        get => _namedExpressions[key];
        set => _namedExpressions[key] = value;
    }

    public void SetCurrentContext(Base context)
    {
        _context.Push(context);
    }

    public void RemoveContext()
    {
        _context.Pop();
    }

    public ICollection<string> Keys => _namedExpressions.Keys;

    public ICollection<Base[]> Values => _namedExpressions.Values;

    public int Count => ((ICollection<KeyValuePair<string, Base[]>>)_namedExpressions).Count;

    public bool IsReadOnly =>
        ((ICollection<KeyValuePair<string, Base[]>>)_namedExpressions).IsReadOnly;

    public void Add(string key, Base[] value)
    {
        _namedExpressions.Add(key, value);
    }

    public void Add(KeyValuePair<string, Base[]> item)
    {
        ((ICollection<KeyValuePair<string, Base[]>>)_namedExpressions).Add(item);
    }

    public void Clear()
    {
        _namedExpressions.Clear();
    }

    public bool Contains(KeyValuePair<string, Base[]> item)
    {
        return ((ICollection<KeyValuePair<string, Base[]>>)_namedExpressions).Contains(item);
    }

    public bool ContainsKey(string key)
    {
        return _namedExpressions.ContainsKey(key);
    }

    public void CopyTo(KeyValuePair<string, Base[]>[] array, int arrayIndex)
    {
        ((ICollection<KeyValuePair<string, Base[]>>)_namedExpressions).CopyTo(array, arrayIndex);
    }

    public IEnumerator<KeyValuePair<string, Base[]>> GetEnumerator()
    {
        return ((IEnumerable<KeyValuePair<string, Base[]>>)_namedExpressions).GetEnumerator();
    }

    public bool Remove(string key)
    {
        return _namedExpressions.Remove(key);
    }

    public bool Remove(KeyValuePair<string, Base[]> item)
    {
        return ((ICollection<KeyValuePair<string, Base[]>>)_namedExpressions).Remove(item);
    }

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Base[] value)
    {
        return _namedExpressions.TryGetValue(key, out value);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _namedExpressions.GetEnumerator();
    }
}
