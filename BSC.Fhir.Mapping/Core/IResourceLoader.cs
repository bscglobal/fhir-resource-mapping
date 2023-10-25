using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core;

public interface IResourceLoader
{
    Task<IDictionary<string, IReadOnlyCollection<Resource>>> GetResourcesAsync(
        IReadOnlyCollection<string> urls,
        CancellationToken cancellationToken = default
    );
}
