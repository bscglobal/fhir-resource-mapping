namespace BSC.Fhir.Mapping.Core;

public interface IClonable<T>
    where T : IClonable<T>
{
    T Clone(dynamic? replacementField = null);
    T? ClonedFrom { get; }
}
