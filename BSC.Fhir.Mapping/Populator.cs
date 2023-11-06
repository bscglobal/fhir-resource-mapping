using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Logging;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping;

public class Populator : IPopulator
{
    private readonly INumericIdProvider _idProvider;
    private readonly IResourceLoader _resourceLoader;
    private readonly ILogger _logger;

    public Populator(INumericIdProvider idProvider, IResourceLoader resourceLoader, ILogger? logger = null)
    {
        _idProvider = idProvider;
        _resourceLoader = resourceLoader;
        _logger = logger ?? FhirMappingLogging.GetLogger();
    }

    public async Task<QuestionnaireResponse> PopulateAsync(
        Questionnaire questionnaire,
        IDictionary<string, Resource> launchContext,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Populating QuestionnaireResponse from Questionnaire ({Name})", questionnaire.Title);
        var response = new QuestionnaireResponse();

        var resolver = new DependencyResolver(
            _idProvider,
            questionnaire,
            response,
            launchContext,
            _resourceLoader,
            ResolvingContext.Population,
            _logger
        );
        var rootScope = await resolver.ParseQuestionnaireAsync(cancellationToken);

        if (rootScope is null)
        {
            throw new InvalidOperationException("Could not populate QuestionnaireResponse");
        }

        CreateQuestionnaireResponseItems(rootScope.Children);

        return ConstructResponse(rootScope);
    }

    private void CreateQuestionnaireResponseItems(IReadOnlyCollection<Scope> scopes)
    {
        foreach (var scope in scopes)
        {
            _logger.LogDebug("CreatingQuestionnaireResponseItems for {LinkId}", scope.Item?.LinkId ?? "root");

            GenerateQuestionnaireResponseItem(scope);
        }
    }

    private void GenerateQuestionnaireResponseItem(Scope scope)
    {
        if (scope.Item is null)
        {
            _logger.LogError("Scope QuestionnaireItem is null on level {0}", scope.Level);
            return;
        }

        if (scope.ResponseItem is null)
        {
            _logger.LogWarning("Scope does not have ResponseItem. LinkId: {LinkId}", scope.Item.LinkId);
            return;
        }

        scope.ResponseItem.Answer = CreateQuestionnaireResponseItemAnswers(scope);

        CreateQuestionnaireResponseItems(scope.Children);
    }

    private List<QuestionnaireResponse.AnswerComponent>? CreateQuestionnaireResponseItemAnswers(Scope scope)
    {
        if (scope.Item is null)
        {
            _logger.LogError("Scope QuestionnaireItem is null on level {0}", scope.Level);
            return null;
        }

        if (scope.Children.Count > 0)
        {
            return null;
        }

        var initialExpression =
            scope.Context.FirstOrDefault(ctx => ctx.Type == Core.Expressions.QuestionnaireContextType.InitialExpression)
            as FhirPathExpression;
        if (!(scope.Item.Initial.Count == 0 || initialExpression is null))
        {
            throw new InvalidOperationException(
                "QuestionnaireItem is not allowed to have both intial.value and initial expression. See rule at http://build.fhir.org/ig/HL7/sdc/expressions.html#initialExpression"
            );
        }

        if (initialExpression is not null)
        {
            if (initialExpression.Value is null)
            {
                _logger.LogDebug("Could not find a value for {0}", initialExpression.Expression);
            }
            else
            {
                _logger.LogDebug(
                    "Setting QuestionnaireResponse Answer from initial expression for LinkId {LinkId}",
                    scope.Item.LinkId
                );
                if (scope.Item.Repeats ?? false)
                {
                    return initialExpression.Value
                        .Select(
                            result =>
                                new QuestionnaireResponse.AnswerComponent()
                                {
                                    Value = result.AsExpectedType(
                                        scope.Item.Type ?? Questionnaire.QuestionnaireItemType.Text,
                                        initialExpression.SourceResource is Resource resource
                                            ? resource.GetType()
                                            : null
                                    )
                                }
                        )
                        .ToList();
                }
                else if (initialExpression.Value.Count > 1)
                {
                    _logger.LogWarning(
                        "expression {0} resolved to more than one answer. LinkId: {1}",
                        initialExpression.Expression,
                        scope.Item.LinkId
                    );
                }
                else
                {
                    return initialExpression.Value.SingleOrDefault() switch
                    {
                        null => null,
                        var x
                            => new()
                            {
                                new()
                                {
                                    Value = x.AsExpectedType(
                                        scope.Item.Type ?? Questionnaire.QuestionnaireItemType.Text,
                                        initialExpression.SourceResource is Resource resource
                                            ? resource.GetType()
                                            : null
                                    )
                                }
                            }
                    };
                }
            }
        }
        else if (scope.Item.Initial.Count > 0)
        {
            return scope.Item.Initial
                .Select(initial => new QuestionnaireResponse.AnswerComponent() { Value = initial.Value })
                .ToList();
        }

        return null;
    }

    private QuestionnaireResponse ConstructResponse(Scope rootScope)
    {
        var response = rootScope.QuestionnaireResponse;

        if (response is null)
        {
            throw new ArgumentException("rootScope.QuestionnaireResponse is null");
        }

        response.Item = ConstructResponseItems(rootScope.Children);

        return response;
    }

    private List<QuestionnaireResponse.ItemComponent> ConstructResponseItems(IReadOnlyCollection<Scope> scopes)
    {
        var list = new List<QuestionnaireResponse.ItemComponent>(scopes.Count);

        foreach (var scope in scopes)
        {
            var childItems = ConstructResponseItems(scope.Children);

            if (scope.ResponseItem is not null)
            {
                list.Add(scope.ResponseItem);
                scope.ResponseItem.Item.AddRange(childItems);
            }
            else
            {
                list.AddRange(childItems);
            }
        }

        return list;
    }
}
