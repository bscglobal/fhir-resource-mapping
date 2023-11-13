using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IDependencyResolverFactory
{
    DependencyResolver CreateDependencyResolver(
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        ResolvingContext context
    );
}
