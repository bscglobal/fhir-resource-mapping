using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data;

public partial class GeneralNote
{
    public static Dictionary<string, IReadOnlyCollection<Resource>> ResourceLoaderResponse(string compositionId)
    {
        var composition = new Composition { Id = compositionId };

        return new() { { $"Composition?_id={composition.Id}", new[] { composition } } };
    }
}
