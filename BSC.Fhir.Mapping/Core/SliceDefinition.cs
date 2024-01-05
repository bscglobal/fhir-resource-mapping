using System.Reflection;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core;

public class SliceDefinition
{
    public class SliceFilter
    {
        public PropertyInfo PropertyInfo { get; set; }
        public Element Value { get; set; }

        public SliceFilter(PropertyInfo propertyInfo, DataType value)
        {
            PropertyInfo = propertyInfo;
            Value = value;
        }
    }

    public string Name { get; set; }
    public List<SliceFilter> Fixed { get; set; } = new();
    public List<SliceFilter> Pattern { get; set; } = new();

    public SliceDefinition(string name)
    {
        Name = name;
    }
}
