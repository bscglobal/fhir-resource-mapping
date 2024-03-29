using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping;

public class Populator : IPopulator
{
    private readonly INumericIdProvider _idProvider;
    private readonly IResourceLoader _resourceLoader;
    private readonly ILogger<Populator> _logger;
    private readonly IScopeTreeCreator _scopeTreeCreator;

    public Populator(
        INumericIdProvider idProvider,
        IResourceLoader resourceLoader,
        ILogger<Populator> logger,
        IScopeTreeCreator scopeTreeCreator
    )
    {
        _idProvider = idProvider;
        _resourceLoader = resourceLoader;
        _logger = logger;
        _scopeTreeCreator = scopeTreeCreator;
    }

    public async Task<QuestionnaireResponse> PopulateAsync(
        Questionnaire questionnaire,
        IDictionary<string, Resource> launchContext,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Populating QuestionnaireResponse from Questionnaire ({Name})", questionnaire.Title);
        var response = new QuestionnaireResponse();

        var rootScope = await _scopeTreeCreator.CreateScopeTreeAsync(
            questionnaire,
            response,
            launchContext,
            ResolvingContext.Population,
            cancellationToken
        );

        // _logger.LogDebug(TreeDebugging.PrintTree(rootScope));

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
            scope.Context.FirstOrDefault(ctx => ctx.Type == QuestionnaireContextType.InitialExpression)
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
                    return initialExpression
                        .Value.Select(result => new QuestionnaireResponse.AnswerComponent()
                        {
                            Value = result.AsExpectedType(
                                scope.Item.Type ?? Questionnaire.QuestionnaireItemType.Text,
                                initialExpression.SourceResourceType
                            )
                        })
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
                                        initialExpression.SourceResourceType
                                    )
                                }
                            }
                    };
                }
            }
        }
        else if (scope.Item.Initial.Count > 0)
        {
            _logger.LogDebug(
                "Setting QuestionnaireResponse Answer from initial field for LinkId {LinkId}",
                scope.Item.LinkId
            );
            return scope
                .Item.Initial.Select(initial => new QuestionnaireResponse.AnswerComponent() { Value = initial.Value })
                .ToList();
        }

        return null;
    }

    private QuestionnaireResponse ConstructResponse(Scope rootScope)
    {
        var response = new QuestionnaireResponse();

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
                var responseItem = new QuestionnaireResponse.ItemComponent
                {
                    LinkId = scope.ResponseItem.LinkId,
                    Answer = scope.ResponseItem.Answer
                };
                list.Add(responseItem);
                responseItem.Item.AddRange(childItems);
            }
            else
            {
                list.AddRange(childItems);
            }
        }

        return list;
    }
}
