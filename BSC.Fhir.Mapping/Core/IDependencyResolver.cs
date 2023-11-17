using BSC.Fhir.Mapping.Expressions;

namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IDependencyResolver
{
    Task<Scope?> ParseQuestionnaireAsync(CancellationToken cancellationToken = default);
}
