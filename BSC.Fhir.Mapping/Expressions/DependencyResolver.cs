using System.Text.Json;
using System.Text.RegularExpressions;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class DependencyResolver
{
    private readonly INumericIdProvider _idProvider;
    private readonly ScopeTree _scopeTree;
    private readonly Questionnaire _questionnaire;
    private readonly QuestionnaireResponse _questionnaireResponse;
    private readonly IResourceLoader _resourceLoader;
    private readonly ResolvingContext _resolvingContext;
    private readonly QuestionnaireContextType[] _notAllowedContextTypes;
    private readonly Dictionary<string, IReadOnlyCollection<Resource>> _queryResults = new();

    public DependencyResolver(
        INumericIdProvider idProvider,
        Questionnaire questionnaire,
        QuestionnaireResponse? questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        IResourceLoader resourceLoader,
        ResolvingContext resolvingContext
    )
    {
        _questionnaire = questionnaire;
        _questionnaireResponse = questionnaireResponse ?? new();
        _scopeTree = new(questionnaire, questionnaireResponse, idProvider);
        _resourceLoader = resourceLoader;
        _resolvingContext = resolvingContext;
        _idProvider = idProvider;
        _notAllowedContextTypes = resolvingContext switch
        {
            ResolvingContext.Population => Constants.EXTRACTION_ONLY_CONTEXTS,
            ResolvingContext.Extraction => Constants.POPULATION_ONLY_CONTEXTS,
            _ => Array.Empty<QuestionnaireContextType>()
        };

        AddLaunchContextToScope(launchContext);
    }

    private void AddLaunchContextToScope(IDictionary<string, Resource> launchContext)
    {
        var scopedLaunchContext = launchContext.Select(
            kv => new LaunchContext(_idProvider.GetId(), kv.Key, kv.Value, _scopeTree.CurrentScope)
        );

        _scopeTree.CurrentScope.Context.AddRange(scopedLaunchContext);
    }

    public async Task<Scope?> ParseQuestionnaireAsync(CancellationToken cancellationToken = default)
    {
        var rootExtensions = _questionnaire.AllExtensions();

        ParseExtensions(rootExtensions.ToArray());
        ParseQuestionnaireItems(_questionnaire.Item, _questionnaireResponse.Item);

        if (_scopeTree.CurrentScope.Parent is not null)
        {
            Console.WriteLine("Error: not in global scope");
            return null;
        }

        var graphResult = CreateDependencyGraph(_scopeTree.CurrentScope);
        if (graphResult is not null)
        {
            return null;
        }

        if (IsCircularGraph(_scopeTree.CurrentScope) is IQuestionnaireExpression<BaseList> faultyDep)
        {
            Console.WriteLine("Error: detected circular dependency {0}", faultyDep.Expression);
            return null;
        }

        // TreeDebugging.PrintTree(_scopeTree.CurrentScope);

        await ResolveDependenciesAsync(cancellationToken);

        return _scopeTree.CurrentScope;
    }

    private void ParseQuestionnaireItems(
        IReadOnlyCollection<Questionnaire.ItemComponent> items,
        List<QuestionnaireResponse.ItemComponent> responseItems
    )
    {
        var sortedItems = items.OrderBy(item => item.LinkId);
        var sortedResponseItems = responseItems.OrderBy(responseItem => responseItem.LinkId);
        var responseItemQueue = new Queue<QuestionnaireResponse.ItemComponent>(sortedResponseItems);
        foreach (var item in items)
        {
            var responseItemCount = 0;
            while (responseItemQueue.TryPeek(out var responseItem) && responseItem.LinkId == item.LinkId)
            {
                responseItemCount++;
                responseItem = responseItemQueue.Dequeue();

                _scopeTree.PushScope(item, responseItem);
                ParseQuestionnaireItem(item, responseItem);
                _scopeTree.PopScope();
            }

            if (responseItemCount == 0)
            {
                var responseItem = new QuestionnaireResponse.ItemComponent { LinkId = item.LinkId };

                _scopeTree.PushScope(item, responseItem);
                ParseQuestionnaireItem(item, responseItem);
                _scopeTree.PopScope();
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
            Console.WriteLine("Warning: Questionnaire item does not have LinkId, skipping...");
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
                    new[] { Constants.FHIRPATH_MIME },
                    QuestionnaireContextType.VariableExpression
                ),
            Constants.CALCULATED_EXPRESSION
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

    private QuestionnaireExpression<BaseList>? CreateDependencyGraph(Scope scope)
    {
        for (var i = 0; i < scope.Context.Count; i++)
        {
            var context = scope.Context[i];
            if (context is not QuestionnaireExpression<BaseList> query)
            {
                continue;
            }

            QuestionnaireExpression<BaseList>? result = null;
            if (query.ExpressionLanguage == Constants.FHIR_QUERY_MIME)
            {
                result = CalculateFhirQueryDependencies(scope, query);
            }
            else if (query is FhirPathExpression fhirpathExpr)
            {
                result = CalculateFhirPathDependencies(scope, fhirpathExpr);
            }

            if (result is not null)
            {
                return result;
            }
        }

        foreach (var child in scope.Children)
        {
            if (CreateDependencyGraph(child) is QuestionnaireExpression<BaseList> expr)
            {
                return expr;
            }
        }

        return null;
    }

    private QuestionnaireExpression<BaseList>? CalculateFhirQueryDependencies(
        Scope scope,
        QuestionnaireExpression<BaseList> query
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
                Console.WriteLine("Warning - Invalid embedded query {0}", match.Value);
                continue;
            }

            fhirpathExpression = Regex.Replace(fhirpathExpression, "[{}]", "");

            Console.WriteLine("Debug: Creating embedded expression {0}", fhirpathExpression);
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

        return null;
    }

    private QuestionnaireExpression<BaseList>? CalculateFhirPathDependencies(Scope scope, FhirPathExpression query)
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
                query.AddDependency(dep);
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

        if (query.ResponseDependant)
        {
            var qItemExpr = Regex.Replace(expression, "%resource", "%questionnaire");
            qItemExpr = Regex.Replace(qItemExpr, "%context", "%qitem");
            var qitemExpr = (FhirPathExpression)query.Clone(new { Id = _idProvider.GetId(), Scope = scope });
            var result = FhirPathMapping.EvaluateExpr(qitemExpr);

            if (result is null || result.Result.FirstOrDefault() is not Questionnaire.ItemComponent qItem)
            {
                Console.WriteLine("Warning: could not resolve expression {0}", qItemExpr);
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

        return null;
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
        // TreeDebugging.PrintTree(_scopeTree.CurrentScope);

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
            Console.WriteLine(
                "Debug: Resolved {0}/{1} FHIRPath expressions",
                resolvedFhirPaths.Length,
                resolvableFhirpaths.Length
            );

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
                .Where(
                    expr =>
                        expr.ExpressionLanguage == Constants.FHIR_QUERY_MIME
                        && !expr.Resolved()
                        && expr.DependenciesResolved()
                )
                .ToArray();

            Console.WriteLine("Debug: There are {0} resolvable FHIR queries", resolvableFhirQueries.Length);

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
                Console.WriteLine("Error: could not resolve all dependencies");
                return false;
            }
        }

        // TreeDebugging.PrintTree(_scopeTree.CurrentScope);

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

    private static bool VisitExpr(
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
            Console.WriteLine("Error: circular reference detected for expression {0}", expr.Expression);
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
        Console.WriteLine("Debug: Resolving {0} fhirpath expressions", fhirpathQueries.Length);

        foreach (var query in fhirpathQueries)
        {
            Console.WriteLine(
                "Debug: Evaluating FHIRPath expression '{0}' ({1}). Type: {2}",
                query.Expression,
                query.Name ?? "anonymous",
                query.Type.ToString()
            );
            var evalResult = FhirPathMapping.EvaluateExpr(query);
            if (evalResult is null)
            {
                Console.WriteLine("Warning: Something went  wrong during evaluation for {0}", query.Expression);
                query.SetValue(null);
                continue;
            }

            var fhirpathResult = evalResult.Result;

            if (fhirpathResult.Length == 0)
            {
                Console.WriteLine("Warning: Found no results for {0}", query.Expression);
                query.SetValue(null);
                continue;
            }

            query.SetValue(fhirpathResult, evalResult.SourceResource);
            if (query.Type == QuestionnaireContextType.Embedded)
            {
                if (fhirpathResult.Length > 1)
                {
                    Console.WriteLine("Warning: Embedded {0} has more than one result", query.Expression);
                    continue;
                }
                Console.WriteLine("Debug - replacing embedded fhirpath query {0} with result", fhirpathResult.First());
                query.SetValue(fhirpathResult);

                var fhirqueryDependants = query.Dependants
                    .Where(dep => dep.ExpressionLanguage == Constants.FHIR_QUERY_MIME)
                    .ToArray();

                var escapedQuery = Regex.Escape(query.Expression);
                foreach (var dep in fhirqueryDependants)
                {
                    Console.WriteLine(fhirpathResult.First().ToString());
                    dep.ReplaceExpression(
                        Regex.Replace(
                            dep.Expression,
                            "\\{\\{" + escapedQuery + "\\}\\}",
                            fhirpathResult.First().ToString() ?? ""
                        )
                    );
                }
            }
            else if (!fhirpathResult.First().GetType().IsSubclassOf(typeof(PrimitiveType)) && fhirpathResult.Length > 1)
            {
                ExplodeExpression(fhirpathResult, new[] { query }, query.Scope, evalResult.SourceResource);
                return true;
            }
        }

        return false;
    }

    private void ExplodeExpression(
        IReadOnlyCollection<Base> results,
        IReadOnlyCollection<IQuestionnaireExpression<BaseList>> originalExprs,
        Scope scope,
        Base? sourceResource = null
    )
    {
        if (scope.Item is null)
        {
            Console.WriteLine("Error: cannot explode expression on root");
            return;
        }

        if (scope.Context.Any(ctx => ctx.Type == QuestionnaireContextType.ExtractionContextId))
        {
            var resources = results.OfType<Resource>();
            var existingScopes = _scopeTree.CurrentScope.GetChildScope(
                child => child.Item is not null && child.Item.LinkId == scope.Item.LinkId
            );

            foreach (var existing in existingScopes)
            {
                var linkId = existing.Item!.LinkId;
                var extractionIdExpr =
                    existing.Context.FirstOrDefault(ctx => ctx.Type == QuestionnaireContextType.ExtractionContextId)
                    as FhirPathExpression;

                if (extractionIdExpr is null)
                {
                    Console.WriteLine(
                        "Warning: could not find key on extractionContext for QuestionnaireItem {0}",
                        linkId
                    );
                    continue;
                }

                var extractionExpr =
                    existing.Context.FirstOrDefault(ctx => ctx.Type == QuestionnaireContextType.ExtractionContext)
                    as FhirQueryExpression;

                if (extractionExpr is null)
                {
                    Console.WriteLine("Warning: could not find extractionContext for QuestionnaireItem {0}", linkId);
                    continue;
                }

                var result = FhirPathMapping.EvaluateExpr(extractionIdExpr);
                if (result is null || result.Result.Length == 0)
                {
                    Console.WriteLine(
                        "Warning: could not resolve expression {0} on QuestionnaireItem {1}",
                        extractionIdExpr.Expression,
                        linkId
                    );
                    continue;
                }

                if (result.Result.Length > 1)
                {
                    Console.WriteLine(
                        "Warning: key expression {0} resolved to more than one value for {1}",
                        extractionIdExpr.Expression,
                        linkId
                    );
                    continue;
                }

                if (result.Result.First() is not FhirString str)
                {
                    Console.WriteLine("Warning: key does not resolve to string");
                    continue;
                }

                var resource = resources.FirstOrDefault(resource => resource.Id == str.Value);

                if (resource is null)
                {
                    Console.WriteLine(
                        "Debug: could not find extractionContext resource with key {0} for QuestionnaireItem {1}. Expected Type: {2}",
                        str.Value,
                        linkId
                    );
                    extractionExpr.SetValue(null);
                }
                else
                {
                    Console.WriteLine("Debug: context resource found for LinkId {0}. Key: {1}", linkId, str.Value);
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

                    Console.WriteLine(
                        "Debug: Exploding expression {0} for answer {1}",
                        originalExprs.First().Expression,
                        result
                    );

                    var newExprs = newScope.Context
                        .OfType<QuestionnaireExpression<BaseList>>()
                        .Where(ctx => originalExprs.Contains(ctx.ClonedFrom));
                    foreach (var expr in newExprs)
                    {
                        var value = new[] { result };
                        if (expr is FhirPathExpression fhirpathExpr && sourceResource is not null)
                        {
                            fhirpathExpr.SetValue(value, sourceResource);
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
                Console.WriteLine(
                    "Error: Scope of exploding expression {0} does not have a parent",
                    originalExprs.First().Expression
                );
                return;
            }
            scope.Parent?.Children.RemoveAt(index.Value);
            scope.Parent?.Children.InsertRange(index.Value, newScopes);
        }
        // TreeDebugging.PrintTree(scope.Parent!);
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
        IReadOnlyCollection<IQuestionnaireExpression<BaseList>> unresolvedExpressions,
        CancellationToken cancellationToken = default
    )
    {
        Console.WriteLine(
            "Debug: Resolving Fhir Queries - {0}",
            JsonSerializer.Serialize(
                unresolvedExpressions.Select(expr => expr.Expression),
                new JsonSerializerOptions() { WriteIndented = true }
            )
        );

        var urls = unresolvedExpressions
            .Select(query => query.Expression)
            .Distinct()
            .Where(url => !_queryResults.ContainsKey(url))
            .ToArray();

        Console.WriteLine("Debug: Executing {0} Fhir Queries", urls.Length);
        Console.WriteLine(
            "Debug: {0}",
            JsonSerializer.Serialize(urls, new JsonSerializerOptions() { WriteIndented = true })
        );

        var resourceResult = await _resourceLoader.GetResourcesAsync(urls, cancellationToken);

        // _logger.LogDebug($"Result length: {fhirQueryResult.Entry.Count}");

        foreach (var result in _queryResults)
        {
            HandleFhirQueryResult(result, unresolvedExpressions);
        }
        foreach (var result in resourceResult)
        {
            _queryResults[result.Key] = result.Value;

            HandleFhirQueryResult(result, unresolvedExpressions);
        }
        // TreeDebugging.PrintTree(_scopeTree.CurrentScope);
        return false;
    }

    private bool HandleFhirQueryResult(
        KeyValuePair<string, IReadOnlyCollection<Resource>> result,
        IReadOnlyCollection<IQuestionnaireExpression<BaseList>> unresolvedExpressions
    )
    {
        var exprs = unresolvedExpressions.Where(expr => expr.Expression == result.Key).ToArray();
        Console.WriteLine("Debug: Found result for query {0}. Expressions: {1}", result.Key, exprs.Length);

        if (result.Value.Count > 1)
        {
            var scopeExprs = exprs.GroupBy(expr => expr.Scope);
            Console.WriteLine("Debug: Scope Groups = {0}", scopeExprs.Count());

            var scope = scopeExprs.First();
            Console.WriteLine("Debug: Scope Group Exprs = {0}", scope.Count());
            ExplodeExpression(result.Value, scope.ToArray(), scope.Key);
            return true;
        }
        else
        {
            foreach (var expr in exprs)
            {
                expr.SetValue(result.Value);
            }
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
