using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core;

public interface IPopulator
{
    Task<QuestionnaireResponse> PopulateAsync(
        Questionnaire questionnaire,
        IDictionary<string, Resource> launchContext,
        CancellationToken cancellationToken = default
    );
}
