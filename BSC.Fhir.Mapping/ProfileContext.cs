using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

public class ProfileContext
{
    public int PoundIndex { get; set; }
    public StructureDefinition Profile { get; set; }

    public ProfileContext(int poundIndex, StructureDefinition profile)
    {
        PoundIndex = poundIndex;
        Profile = profile;
    }
}
