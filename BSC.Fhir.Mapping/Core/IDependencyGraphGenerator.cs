using BSC.Fhir.Mapping.Expressions;

namespace BSC.Fhir.Mapping.Core.Expressions;

public interface IDependencyGraphGenerator
{
    void Generate(Scope rootScope);
}
