using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping.Expressions;

public class ScopeTreeCreator : IScopeTreeCreator
{
    private readonly IServiceProvider _serviceProvider;

    public ScopeTreeCreator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Scope> CreateScopeTreeAsync(
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        ResolvingContext context,
        CancellationToken cancellationToken = default
    )
    {
        var parser = new QuestionnaireParser(
            _serviceProvider.GetRequiredService<INumericIdProvider>(),
            questionnaire,
            questionnaireResponse,
            launchContext,
            _serviceProvider.GetRequiredService<IResourceLoader>(),
            context,
            _serviceProvider.GetRequiredService<FhirPathMapping>(),
            _serviceProvider.GetRequiredService<ILogger<QuestionnaireParser>>()
        );

        return await parser.ParseQuestionnaireAsync(cancellationToken);
    }
}
