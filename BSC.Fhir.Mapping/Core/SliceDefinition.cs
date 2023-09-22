using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core;

public class SliceDefinition
{
    public string Name { get; set; }
    public List<DataType> Fixed { get; set; } = new();
    public List<DataType> Pattern { get; set; } = new();

    public SliceDefinition(string name)
    {
        Name = name;
    }
}
