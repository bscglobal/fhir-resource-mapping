using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core;

public interface IProfileLoader
{
    Task<StructureDefinition?> LoadProfileAsync(Canonical url, CancellationToken cancellationToken = default);
}
