namespace BSC.Fhir.Mapping.Expressions;

public class NumericIdProvider
{
    private int _currentId = 0;

    public int GetId() => _currentId++;
}
