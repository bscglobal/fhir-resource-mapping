using System.Text.RegularExpressions;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;
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
    private readonly Dictionary<string, IReadOnlyCollection<Resource>> _queryResults = new();
    private readonly ILogger<QuestionnaireParser> _logger;
    private readonly FhirPathMapping _fhirPathEvaluator;

    public QuestionnaireParser(
        INumericIdProvider idProvider,
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        IResourceLoader resourceLoader,
        ResolvingContext resolvingContext,
        FhirPathMapping fhirPathEvaluator,
        ILogger<QuestionnaireParser> logger
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
    }

    private void AddLaunchContextToScope(IDictionary<string, Resource> launchContext)
    {
        var scopedLaunchContext = launchContext.Select(
            kv => new LaunchContext(_idProvider.GetId(), kv.Key, kv.Value, _scopeTree.CurrentScope)
        );

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

        CreateDependencyGraph(_scopeTree.CurrentScope);

        if (IsCircularGraph(_scopeTree.CurrentScope) is IQuestionnaireExpression<BaseList> faultyDep)
        {
            throw new InvalidOperationException($"Detected circular dependency {faultyDep.Expression}");
        }

        await ResolveDependenciesAsync(cancellationToken);
        // TreeDebugging.PrintTree(_scopeTree.CurrentScope);

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

        if (responseItem.Answer.Count == 0 && item.Initial.Count > 0)
        {
            responseItem.Answer = item.Initial
                .Select(initial => new QuestionnaireResponse.AnswerComponent { Value = initial.Value })
                .ToList();
        }

        ParseQuestionnaireItems(item.Item, responseItem.Item);
    }

    private void ParseExtensions(IReadOnlyCollection<Extension> extensions, string? currentLinkId = null)
    {
        var queries = extensions
            .Select(extension => ParseExtension(extension))
            .OfType<QuestionnaireExpression<BaseList>>();
        _scopeTree.CurrentScope.Context.AddRange(queries);
    }

    private IQuestionnaireExpression<BaseList>? ParseExtension(Extension extension)
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
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME, Constants.FHIR_QUERY_MIME },
                    QuestionnaireContextType.ExtractionContext
                ),
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
            _logger.LogWarning(errorMessage($"Unsupported expression language {expression.Language}"));
            return null;
        }

        if (string.IsNullOrEmpty(expression.Expression_))
        {
            _logger.LogWarning(errorMessage("Empty expression"));
            return null;
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

    private void CreateDependencyGraph(Scope scope)
    {
        for (var i = 0; i < scope.Context.Count; i++)
        {
            var context = scope.Context[i];
            if (context is not QuestionnaireExpression<BaseList> query)
            {
                continue;
            }

            if (query.ExpressionLanguage == Constants.FHIR_QUERY_MIME)
            {
                CalculateFhirQueryDependencies(scope, query);
            }
            else if (query is FhirPathExpression fhirpathExpr)
            {
                CalculateFhirPathDependencies(scope, fhirpathExpr);
            }
        }

        foreach (var child in scope.Children)
        {
            CreateDependencyGraph(child);
        }
    }

    private void CalculateFhirQueryDependencies(Scope scope, QuestionnaireExpression<BaseList> query)
    {
        var expression = query.Expression;
        var embeddedFhirpathRegex = @"\{\{(.*)\}\}";
        var matches = Regex.Matches(expression, embeddedFhirpathRegex);

        foreach (Match match in matches)
        {
            var fhirpathExpression = match.Groups.Values.FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(fhirpathExpression))
            {
                _logger.LogWarning("Invalid embedded query {0}", match.Value);
                continue;
            }

            fhirpathExpression = Regex.Replace(fhirpathExpression, "[{}]", "");

            var embeddedQuery = CreateFhirPathExpression(
                null,
                fhirpathExpression,
                QuestionnaireContextType.Embedded,
                scope,
                query
            );
            scope.Context.Add(embeddedQuery);

            query.AddDependency(embeddedQuery);
        }
    }

    private void CalculateFhirPathDependencies(Scope scope, FhirPathExpression query)
    {
        // Regex for splitting fhirpath into respective parts, split by a period and including functions
        var fhirpathRegex = @"([^.]+(\((.+\..+)+\)))?([^.]+)?";
        var expression = query.Expression;

        var parts = Regex.Matches(expression, fhirpathRegex).Select(match => match.Value);
        var variables = parts.Where(part => part.StartsWith('%'));

        foreach (var variable in variables)
        {
            if (Constants.RESPONSE_DEPENDANT_CONTEXT.Contains(variable))
            {
                query.MakeResponseDependant();
                continue;
            }

            var varName = variable[1..];
            var dep = scope.GetContext(varName);

            if (dep is not null)
            {
                query.AddDependency(dep);
            }
            else
            {
                _logger.LogError(
                    "Could not find dependency {VarName} in expression {Expression} for LinkId {LinkId}",
                    varName,
                    expression,
                    query.QuestionnaireItem?.LinkId ?? "root"
                );
            }
        }

        if (query.ResponseDependant)
        {
            var qItemExpr = Regex.Replace(expression, "%resource", "%questionnaire");
            qItemExpr = Regex.Replace(qItemExpr, "%context", "%qitem");
            var qitemExpr = (FhirPathExpression)query.Clone(new { Id = _idProvider.GetId(), Scope = scope });
            var result = _fhirPathEvaluator.EvaluateExpr(qitemExpr);

            if (result is null || result.Result.FirstOrDefault() is not Questionnaire.ItemComponent qItem)
            {
                _logger.LogWarning(
                    "Response Dependant FHIRPath expression does not resolve to QuestionnaireItem: {Expr}",
                    qItemExpr
                );
            }
            else
            {
                var targetScope = ScopeTree.GetScope(qItem.LinkId, scope);
                var initial =
                    targetScope?.Context.FirstOrDefault(ctx => ctx.Type == QuestionnaireContextType.InitialExpression)
                    as IQuestionnaireExpression<BaseList>;

                if (initial is not null)
                {
                    query.AddDependency(initial);
                }
            }
        }
    }

    private IQuestionnaireExpression<BaseList>? IsCircularGraph(Scope scope)
    {
        var checkedExprs = new HashSet<IQuestionnaireContext<BaseList>>();

        foreach (var ctx in scope.Context.OfType<IQuestionnaireExpression<BaseList>>())
        {
            if (IsCircularGraph(ctx.Id, ctx) is IQuestionnaireExpression<BaseList> faultyDep)
            {
                return faultyDep;
            }
        }

        foreach (var child in scope.Children)
        {
            if (IsCircularGraph(child) is IQuestionnaireExpression<BaseList> faultyDep)
            {
                return faultyDep;
            }
        }

        return null;
    }

    private IQuestionnaireExpression<BaseList>? IsCircularGraph(
        int originalId,
        IQuestionnaireExpression<BaseList> expression
    )
    {
        foreach (var dep in expression.Dependencies.OfType<IQuestionnaireExpression<BaseList>>())
        {
            if (originalId == dep.Id)
            {
                return expression;
            }

            if (IsCircularGraph(originalId, dep) is IQuestionnaireExpression<BaseList> faultyDep)
            {
                return faultyDep;
            }
        }

        return null;
    }

    private async Task<bool> ResolveDependenciesAsync(CancellationToken cancellationToken = default)
    {
        var oldLength = 0;
        while (true)
        {
            var expressions = _scopeTree.CurrentScope
                .AllContextInSubtree()
                .OfType<IQuestionnaireExpression<BaseList>>()
                .Where(
                    expr =>
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

            oldLength = resolvableFhirpaths.Length;

            if (ResolveFhirPathExpression(resolvableFhirpaths))
            {
                continue;
            }

            var resolvedFhirPaths = resolvableFhirpaths.Where(expr => expr.Resolved()).ToArray();

            expressions = _scopeTree.CurrentScope
                .AllContextInSubtree()
                .OfType<IQuestionnaireExpression<BaseList>>()
                .Where(
                    expr =>
                        !expr.Resolved()
                        && !_notAllowedContextTypes.Contains(expr.Type)
                        && !expr.HasDependency(ctx => _notAllowedContextTypes.Contains(ctx.Type))
                )
                .ToArray();

            var resolvableFhirQueries = expressions
                .OfType<FhirQueryExpression>()
                .Where(
                    expr =>
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

            if (resolvedFhirPaths.Length + resolvableFhirQueries.Length == 0)
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

                var fhirqueryDependants = query.Dependants
                    .Where(dep => dep.ExpressionLanguage == Constants.FHIR_QUERY_MIME)
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
                fhirpathResult.Count == 1 && fhirpathResult.First() is QuestionnaireResponse.ItemComponent responseItem
            )
            {
                query.SetValue(responseItem.Answer.Select(a => a.Value).ToArray());
            }
            else if (!fhirpathResult.First().GetType().IsSubclassOf(typeof(PrimitiveType)) && fhirpathResult.Count > 1)
            {
                _logger.LogDebug("exploding {Expr}", query.Expression);
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
            var existingScopes = _scopeTree.CurrentScope.GetChildScope(
                child => child.Item is not null && child.Item.LinkId == scope.Item.LinkId
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
                    if (resource is not null)
                    {
                        _logger.LogDebug("Found resource for value {Value}", str.Value);
                    }
                }

                if (resource is null)
                {
                    _logger.LogDebug("Creating new resource during explosijklng for {Expr}", extractionExpr.Expression);
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

                    var newExprs = newScope.Context
                        .OfType<QuestionnaireExpression<BaseList>>()
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
                allContext.FirstOrDefault(
                    ctx => ctx is IClonable<IQuestionnaireExpression<BaseList>> cloned && cloned.ClonedFrom == dependant
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

        var resourceResult = await _resourceLoader.GetResourcesAsync(urls, cancellationToken);
        _logger.LogDebug("Got {Count} results for Fhir Query", resourceResult.Count);

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
