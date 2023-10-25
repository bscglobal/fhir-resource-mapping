using System.Text.Json;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

using BaseList = IReadOnlyCollection<Base>;

public record EvaluationResult(Base SourceResource, Base[] Result, string? Name);

public static class FhirPathMapping
{
    private class EvaluationContext
    {
        public Base? Resource { get; set; }
        public string Expression { get; set; }

        public EvaluationContext(string expression, Base? resource = null)
        {
            Expression = expression;
            Resource = resource;
        }
    }

    public static EvaluationResult? EvaluateExpr(string expr, Scope<BaseList> scope, string? expressionName = null)
    {
        var evaluationCtx = GetEvaluationContext(expr, scope);
        if (evaluationCtx?.Resource is null)
        {
            return null;
        }

        try
        {
            var result = FhirPathExtensions
                .Select(
                    evaluationCtx.Resource,
                    evaluationCtx.Expression,
                    new FhirEvaluationContext(
                        ElementNodeExtensions.ToScopedNode(evaluationCtx.Resource.ToTypedElement())
                    )
                )
                .ToArray();

            return new EvaluationResult(evaluationCtx.Resource, result, expressionName);
        }
        catch (Exception e)
        {
            Console.WriteLine(
                "\nException thrown: {0}\nMessage: {1}\nExpr: {2}\nResource: {3}\n\nStack Trace:\n\n{4}\n\n",
                e.GetType().ToString(),
                e.Message,
                evaluationCtx.Expression,
                JsonSerializer.Serialize(evaluationCtx.Resource),
                e.StackTrace
            );
            return null;
        }
    }

    public static Type? EvaluateTypeFromExpr(string expr, Scope<BaseList> scope)
    {
        var evaluationCtx = GetEvaluationContext(expr, scope);

        if (evaluationCtx.Resource is not null)
        {
            var evalResult = FhirPathExtensions.Select(
                evaluationCtx.Resource,
                evaluationCtx.Expression,
                new FhirEvaluationContext { Resource = evaluationCtx.Resource.ToTypedElement() }
            );

            if (evalResult.Any())
            {
                return evalResult.First().GetType();
            }
            else
            {
                var currentContext = evaluationCtx.Resource.GetType();
                foreach (var part in evaluationCtx.Expression.Split('.')[1..])
                {
                    var fieldName = part[0..1].ToUpper() + part[1..];
                    var newContext = currentContext.GetProperty(fieldName)?.PropertyType.NonParameterizedType();
                    if (newContext is null)
                    {
                        break;
                    }

                    currentContext = newContext;
                }

                return currentContext;
            }
        }

        throw new NotImplementedException();
    }

    private static EvaluationContext GetEvaluationContext(string expr, Scope<BaseList> scope)
    {
        EvaluationContext evaluationCtx;
        var expressionParts = expr.Split('.');
        var start = expressionParts[0];
        if (start.StartsWith("%"))
        {
            evaluationCtx = start switch
            {
                "%resource" => ResourceEvaluationSource(expr, scope),
                "%questionnaire" => QuestionnaireEvaluationSource(expressionParts, scope),
                "%context" => ContextEvaluationSource(expressionParts, scope),
                "%qitem" => QItemEvaluationSource(expressionParts, scope),
                _ => VariableEvaluationSource(expressionParts, scope)
            };
        }
        else if (scope.ExtractionContext() is IQuestionnaireContext<BaseList> context)
        {
            evaluationCtx = new(expr, context.Value?.FirstOrDefault());
        }
        else
        {
            throw new InvalidOperationException($"Could not find evaluation context for expression {expr}");
        }

        return evaluationCtx;
    }

    private static EvaluationContext ResourceEvaluationSource(string expr, Scope<BaseList> scope)
    {
        return new(expr, scope.ResponseItem);
    }

    private static EvaluationContext QuestionnaireEvaluationSource(string[] exprParts, Scope<BaseList> scope)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        return new(execExpr, scope.Questionnaire);
    }

    private static EvaluationContext ContextEvaluationSource(string[] exprParts, Scope<BaseList> scope)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        Base? source = scope.ResponseItem as Base ?? scope.ResponseItem;

        return new(execExpr, source);
    }

    private static EvaluationContext QItemEvaluationSource(string[] exprParts, Scope<BaseList> scope)
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

        return new(execExpr, source);
    }

    private static EvaluationContext VariableEvaluationSource(string[] exprParts, Scope<BaseList> scope)
    {
        EvaluationContext evaluationCtx;
        var variableName = exprParts[0][1..];
        if (scope.GetResolvedContext(variableName) is ResolvedContext<BaseList> context)
        {
            if (context.Value.Count == 0 || context.Value.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Cannot use variable with any number of items other than 1. variableName: {variableName}"
                );
            }

            exprParts[0] = "%resource";
            var execExpr = string.Join('.', exprParts);
            evaluationCtx = new(execExpr, context.Value.First());
        }
        else
        {
            exprParts[0] = "%resource";
            var execExpr = string.Join('.', exprParts);

            evaluationCtx = new(execExpr);
        }

        return evaluationCtx;
    }
}
