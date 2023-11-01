using System.Reflection;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class ExtractionContext
{
    public Base Value { get; }
    public HashSet<PropertyInfo> DirtyFields { get; } = new();

    public ExtractionContext(Base value)
    {
        Value = value;
    }
}
