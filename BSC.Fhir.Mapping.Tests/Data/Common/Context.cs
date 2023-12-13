namespace BSC.Fhir.Mapping.Tests.Data.Common;

public record LaunchContext(string Name, string Type, string Display);

public record FhirExpression(string Expression, string? Name, string? Language);

public record FhirPathExpression(string Expression, string? Name = null)
    : FhirExpression(Expression, Name, "text/fhirpath");

public record FhirQueryExpression(string Expression, string? Name = null)
    : FhirExpression(Expression, Name, "application/x-fhir-query");
