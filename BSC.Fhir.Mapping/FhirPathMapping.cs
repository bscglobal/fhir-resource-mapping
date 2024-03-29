using System.Text.Json;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping;

using BaseList = IReadOnlyCollection<Base>;

public record EvaluationResult(IReadOnlyCollection<Base> Result, Type? SourceResourceType, string? Name);

public class FhirPathMapping
{
    private static FhirPathCompiler COMPILER = new(new SymbolTable().AddStandardFP().AddFhirExtensions());
    private static FhirPathCompilerCache CACHE = new(COMPILER);

    private class EvaluationContext
    {
        public IEnumerable<Base>? Resource { get; set; }
        public string Expression { get; set; }

        public EvaluationContext(string expression, IReadOnlyCollection<Base>? resource = null)
        {
            Expression = expression;
            Resource = resource;
        }
    }

    private readonly ILogger<FhirPathMapping> _logger;

    public FhirPathMapping(ILogger<FhirPathMapping> logger)
    {
        _logger = logger;
    }

    public EvaluationResult? EvaluateExpr(FhirPathExpression expr, string? expressionName = null)
    {
        var evaluationCtx = GetEvaluationContext(expr);
        if (evaluationCtx is null)
        {
            return new EvaluationResult(Array.Empty<Base>(), null, expressionName);
        }

        try
        {
            var totalResults = new List<Base>();
            var elements =
                evaluationCtx.Resource?.Select(r => r.ToTypedElement()) ?? new[] { ElementNode.ForPrimitive(true) };
            foreach (var element in elements)
            {
                var result = CACHE
                    .Select(
                        element,
                        evaluationCtx.Expression,
                        evaluationCtx.Resource is not null
                            ? new FhirEvaluationContext(ElementNodeExtensions.ToScopedNode(element))
                            : null
                    )
                    .ToFhirValues();

                totalResults.AddRange(result);
            }

            return new EvaluationResult(
                totalResults,
                evaluationCtx.Resource?.FirstOrDefault()?.GetType(),
                expressionName
            );
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                string.Format(
                    "Problem during FHIRPath evaluation: {0}\n\nMessage: {1}\nExpr: {2}\nResource: {3}\n\nStack Trace:\n\n{4}\n\n",
                    e.GetType().ToString(),
                    e.Message,
                    evaluationCtx.Expression,
                    JsonSerializer.Serialize(evaluationCtx.Resource),
                    e.StackTrace
                )
            );
            return null;
        }
    }

    private EvaluationContext? GetEvaluationContext(FhirPathExpression expr)
    {
        EvaluationContext? evaluationCtx = null;
        var expressionParts = expr.Expression.Split('.');
        var start = expressionParts[0];
        if (start.StartsWith("%"))
        {
            evaluationCtx = start switch
            {
                "%resource" => ResourceEvaluationSource(expr.Expression, expr.Scope),
                "%questionnaire" => QuestionnaireEvaluationSource(expressionParts, expr.Scope),
                "%context" => ContextEvaluationSource(expressionParts, expr.Scope),
                "%qitem" => QItemEvaluationSource(expressionParts, expr.Scope),
                _ => VariableEvaluationSource(expressionParts, expr)
            };
        }
        else if (expr.Scope.ExtractionContext() is IQuestionnaireContext<BaseList> context)
        {
            evaluationCtx = new(expr.Expression, context.Value is not null ? context.Value : null);
        }
        else
        {
            _logger.LogError("Could not find evaluation context for expression {0}", expr.Expression);
            evaluationCtx = new(expr.Expression);
        }

        return evaluationCtx;
    }

    private EvaluationContext ResourceEvaluationSource(string expr, Scope scope)
    {
        return new(expr, new[] { scope.QuestionnaireResponse });
    }

    private EvaluationContext QuestionnaireEvaluationSource(string[] exprParts, Scope scope)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        return new(execExpr, new[] { scope.Questionnaire });
    }

    private static EvaluationContext ContextEvaluationSource(string[] exprParts, Scope scope)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        Base? source = scope.ResponseItem as Base ?? scope.ResponseItem;

        return new(execExpr, source is not null ? new[] { source } : null);
    }

    private static EvaluationContext QItemEvaluationSource(string[] exprParts, Scope scope)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        Base source = scope.Item switch
        {
            null
                => throw new InvalidOperationException(
                    "Can not access QuestionnaireItem from expression not on QuestionnaireItem"
                ),
            _ => scope.Item
        };

        return new(execExpr, new[] { source });
    }

    private EvaluationContext? VariableEvaluationSource(string[] exprParts, FhirPathExpression expression)
    {
        EvaluationContext? evaluationCtx = null;
        var variableName = exprParts[0][1..];
        var context = expression.Dependencies.FirstOrDefault(dep => dep.Resolved() && dep.Name == variableName);

        if (context?.Value?.Count > 0)
        {
            exprParts[0] = "%resource";
            var execExpr = string.Join('.', exprParts);
            evaluationCtx = new(execExpr, context.Value);
        }
        else if (context?.Value?.Count == 0)
        {
            _logger.LogDebug("Context %{Variable} has an empty list of values", variableName);
        }
        else
        {
            _logger.LogError("Cannot find context in scope for variable {0}", variableName);
        }

        return evaluationCtx;
    }
}
