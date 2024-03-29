using System.Text.Json;
using System.Text.RegularExpressions;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class QuestionnaireParser
{
    private readonly INumericIdProvider _idProvider;
    private readonly ScopeTree _scopeTree;
    private readonly Questionnaire _questionnaire;
    private readonly QuestionnaireResponse _questionnaireResponse;
    private readonly IResourceLoader _resourceLoader;
    private readonly ResolvingContext _resolvingContext;
    private readonly QuestionnaireContextType[] _notAllowedContextTypes;
    private readonly ILogger<QuestionnaireParser> _logger;
    private readonly FhirPathMapping _fhirPathEvaluator;
    private readonly IDependencyGraphGenerator _dependencyGraphGenerator;

    private readonly Dictionary<string, IReadOnlyCollection<Resource>> _queryResults = new();

    public QuestionnaireParser(
        INumericIdProvider idProvider,
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        IResourceLoader resourceLoader,
        ResolvingContext resolvingContext,
        FhirPathMapping fhirPathEvaluator,
        ILogger<QuestionnaireParser> logger,
        IDependencyGraphGenerator dependencyGraphGenerator
    )
    {
        _questionnaire = questionnaire;
        _questionnaireResponse = questionnaireResponse ?? new();
        _scopeTree = new(questionnaire, _questionnaireResponse, idProvider);
        _resourceLoader = resourceLoader;
        _resolvingContext = resolvingContext;
        _idProvider = idProvider;
        _notAllowedContextTypes = resolvingContext switch
        {
            ResolvingContext.Population => Constants.EXTRACTION_ONLY_CONTEXTS,
            ResolvingContext.Extraction => Constants.POPULATION_ONLY_CONTEXTS,
            _ => Array.Empty<QuestionnaireContextType>()
        };
        _logger = logger;

        AddLaunchContextToScope(launchContext);
        _fhirPathEvaluator = fhirPathEvaluator;
        _dependencyGraphGenerator = dependencyGraphGenerator;
    }

    private void AddLaunchContextToScope(IDictionary<string, Resource> launchContext)
    {
        var scopedLaunchContext = launchContext.Select(kv => new QuestionnaireContext(
            _idProvider.GetId(),
            kv.Key,
            kv.Value,
            _scopeTree.CurrentScope,
            QuestionnaireContextType.LaunchContext
        ));

        _scopeTree.CurrentScope.Context.AddRange(scopedLaunchContext);
    }

    public async Task<Scope> ParseQuestionnaireAsync(CancellationToken cancellationToken = default)
    {
        var rootExtensions = _questionnaire.AllExtensions();

        ParseExtensions(rootExtensions.ToArray());
        ParseQuestionnaireItems(_questionnaire.Item, _questionnaireResponse.Item);

        if (_scopeTree.CurrentScope.Parent is not null)
        {
            throw new InvalidOperationException(
                "Current Scope after parsing has a Parent, meaning it is not the root Scope."
            );
        }

        _dependencyGraphGenerator.Generate(_scopeTree.CurrentScope);

        if (!(await ResolveDependenciesAsync(cancellationToken)))
        {
            throw new InvalidOperationException("Could not resolve all dependencies");
        }

        return _scopeTree.CurrentScope;
    }

    private void ParseQuestionnaireItems(
        IReadOnlyCollection<Questionnaire.ItemComponent> items,
        List<QuestionnaireResponse.ItemComponent> responseItems
    )
    {
        var sortedItems = items.OrderBy(item => item.LinkId);
        var sortedResponseItems = responseItems
            .Where(responseItem => sortedItems.Any(item => item.LinkId == responseItem.LinkId))
            .OrderBy(responseItem => responseItem.LinkId);
        var responseItemQueue = new Queue<QuestionnaireResponse.ItemComponent>(sortedResponseItems);
        foreach (var item in sortedItems)
        {
            var responseItemCount = 0;
            QuestionnaireResponse.ItemComponent? responseItem;
            while (responseItemQueue.TryPeek(out responseItem) && responseItem.LinkId == item.LinkId)
            {
                responseItemCount++;
                responseItem = responseItemQueue.Dequeue();

                _scopeTree.PushScope(item, responseItem);
                ParseQuestionnaireItem(item, responseItem);
                _scopeTree.PopScope();
            }

            if (responseItemCount == 0)
            {
                responseItem = new QuestionnaireResponse.ItemComponent { LinkId = item.LinkId };

                _scopeTree.PushScope(item, responseItem);
                ParseQuestionnaireItem(item, responseItem);
                _scopeTree.PopScope();
                responseItems.Add(responseItem);
            }
        }
    }

    private void ParseQuestionnaireItem(
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem
    )
    {
        if (string.IsNullOrEmpty(item.LinkId))
        {
            _logger.LogWarning("Questionnaire item does not have LinkId, skipping...");
            return;
        }

        var extensions = item.AllExtensions();
        ParseExtensions(extensions.ToArray(), item.LinkId);

        ParseQuestionnaireItems(item.Item, responseItem.Item);
    }

    private void ParseExtensions(IReadOnlyCollection<Extension> extensions, string? currentLinkId = null)
    {
        var queries = extensions
            .Select(extension => ParseExtension(extension))
            .OfType<IQuestionnaireContext<BaseList>>();
        _scopeTree.CurrentScope.Context.AddRange(queries);
    }

    private IQuestionnaireContext<BaseList>? ParseExtension(Extension extension)
    {
        return extension.Url switch
        {
            Constants.POPULATION_CONTEXT when _resolvingContext == ResolvingContext.Population
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME, Constants.FHIR_QUERY_MIME },
                    QuestionnaireContextType.PopulationContext
                ),
            Constants.EXTRACTION_CONTEXT when _resolvingContext == ResolvingContext.Extraction
                => extension.Value switch
                {
                    Expression
                        => ParseExpressionExtension(
                            extension,
                            new[] { Constants.FHIRPATH_MIME, Constants.FHIR_QUERY_MIME },
                            QuestionnaireContextType.ExtractionContext
                        ),
                    Code code => ResourceTypeExtractionContext(code),
                    _ => ParseExtensionError(extension, "Unexpected type for ExtractionContext")
                },
            Constants.INITIAL_EXPRESSION when _resolvingContext == ResolvingContext.Population
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME },
                    QuestionnaireContextType.InitialExpression
                ),
            Constants.VARIABLE_EXPRESSION
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME, Constants.FHIR_QUERY_MIME },
                    QuestionnaireContextType.VariableExpression
                ),
            Constants.CALCULATED_EXPRESSION when _resolvingContext == ResolvingContext.Extraction
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME },
                    QuestionnaireContextType.CalculatedExpression
                ),
            Constants.EXTRACTION_CONTEXT_ID when _resolvingContext == ResolvingContext.Extraction
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME },
                    QuestionnaireContextType.ExtractionContextId
                ),
            _ => null
        };
    }

    private IQuestionnaireExpression<BaseList>? ParseExpressionExtension(
        Extension extension,
        string[] supportedLanguages,
        QuestionnaireContextType extensionType
    )
    {
        var errorMessage = (string message) =>
            $"{message} for {extensionType} in Questionnaire.Item {_scopeTree.CurrentItem?.LinkId ?? "root"}. Skipping resolution for this extension...";

        if (extension.Value is not Expression expression)
        {
            var type = ModelInfo.GetFhirTypeNameForType(extension.Value.GetType());
            _logger.LogWarning(errorMessage($"Unexpected type {type}. Expected Expression"));
            return null;
        }

        if (!supportedLanguages.Contains(expression.Language))
        {
            return ParseExtensionError(extension, errorMessage($"Unsupported language {expression.Language}"));
        }

        if (string.IsNullOrEmpty(expression.Expression_))
        {
            return ParseExtensionError(extension, errorMessage("Expression is empty"));
        }

        IQuestionnaireExpression<BaseList> query;
        if (expression.Language == Constants.FHIR_QUERY_MIME)
        {
            query = CreateFhirQueryExpression(
                expression.Name,
                expression.Expression_,
                extensionType,
                _scopeTree.CurrentScope
            );
        }
        else
        {
            query = CreateFhirPathExpression(
                expression.Name,
                expression.Expression_,
                extensionType,
                _scopeTree.CurrentScope
            );
        }
        expression.AddExtension("ExpressionId", new Id { Value = query.Id.ToString() });

        return query;
    }

    private IQuestionnaireExpression<BaseList>? ParseExtensionError(Extension extension, string message)
    {
        _logger.LogError(
            "{Message} for {ExtensionUrl} in Questionnaire.Item {LinkId}. Skipping resolution for this extension...",
            message,
            extension.Url,
            _scopeTree.CurrentItem?.LinkId ?? "root"
        );
        return null;
    }

    private IQuestionnaireContext<BaseList>? ResourceTypeExtractionContext(Code code)
    {
        var resourceType = ModelInfo.GetTypeForFhirType(code.Value);
        var resource = Activator.CreateInstance(resourceType) as Resource;

        if (resource is null)
        {
            _logger.LogError("Could not create resource for type {Type}", resourceType);
            return null;
        }

        var extractionContext = new QuestionnaireContext(
            _idProvider.GetId(),
            null,
            resource,
            _scopeTree.CurrentScope,
            QuestionnaireContextType.ExtractionContext
        );

        return extractionContext;
    }

    private async Task<bool> ResolveDependenciesAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            var expressions = _scopeTree
                .CurrentScope.AllContextInSubtree()
                .OfType<IQuestionnaireExpression<BaseList>>()
                .Where(expr =>
                    !expr.Resolved()
                    && !_notAllowedContextTypes.Contains(expr.Type)
                    && !expr.HasDependency(ctx => _notAllowedContextTypes.Contains(ctx.Type))
                )
                .ToArray();

            if (expressions.Length == 0)
            {
                break;
            }
            var resolvableFhirpaths = expressions
                .OfType<FhirPathExpression>()
                .Where(expr => !expr.Resolved() && expr.DependenciesResolved())
                .ToArray();

            _logger.LogDebug(
                "Resolving FHIRPath expressions: [\n\t{Expressions}\n]",
                string.Join(",\n\t", resolvableFhirpaths.Select(expr => expr.Expression))
            );

            if (ResolveFhirPathExpression(resolvableFhirpaths))
            {
                continue;
            }

            var resolvedFhirPaths = resolvableFhirpaths.Where(expr => expr.Resolved()).ToArray();

            if (resolvedFhirPaths.Length != resolvableFhirpaths.Length)
            {
                _logger.LogDebug(
                    "Unresolved FHIRPath expressions: [{Expressions}]",
                    string.Join(
                        ", ",
                        resolvableFhirpaths.Where(expr => !expr.Resolved()).Select(expr => expr.Expression)
                    )
                );
            }

            expressions = _scopeTree
                .CurrentScope.AllContextInSubtree()
                .OfType<IQuestionnaireExpression<BaseList>>()
                .Where(expr =>
                    !expr.Resolved()
                    && !_notAllowedContextTypes.Contains(expr.Type)
                    && !expr.HasDependency(ctx => _notAllowedContextTypes.Contains(ctx.Type))
                )
                .ToArray();

            var resolvableFhirQueries = expressions
                .OfType<FhirQueryExpression>()
                .Where(expr =>
                    expr.ExpressionLanguage == Constants.FHIR_QUERY_MIME
                    && !expr.Resolved()
                    && expr.DependenciesResolved()
                )
                .ToArray();

            if (
                resolvableFhirQueries.Length > 0
                && await ResolveFhirQueriesAsync(resolvableFhirQueries, cancellationToken)
            )
            {
                continue;
            }

            var resolvedFhirQueries = resolvableFhirQueries.Where(expr => expr.Resolved()).ToArray();

            if (resolvedFhirQueries.Length != resolvableFhirQueries.Length)
            {
                _logger.LogDebug(
                    "Unresolved FHIR queries: [{Queries}]",
                    string.Join(
                        ", ",
                        resolvableFhirQueries.Where(expr => !expr.Resolved()).Select(expr => expr.Expression)
                    )
                );
            }

            if (resolvedFhirPaths.Length + resolvedFhirQueries.Length == 0)
            {
                _logger.LogError("could not resolve all dependencies");
                return false;
            }
        }

        return true;
    }

    private HashSet<IQuestionnaireExpression<BaseList>>? TopologicalSort(Scope scope)
    {
        var expressions = scope.AllContextInSubtree().OfType<IQuestionnaireExpression<BaseList>>();

        var result = new HashSet<IQuestionnaireExpression<BaseList>>(QuestionnaireContextComparer<BaseList>.Default);
        var orderedExprs = new HashSet<IQuestionnaireExpression<BaseList>>(
            QuestionnaireContextComparer<BaseList>.Default
        );
        var visitedExprs = new HashSet<IQuestionnaireExpression<BaseList>>(
            QuestionnaireContextComparer<BaseList>.Default
        );

        foreach (var expr in expressions)
        {
            if (orderedExprs.Contains(expr))
            {
                continue;
            }

            if (!VisitExpr(expr, orderedExprs, visitedExprs, result))
            {
                return null;
            }
        }

        return result;
    }

    private bool VisitExpr(
        IQuestionnaireExpression<BaseList> expr,
        HashSet<IQuestionnaireExpression<BaseList>> orderedExprs,
        HashSet<IQuestionnaireExpression<BaseList>> visitedExprs,
        HashSet<IQuestionnaireExpression<BaseList>> result
    )
    {
        if (orderedExprs.Contains(expr))
        {
            return true;
        }

        if (visitedExprs.Contains(expr))
        {
            _logger.LogError("circular reference detected for expression {0}", expr.Expression);
            return false;
        }

        visitedExprs.Add(expr);
        foreach (var dep in expr.Dependencies.OfType<IQuestionnaireExpression<BaseList>>())
        {
            if (!VisitExpr(dep, orderedExprs, visitedExprs, result))
            {
                return false;
            }
        }

        visitedExprs.Remove(expr);
        orderedExprs.Add(expr);
        result.Add(expr);

        return true;
    }

    private bool ResolveFhirPathExpression(IReadOnlyCollection<FhirPathExpression> unresolvedExpressions)
    {
        var fhirpathQueries = unresolvedExpressions
            .Where(query => query.ExpressionLanguage == Constants.FHIRPATH_MIME && query.DependenciesResolved())
            .ToArray();

        foreach (var query in fhirpathQueries)
        {
            var evalResult = _fhirPathEvaluator.EvaluateExpr(query);
            if (evalResult is null)
            {
                _logger.LogWarning("Something went wrong during evaluation for {0}", query.Expression);
                query.SetValue(null);
                continue;
            }

            var fhirpathResult = evalResult.Result;

            if (fhirpathResult.Count == 0)
            {
                _logger.LogDebug("Result for {Expr} is empty", query.Expression);
                Type? type = null;
                if (
                    query.Type == QuestionnaireContextType.ExtractionContext
                    && query.Dependencies.OfType<FhirQueryExpression>().FirstOrDefault() is FhirQueryExpression dep
                    && dep.ValueType is not null
                )
                {
                    type = dep.ValueType;
                }
                else if (
                    query.Type == QuestionnaireContextType.Embedded
                    && query.Dependants.OfType<FhirQueryExpression>().FirstOrDefault()
                        is FhirQueryExpression extractionContextExpr
                )
                {
                    var resourceName = extractionContextExpr.Expression.Split('?').First();
                    var fhirType = ModelInfo.GetTypeForFhirType(resourceName);

                    if (fhirType is not null)
                    {
                        type = fhirType;
                    }
                    else
                    {
                        _logger.LogError("Cannot find type of {ResourceName}", resourceName);
                    }
                }

                BaseList? value = null;
                if (type is not null)
                {
                    _logger.LogDebug("Creating resource {Type}", type);
                    var resource = Activator.CreateInstance(type) as Resource;
                    if (resource is not null)
                    {
                        value = new[] { resource };
                    }
                }
                query.SetValue(value);
                continue;
            }

            _logger.LogDebug("Got {Count} results for FHIRPath {Expr}", fhirpathResult.Count, query.Expression);
            query.SetValue(fhirpathResult, evalResult.SourceResourceType);
            if (query.Type == QuestionnaireContextType.Embedded)
            {
                if (fhirpathResult.Count > 1)
                {
                    _logger.LogWarning("Embedded {0} has more than one result", query.Expression);
                    continue;
                }
                query.SetValue(fhirpathResult);

                var fhirqueryDependants = query
                    .Dependants.Where(dep => dep.ExpressionLanguage == Constants.FHIR_QUERY_MIME)
                    .ToArray();

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
            else if (
                fhirpathResult.Count > 0
                && fhirpathResult.First() is QuestionnaireResponse.ItemComponent itemComponent
            )
            {
                var answers = fhirpathResult
                    .SelectMany(result =>
                        result is QuestionnaireResponse.ItemComponent item
                            ? item.Answer.ToArray()
                            : Array.Empty<QuestionnaireResponse.AnswerComponent>()
                    )
                    .Select(a => a.Value);

                var deps = query.Dependencies.Where(dep => dep.Scope.Item?.LinkId == itemComponent.LinkId);

                var resolved = deps.Where(dep =>
                    dep.Resolved()
                    && (
                        dep.Type == QuestionnaireContextType.InitialExpression
                        || dep.Type == QuestionnaireContextType.CalculatedExpression
                    )
                );

                var value = deps.SelectMany(dep => dep.Value!).Concat(answers).ToArray();

                query.SetValue(value);
            }
            else if (
                fhirpathResult.First().GetType() is Type type
                && !(
                    type.IsSubclassOf(typeof(PrimitiveType))
                    || (type.IsAssignableTo(typeof(Coding)) && query.QuestionnaireItem?.Item.Count == 0)
                )
                && fhirpathResult.Count > 1
            )
            {
                _logger.LogDebug("Exploding {Expr}", query.Expression);
                ExplodeExpression(fhirpathResult, new[] { query }, query.Scope, evalResult.SourceResourceType);
                return true;
            }
        }

        return false;
    }

    private void ExplodeExpression(
        IReadOnlyCollection<Base> results,
        IReadOnlyCollection<IQuestionnaireExpression<BaseList>> originalExprs,
        Scope scope,
        Type? sourceResourceType = null
    )
    {
        _logger.LogDebug("Exploding expressions for result on scope {LinkId}", scope.Item?.LinkId ?? "root");
        if (scope.Item is null)
        {
            _logger.LogError("Cannot explode expression on root");
            return;
        }

        if (scope.Context.Any(ctx => ctx.Type == QuestionnaireContextType.ExtractionContextId))
        {
            var resources = results.OfType<Resource>();
            var existingScopes = _scopeTree.CurrentScope.GetChildScope(child =>
                child.Item is not null && child.Item.LinkId == scope.Item.LinkId
            );

            _logger.LogDebug(
                "There are {Count} existing scopes with LinkId {LinkId}",
                existingScopes.Count,
                scope.Item.LinkId
            );
            foreach (var existing in existingScopes)
            {
                var linkId = existing.Item!.LinkId;
                var extractionIdExpr =
                    existing.Context.FirstOrDefault(ctx => ctx.Type == QuestionnaireContextType.ExtractionContextId)
                    as FhirPathExpression;

                if (extractionIdExpr is null)
                {
                    _logger.LogWarning("could not find key on extractionContext for QuestionnaireItem {0}", linkId);
                    continue;
                }

                var extractionExpr =
                    existing.Context.FirstOrDefault(ctx => ctx.Type == QuestionnaireContextType.ExtractionContext)
                    as IQuestionnaireExpression<BaseList>;

                if (extractionExpr is null)
                {
                    _logger.LogWarning("could not find extractionContext for QuestionnaireItem {0}", linkId);
                    continue;
                }

                var result = _fhirPathEvaluator.EvaluateExpr(extractionIdExpr);
                Resource? resource = null;
                if (result is null || result.Result.Count == 0)
                {
                    _logger.LogWarning(
                        "Could not resolve expression {0} on QuestionnaireItem {1}",
                        extractionIdExpr.Expression,
                        linkId
                    );
                }
                else
                {
                    if (result.Result.Count > 1)
                    {
                        _logger.LogWarning(
                            "Key expression {0} resolved to more than one value for {1}",
                            extractionIdExpr.Expression,
                            linkId
                        );
                        continue;
                    }

                    if (result.Result.First() is not FhirString str)
                    {
                        _logger.LogWarning("key does not resolve to string");
                        continue;
                    }

                    resource = resources.FirstOrDefault(resource => resource.Id == str.Value);
                }

                if (resource is null)
                {
                    _logger.LogDebug("Creating new resource during exploding for {Expr}", extractionExpr.Expression);
                    string? query = null;
                    if (extractionExpr is FhirQueryExpression)
                    {
                        query = extractionExpr.Expression;
                    }
                    else if (
                        extractionExpr.Dependencies.FirstOrDefault(dep => dep is FhirQueryExpression)
                        is FhirQueryExpression expr
                    )
                    {
                        query = expr.Expression;
                    }

                    if (!string.IsNullOrEmpty(query))
                    {
                        var fhirTypeName = query.Split('?').FirstOrDefault();
                        if (
                            !string.IsNullOrEmpty(fhirTypeName)
                            && ModelInfo.GetTypeForFhirType(fhirTypeName) is Type fhirType
                        )
                        {
                            resource = Activator.CreateInstance(fhirType) as Resource;
                        }
                    }
                }

                if (resource is not null)
                {
                    extractionExpr.SetValue(new[] { resource });
                }
            }
        }
        else if (_resolvingContext == ResolvingContext.Population)
        {
            var newScopes = results
                .Select(result =>
                {
                    var newScope = scope.Clone();

                    var newExprs = newScope
                        .Context.OfType<QuestionnaireExpression<BaseList>>()
                        .Where(ctx => originalExprs.Contains(ctx.ClonedFrom));
                    foreach (var expr in newExprs)
                    {
                        var value = new[] { result };
                        if (expr is FhirPathExpression fhirpathExpr && sourceResourceType is not null)
                        {
                            fhirpathExpr.SetValue(value, sourceResourceType);
                        }
                        else
                        {
                            expr.SetValue(value);
                        }
                    }

                    var allNewExprs = newScope.AllContextInSubtree();

                    foreach (var expr in newExprs)
                    {
                        ReplaceDependencies(expr.ClonedFrom!, expr, allNewExprs);
                    }

                    return newScope;
                })
                .ToArray();

            var index = scope.Parent?.Children.IndexOf(scope);

            if (!index.HasValue)
            {
                _logger.LogError(
                    "Scope of exploding expression {0} does not have a parent",
                    originalExprs.First().Expression
                );
                return;
            }
            scope.Parent?.Children.RemoveAt(index.Value);
            scope.Parent?.Children.InsertRange(index.Value, newScopes);
        }
    }

    private void ReplaceDependencies(
        IQuestionnaireExpression<BaseList> originalExpr,
        IQuestionnaireExpression<BaseList> replacementExpr,
        IReadOnlyCollection<IQuestionnaireContext<BaseList>> allContext
    )
    {
        foreach (var dependant in originalExpr.Dependants)
        {
            if (
                allContext.FirstOrDefault(ctx =>
                    ctx is IClonable<IQuestionnaireExpression<BaseList>> cloned && cloned.ClonedFrom == dependant
                )
                is IQuestionnaireExpression<BaseList> newDep
            )
            {
                newDep.RemoveDependency(originalExpr);
                newDep.AddDependency(replacementExpr);

                ReplaceDependencies(dependant, newDep, allContext);
            }
        }
    }

    private async Task<bool> ResolveFhirQueriesAsync(
        IReadOnlyCollection<FhirQueryExpression> unresolvedExpressions,
        CancellationToken cancellationToken = default
    )
    {
        var urls = unresolvedExpressions
            .Select(query => query.Expression)
            .Distinct()
            .Where(url => !_queryResults.ContainsKey(url))
            .ToArray();

        _logger.LogDebug("Resolving FHIR Queries: [{Queries}]", string.Join(",\n", urls));

        var resourceResult = await _resourceLoader.GetResourcesAsync(urls, cancellationToken);

        foreach (var result in _queryResults)
        {
            HandleFhirQueryResult(result, unresolvedExpressions);
        }

        foreach (var result in resourceResult)
        {
            _queryResults[result.Key] = result.Value;

            HandleFhirQueryResult(result, unresolvedExpressions);
        }

        var failedQueries = unresolvedExpressions.Where(expr => !(expr.Value?.Count > 0)).ToArray();
        foreach (var query in failedQueries)
        {
            var fhirTypeName = query.Expression.Split('?').FirstOrDefault();
            if (!string.IsNullOrEmpty(fhirTypeName) && ModelInfo.GetTypeForFhirType(fhirTypeName) is Type fhirType)
            {
                if (query.Type == QuestionnaireContextType.ExtractionContext)
                {
                    var resource = Activator.CreateInstance(fhirType) as Resource;
                    if (resource is not null)
                    {
                        query.SetValue(new[] { resource });
                        continue;
                    }
                }
                else
                {
                    query.SetValue(fhirType);
                }
            }
            else
            {
                _logger.LogError(
                    "Could not find FhirType to set as default value for failed query {Query}",
                    query.Expression
                );
                query.SetValue(null as BaseList);
            }
        }

        return false;
    }

    private bool HandleFhirQueryResult(
        KeyValuePair<string, IReadOnlyCollection<Resource>> result,
        IReadOnlyCollection<IQuestionnaireExpression<BaseList>> unresolvedExpressions
    )
    {
        var exprs = unresolvedExpressions.Where(expr => expr.Expression == result.Key).ToArray();

        if (result.Value.Count > 1 && exprs.Length > 0)
        {
            var scopeExprs = exprs.GroupBy(expr => expr.Scope);

            var scope = scopeExprs.First();
            if (scope.Key.Item is not null)
            {
                ExplodeExpression(result.Value, scope.ToArray(), scope.Key);
                return true;
            }
        }

        foreach (var expr in exprs)
        {
            expr.SetValue(result.Value);
        }

        return false;
    }

    private FhirPathExpression CreateFhirPathExpression(
        string? name,
        string expr,
        QuestionnaireContextType queryType,
        Scope scope,
        IQuestionnaireExpression<BaseList>? from = null
    ) =>
        new(
            _idProvider.GetId(),
            name,
            expr,
            scope,
            queryType,
            from is not null ? from.QuestionnaireItem : _scopeTree.CurrentItem,
            from is not null ? from.QuestionnaireResponseItem : _scopeTree.CurrentResponseItem
        );

    private FhirQueryExpression CreateFhirQueryExpression(
        string? name,
        string expr,
        QuestionnaireContextType queryType,
        Scope scope
    ) => new(_idProvider.GetId(), name, expr, scope, queryType, _scopeTree.CurrentItem, _scopeTree.CurrentResponseItem);
}
