using BSC.Fhir.Mapping.Core;

namespace BSC.Fhir.Mapping.Expressions;

public class NumericIdProvider : INumericIdProvider
{
    private int _currentId = 0;

    public int GetId() => _currentId++;
}
