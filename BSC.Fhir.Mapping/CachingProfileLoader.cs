using BSC.Fhir.Mapping.Core;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

internal class CachingProfileLoader : IProfileLoader
{
    private readonly Dictionary<string, StructureDefinition?> _loadedProfiles = new();
    private readonly IProfileLoader? _profileLoader;

    public CachingProfileLoader(IProfileLoader? profileLoader = null)
    {
        _profileLoader = profileLoader;
    }

    public async Task<StructureDefinition?> LoadProfileAsync(
        Canonical url,
        CancellationToken cancellationToken = default
    )
    {
        if (_profileLoader is null)
        {
            // TODO: Log something.
            return null;
        }

        var key = url.ToString();
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        if (_loadedProfiles.TryGetValue(key, out var profile))
        {
            return profile;
        }

        var loadedProfile = await _profileLoader.LoadProfileAsync(url, cancellationToken);

        _loadedProfiles[key] = loadedProfile;

        return loadedProfile;
    }
}
