using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core;

public interface IProfileLoader
{
    StructureDefinition LoadProfile(Canonical url);
}
