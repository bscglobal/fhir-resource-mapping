using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core;

public interface IExtractor
{
    Task<Bundle> ExtractAsync(
        Questionnaire questionnaire,
        QuestionnaireResponse questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        CancellationToken cancellationToken = default
    );
}
