using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core;

public class ContextResult
{
    public Resource[] Resources { get; init; } = Array.Empty<Resource>();
    public Func<Resource?> CreateNewResource { get; init; } = () => null;
}
