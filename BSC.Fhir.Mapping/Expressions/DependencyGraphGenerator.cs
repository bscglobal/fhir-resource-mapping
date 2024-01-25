using System.Text.RegularExpressions;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class DependencyGraphGenerator : IDependencyGraphGenerator
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly INumericIdProvider _idProvider;
    private readonly FhirPathMapping _fhirPathEvaluator;

    public DependencyGraphGenerator(
        INumericIdProvider idProvider,
        FhirPathMapping fhirPathEvaluator,
        ILoggerFactory loggerFactory
    )
    {
        _idProvider = idProvider;
        _fhirPathEvaluator = fhirPathEvaluator;
        _loggerFactory = loggerFactory;
    }

    public void Generate(Scope rootScope)
    {
        var generator = new Generator(
            rootScope,
            _loggerFactory.CreateLogger<Generator>(),
            _idProvider,
            _fhirPathEvaluator
        );
        generator.CreateDependencyGraph(rootScope);

        if (generator.IsCircularGraph(rootScope) is IQuestionnaireExpression<BaseList> faultyDep)
        {
            throw new InvalidOperationException($"Detected circular dependency {faultyDep.Expression}");
        }
    }

    private class Generator
    {
        private readonly Scope _rootScope;
        private readonly ILogger<Generator> _logger;
        private readonly INumericIdProvider _idProvider;
        private readonly HashSet<int> _visitedScopes = new();
        private readonly FhirPathMapping _fhirPathEvaluator;

        public Generator(
            Scope rootScope,
            ILogger<Generator> logger,
            INumericIdProvider idProvider,
            FhirPathMapping fhirPathEvaluator
        )
        {
            _rootScope = rootScope;
            _logger = logger;
            _idProvider = idProvider;
            _fhirPathEvaluator = fhirPathEvaluator;
        }

        public void CreateDependencyGraph(Scope scope)
        {
            if (!_visitedScopes.Add(scope.Id))
            {
                _logger.LogDebug("Already visited scope {LinkId}", scope.Item?.LinkId ?? "root");
                return;
            }

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
                if (query.Name == "images")
                {
                    _logger.LogDebug("Match: {0}", match.Value);
                }
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
                if (query.Name == "images")
                {
                    _logger.LogDebug(
                        "Adding Embedded expression {Expression} ({Id}) to scope {LinkId}",
                        embeddedQuery.Expression,
                        embeddedQuery.Id,
                        scope.Item?.LinkId ?? "root"
                    );
                }
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
                var qitemExpr = (FhirPathExpression)query.Clone(new { Id = _idProvider.GetId(), Scope = scope, });

                qitemExpr.ReplaceExpression(qItemExpr);
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
                    _logger.LogDebug("Getting dependencies for expression {Expr}", query.Expression);
                    var targetScope = ScopeTree.GetScope(qItem.LinkId, _rootScope);

                    if (targetScope is null)
                    {
                        _logger.LogDebug("Could not find scope for LinkId {LinkId}", qItem.LinkId);
                        return;
                    }

                    if (targetScope == scope)
                    {
                        _logger.LogDebug("Target scope is current scope");
                        return;
                    }

                    var currentScopePath = scope.Path().ToArray();

                    if (!currentScopePath.Contains(targetScope))
                    {
                        var targetScopePath = targetScope.Path().ToArray();

                        var shortestPath = Math.Min(currentScopePath.Length, targetScopePath.Length);

                        _logger.LogDebug(
                            "Current Scope Path: [{Path}]",
                            string.Join(", ", currentScopePath.Select(s => $"{s.Item?.LinkId ?? "root"} - {s.Id}"))
                        );
                        _logger.LogDebug(
                            "Target Scope Path: [{Path}]",
                            string.Join(", ", targetScopePath.Select(s => $"{s.Item?.LinkId ?? "root"} - {s.Id}"))
                        );

                        var targetRoot = targetScopePath.First();

                        for (var i = 0; i < shortestPath; i++)
                        {
                            var scope1 = currentScopePath[i];
                            var scope2 = targetScopePath[i];

                            if (scope1 != scope2)
                            {
                                _logger.LogDebug(
                                    "Found LCA scope: {LinkId} - {Id}",
                                    targetRoot.Item?.LinkId ?? "root",
                                    targetRoot.Id
                                );
                                targetRoot = scope2;
                                break;
                            }

                            if (i == shortestPath - 1)
                            {
                                _logger.LogDebug(
                                    "Found LCA scope: {LinkId} - {Id}",
                                    scope1.Item?.LinkId ?? "root",
                                    scope1.Id
                                );
                                targetRoot = targetScopePath[i + 1];
                                break;
                            }

                            _logger.LogDebug(
                                "Found common scope: {LinkId} - {Id}",
                                scope1.Item?.LinkId ?? "root",
                                scope1.Id
                            );
                            targetRoot = scope1;
                        }

                        CreateDependencyGraph(targetRoot);
                    }

                    _logger.LogDebug(
                        "Context for Scope {LinkId} is [{Context}]",
                        targetScope.Item?.LinkId ?? "root",
                        string.Join(", ", targetScope.Context.Select(ctx => ctx.Type))
                    );

                    var initial =
                        targetScope.Context.FirstOrDefault(ctx =>
                            ctx.Type == QuestionnaireContextType.InitialExpression
                            || ctx.Type == QuestionnaireContextType.CalculatedExpression
                        ) as IQuestionnaireExpression<BaseList>;

                    if (initial is not null)
                    {
                        _logger.LogDebug("Adding dependency '{0}' to '{1}'", initial.Expression, query.Expression);
                        query.AddDependency(initial);
                    }
                }
            }
        }

        public IQuestionnaireExpression<BaseList>? IsCircularGraph(Scope scope)
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
                from is not null ? from.QuestionnaireItem : _rootScope.Item,
                from is not null ? from.QuestionnaireResponseItem : _rootScope.ResponseItem
            );

        private FhirQueryExpression CreateFhirQueryExpression(
            string? name,
            string expr,
            QuestionnaireContextType queryType,
            Scope scope
        ) => new(_idProvider.GetId(), name, expr, scope, queryType, _rootScope.Item, _rootScope.ResponseItem);
    }
}
