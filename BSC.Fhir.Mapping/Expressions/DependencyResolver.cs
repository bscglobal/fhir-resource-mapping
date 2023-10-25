using System.Text.RegularExpressions;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class DependencyResolver
{
    private readonly NumericIdProvider _idProvider = new();
    private readonly ResolverState _state;
    private readonly Questionnaire _questionnaire;
    private readonly QuestionnaireResponse? _questionnaireResponse;
    private readonly IResourceLoader _resourceLoader;

    public DependencyResolver(
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse,
        IReadOnlyCollection<LaunchContext> launchContext,
        IResourceLoader resourceLoader
    )
    {
        _questionnaire = questionnaire;
        _questionnaireResponse = questionnaireResponse;
        _state = new(questionnaire, questionnaireResponse);
        _resourceLoader = resourceLoader;

        AddLaunchContextToScope(launchContext);
    }

    private void AddLaunchContextToScope(IReadOnlyCollection<LaunchContext> launchContext)
    {
        _state.CurrentScope.Context.AddRange(launchContext);
    }

    public async Task<Scope<IReadOnlyCollection<Base>>?> ParseQuestionnaireAsync(
        CancellationToken cancellationToken = default
    )
    {
        var rootExtensions = _questionnaire.AllExtensions();

        ParseExtensions(rootExtensions.ToArray());
        ParseQuestionnaireItems(_questionnaire.Item);

        if (_state.CurrentScope.Parent is not null)
        {
            Console.WriteLine("Error: not in global scope");
            return null;
        }
        else
        {
            Console.WriteLine("Debug: in global scope");
        }

        var graphResult = CreateDependencyGraph(_state.CurrentScope);
        if (graphResult is not null)
        {
            // _logger.LogError(
            //     $"Circular dependency detected for {graphResult.Expression.Expression_} in {graphResult.LinkId}"
            // );
            return null;
        }

        await ResolveDependenciesAsync(_state.CurrentScope, cancellationToken);

        return _state.CurrentScope;
    }

    private void ParseQuestionnaireItems(IReadOnlyCollection<Questionnaire.ItemComponent> items)
    {
        foreach (var item in items)
        {
            _state.PushScope(item);
            ParseQuestionnaireItem(item);
            _state.PopScope();
        }
    }

    private void ParseQuestionnaireItem(Questionnaire.ItemComponent item)
    {
        if (string.IsNullOrEmpty(item.LinkId))
        {
            // _logger.LogWarning("Questionnaire item does not have LinkId, skipping...");
            return;
        }

        var extensions = item.AllExtensions();
        ParseExtensions(extensions.ToArray(), item.LinkId);

        ParseQuestionnaireItems(item.Item);
    }

    private void ParseExtensions(IReadOnlyCollection<Extension> extensions, string? currentLinkId = null)
    {
        var queries = extensions.Select(extension => ParseExtension(extension)).OfType<QuestionnaireExpression>();
        _state.CurrentScope.Context.AddRange(queries);
    }

    private QuestionnaireExpression? ParseExtension(Extension extension)
    {
        return extension.Url switch
        {
            Constants.POPULATION_CONTEXT
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME, Constants.FHIR_QUERY_MIME },
                    QuestionnaireContextType.PopulationContext
                ),
            Constants.EXTRACTION_CONTEXT
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME, Constants.FHIR_QUERY_MIME },
                    QuestionnaireContextType.ExtractionContext
                ),
            Constants.INITIAL_EXPRESSION
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME },
                    QuestionnaireContextType.InitialExpression
                ),
            _ => null
        };
    }

    private QuestionnaireExpression? ParseExpressionExtension(
        Extension extension,
        string[] supportedLanguages,
        QuestionnaireContextType extensionType
    )
    {
        var errorMessage = (string message) =>
            $"{message} for {extensionType} in Questionnaire.Item {_state.CurrentItem?.LinkId ?? "root"}. Skipping resolution for this extension...";

        if (extension.Value is not Expression expression)
        {
            var type = ModelInfo.GetFhirTypeNameForType(extension.Value.GetType());
            // _logger.LogWarning(errorMessage($"Unexpected type {type}. Expected Expression"));
            return null;
        }

        if (!supportedLanguages.Contains(expression.Language))
        {
            // _logger.LogWarning(errorMessage($"Unsupported expression language {expression.Language}"));
            return null;
        }

        if (string.IsNullOrEmpty(expression.Expression_))
        {
            // _logger.LogWarning(errorMessage("Empty expression"));
            return null;
        }

        var query = CreateExpression(expression, extensionType);
        expression.AddExtension("ExpressionId", new Id { Value = query.Id.ToString() });

        return query;
    }

    private QuestionnaireExpression? CreateDependencyGraph(Scope<BaseList> scope)
    {
        for (var i = 0; i < scope.Context.Count; i++)
        {
            var context = scope.Context[i];
            if (context is not QuestionnaireExpression query)
            {
                continue;
            }

            QuestionnaireExpression? result = null;
            if (query.ExpressionLanguage == Constants.FHIR_QUERY_MIME)
            {
                result = CalculateFhirQueryDependencies(scope, query);
            }
            else if (query.ExpressionLanguage == Constants.FHIRPATH_MIME)
            {
                result = CalculateFhirPathDependencies(scope, query);
            }

            if (result is not null)
            {
                return result;
            }
        }

        foreach (var child in scope.Children)
        {
            if (CreateDependencyGraph(child) is QuestionnaireExpression expr)
            {
                return expr;
            }
        }

        return null;
    }

    private QuestionnaireExpression? CalculateFhirQueryDependencies(
        Scope<BaseList> scope,
        QuestionnaireExpression query
    )
    {
        var expression = query.Expression;
        var embeddedFhirpathRegex = @"\{\{(.*)\}\}";
        var matches = Regex.Matches(expression, embeddedFhirpathRegex);

        foreach (Match match in matches)
        {
            var fhirpathExpression = match.Groups.Values.FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(fhirpathExpression))
            {
                // _logger.LogWarning($"Invalid embedded query {match.Value}");
                continue;
            }

            fhirpathExpression = Regex.Replace(fhirpathExpression, "[{}]", "");

            var expr = new Expression { Language = Constants.FHIRPATH_MIME, Expression_ = fhirpathExpression };
            var embeddedQuery = CreateExpression(expr, QuestionnaireContextType.Embedded, query);
            scope.Context.Add(embeddedQuery);

            if (!query.AddDependency(embeddedQuery))
            {
                return query;
            }
        }

        return null;
    }

    private QuestionnaireExpression? CalculateFhirPathDependencies(Scope<BaseList> scope, QuestionnaireExpression query)
    {
        var fhirpathRegex = @"([^.]+(\((.+\..+)+\)))?([^.]+)?";
        var expression = query.Expression;

        var parts = Regex.Matches(expression, fhirpathRegex).Select(match => match.Value);
        var variables = parts.Where(part => part.StartsWith('%'));

        foreach (var variable in variables)
        {
            if (Constants.POPULATION_DEPENDANT_CONTEXT.Contains(variable))
            {
                query.MakeResponseDependant();
                continue;
            }

            var varName = variable[1..];
            var dep = scope.GetContext(varName);

            if (dep is not null)
            {
                if (!query.AddDependency(dep))
                {
                    return query;
                }
            }
            else
            {
                Console.WriteLine(
                    "Error: Could not find dependency {0} in expression {1} for LinkId {2}",
                    varName,
                    expression,
                    query.QuestionnaireItem?.LinkId ?? "root"
                );
            }
        }

        return null;
    }

    private async Task ResolveDependenciesAsync(Scope<BaseList> scope, CancellationToken cancellationToken = default)
    {
        var oldLength = 0;
        while (
            scope.Context
                .Where(
                    context =>
                        context is IQuestionnaireExpression<BaseList> expr
                        && !expr.Resolved()
                        && expr.DependenciesResolved()
                )
                .ToArray()
                is IQuestionnaireExpression<BaseList>[] unresolved
            && unresolved.Length > 0
        )
        {
            if (unresolved.Length == oldLength)
            {
                Console.WriteLine("Error: could not resolve all dependencies");
                return;
            }

            oldLength = unresolved.Length;

            ResolveFhirPathExpression(scope, unresolved);
            await ResolveFhirQueriesAsync(scope, unresolved, cancellationToken);
        }

        foreach (var child in scope.Children)
        {
            await ResolveDependenciesAsync(child, cancellationToken);
        }
    }

    private void ResolveFhirPathExpression(
        Scope<BaseList> scope,
        IReadOnlyCollection<IQuestionnaireExpression<BaseList>> unresolvedExpressions
    )
    {
        var fhirpathQueries = unresolvedExpressions
            .Where(query => query.ExpressionLanguage == Constants.FHIRPATH_MIME && query.DependenciesResolved())
            .ToArray();

        foreach (var query in fhirpathQueries)
        {
            var fhirpathResult = FhirPathMapping.EvaluateExpr(query.Expression, scope)?.Result;

            if (fhirpathResult is null || fhirpathResult.Length > 0)
            {
                // _logger.LogWarning($"Found no results for {query.Expression.Expression_}");
                continue;
            }

            query.SetValue(fhirpathResult);
            var fhirqueryDependants = query.Dependants
                .Where(dep => dep.ExpressionLanguage == Constants.FHIR_QUERY_MIME)
                .ToArray();

            if (fhirpathQueries.Length > 0 && fhirpathResult.Length > 1)
            {
                // _logger.LogWarning("Embedded {Query} has more than one result", query.Expression.Expression_);
            }

            var escapedQuery = Regex.Escape(query.Expression);
            foreach (var dep in fhirqueryDependants)
            {
                dep.ReplaceExpression(
                    Regex.Replace(
                        dep.Expression,
                        "\\{\\{" + escapedQuery + "\\}\\}",
                        fhirpathResult.First().ToString() ?? ""
                    )
                );
            }
        }
    }

    private async Task ResolveFhirQueriesAsync(
        Scope<BaseList> scope,
        IReadOnlyCollection<IQuestionnaireExpression<BaseList>> unresolvedExpressions,
        CancellationToken cancellationToken = default
    )
    {
        var fhirQueries = unresolvedExpressions
            .Where(
                expr =>
                    !expr.Resolved()
                    && expr.ExpressionLanguage == Constants.FHIR_QUERY_MIME
                    && expr.DependenciesResolved()
            )
            .ToArray();

        var urls = fhirQueries.Select(query => query.Expression).Distinct().ToArray();
        // _logger.LogDebug($"Executing {urls.Length} Fhir Queries");
        // _logger.LogDebug(JsonSerializer.Serialize(urls, new JsonSerializerOptions() { WriteIndented = true }));

        var resourceResult = await _resourceLoader.GetResourcesAsync(urls, cancellationToken);

        // _logger.LogDebug($"Result length: {fhirQueryResult.Entry.Count}");

        foreach (var query in fhirQueries)
        {
            // var queryResult = fhirQueryResult.Entry
            //     .Select(entry => entry.Resource)
            //     .OfType<Bundle>()
            //     .Where(
            //         bundle =>
            //             bundle.Link.Any(
            //                 link => link.Relation == "self" && link.Url.EndsWith(query.Expression)
            //             )
            //     )
            //     .FirstOrDefault()
            //     ?.Entry.Select(entry => entry.Resource)
            //     .ToArray();
            if (resourceResult.TryGetValue(query.Expression, out var queryResult))
            {
                // _logger.LogDebug("Found result for query {Expression}", query.Expression.Expression_);

                query.SetValue(queryResult);
            }
            else
            {
                // _logger.LogDebug("Did not find result for query {Expression}", query.Expression.Expression_);
            }
        }
    }

    private QuestionnaireExpression CreateExpression(
        Expression expr,
        QuestionnaireContextType queryType,
        QuestionnaireExpression? from = null
    ) =>
        new(
            _idProvider.GetId(),
            expr,
            queryType,
            from is not null ? from.QuestionnaireItem : _state.CurrentItem,
            from is not null ? from.QuestionnaireResponseItem : _state.CurrentResponseItem
        );
}
