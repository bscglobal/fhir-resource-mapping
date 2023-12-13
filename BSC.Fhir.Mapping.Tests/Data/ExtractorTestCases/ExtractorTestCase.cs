using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Data.ExtractorTestCases;

public abstract record ExtractorTestCase(
    Questionnaire Questionnaire,
    QuestionnaireResponse QuestionnaireResponse,
    Dictionary<string, IReadOnlyCollection<Resource>> ResourceLoaderResponse,
    Dictionary<string, StructureDefinition> Profiles,
    Dictionary<string, Resource> LaunchContext,
    Bundle ExpectedBundle
);
