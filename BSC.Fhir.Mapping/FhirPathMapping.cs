using System.Text.Json;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

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

    public static EvaluationResult? EvaluateExpr(string expr, MappingContext ctx, string? expressionName = null)
    {
        var evaluationCtx = GetEvaluationContext(expr, ctx);
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

    public static Type? EvaluateTypeFromExpr(string expr, MappingContext ctx)
    {
        var evaluationCtx = GetEvaluationContext(expr, ctx);

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

    private static EvaluationContext GetEvaluationContext(string expr, MappingContext ctx)
    {
        EvaluationContext evaluationCtx;
        var expressionParts = expr.Split('.');
        var start = expressionParts[0];
        if (start.StartsWith("%"))
        {
            evaluationCtx = start switch
            {
                "%resource" => ResourceEvaluationSource(expr, ctx),
                "%questionnaire" => QuestionnaireEvaluationSource(expressionParts, ctx),
                "%context" => ContextEvaluationSource(expressionParts, ctx),
                "%qitem" => QItemEvaluationSource(expressionParts, ctx),
                _ => VariableEvaluationSource(expressionParts, ctx)
            };
        }
        else if (ctx.CurrentExtractionContext is not null)
        {
            evaluationCtx = new(expr, ctx.CurrentExtractionContext.Value);
        }
        else
        {
            throw new InvalidOperationException($"Could not find evaluation context for expression {expr}");
        }

        return evaluationCtx;
    }

    private static EvaluationContext ResourceEvaluationSource(string expr, MappingContext ctx)
    {
        return new(expr, ctx.QuestionnaireResponse);
    }

    private static EvaluationContext QuestionnaireEvaluationSource(string[] exprParts, MappingContext ctx)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        return new(execExpr, ctx.Questionnaire);
    }

    private static EvaluationContext ContextEvaluationSource(string[] exprParts, MappingContext ctx)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        Base? source = ctx.QuestionnaireResponseItem as Base ?? ctx.QuestionnaireResponse;

        return new(execExpr, source);
    }

    private static EvaluationContext QItemEvaluationSource(string[] exprParts, MappingContext ctx)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        Base source = ctx.QuestionnaireItem switch
        {
            null
                => throw new InvalidOperationException(
                    "Can not access QuestionnaireItem from expression not on QuestionnaireItem"
                ),
            _ => ctx.QuestionnaireItem
        };

        return new(execExpr, source);
    }

    private static EvaluationContext VariableEvaluationSource(string[] exprParts, MappingContext ctx)
    {
        EvaluationContext evaluationCtx;
        var variableName = exprParts[0][1..];
        if (!ctx.CurrentContext.TryGetValue(variableName, out var variable))
        {
            exprParts[0] = "%resource";
            var execExpr = string.Join('.', exprParts);

            evaluationCtx = new(execExpr);
        }
        else
        {
            if (variable.Value.Length == 0 || variable.Value.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Cannot use variable with any number of items other than 1. variableName: {variableName}"
                );
            }

            exprParts[0] = "%resource";
            var execExpr = string.Join('.', exprParts);
            evaluationCtx = new(execExpr, variable.Value.First());
        }

        return evaluationCtx;
    }
}
