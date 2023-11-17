using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IScopeTreeCreator
{
    Task<Scope> CreateScopeTreeAsync(
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        ResolvingContext context,
        CancellationToken cancellationToken = default
    );
}
