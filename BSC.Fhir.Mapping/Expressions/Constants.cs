using BSC.Fhir.Mapping.Core.Expressions;

namespace BSC.Fhir.Mapping.Expressions;

public static class Constants
{
    public const string LAUNCH_CONTEXT =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-launchContext";
    public const string POPULATION_CONTEXT =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext";
    public const string INITIAL_EXPRESSION =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression";
    public const string VARIABLE_EXPRESSION = "http://hl7.org/fhir/StructureDefinition/variable";
    public const string EXTRACTION_CONTEXT =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext";
    public const string CALCULATED_EXPRESSION =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition-sdc-questionnaire-calculatedExpression.html";
    public const string HIDDEN = "http://hl7.org/fhir/StructureDefinition/questionnaire-hidden";
    public const string EXTRACTION_CONTEXT_ID = "extractionContextId";

    public const string FHIR_QUERY_MIME = "application/x-fhir-query";
    public const string FHIRPATH_MIME = "text/fhirpath";
    public static readonly QuestionnaireContextType[] POPULATION_ONLY_CONTEXTS = new[]
    {
        QuestionnaireContextType.PopulationContext,
        QuestionnaireContextType.InitialExpression
    };
    public static readonly QuestionnaireContextType[] EXTRACTION_ONLY_CONTEXTS = new[]
    {
        QuestionnaireContextType.ExtractionContext,
        QuestionnaireContextType.ExtractionContextId
    };
    public static readonly string[] POPULATION_DEPENDANT_CONTEXT = new[] { "%resource", "%context" };
}
