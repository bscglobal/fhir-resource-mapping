using System.Text.Json;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;

namespace BSC.Fhir.Mapping;

public record EvaluationResult(Base SourceResource, Base[] Result);

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

    public static EvaluationResult? EvaluateExpr(string expr, MappingContext ctx)
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

            return new EvaluationResult(evaluationCtx.Resource, result);
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
        if (expr.StartsWith("%"))
        {
            if (expr.StartsWith("%resource"))
            {
                if (ctx.Questionnaire is null)
                {
                    throw new ArgumentException("Questionnaire in MappingContext is null");
                }

                return new(expr, ctx.Questionnaire);
            }
            else
            {
                var expressionParts = expr.Split('.');
                var variableName = expressionParts.First()[1..];

                if (!ctx.TryGetValue(variableName, out var variable))
                {
                    expressionParts[0] = "%resource";
                    var execExpr = string.Join('.', expressionParts);

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

                    expressionParts[0] = "%resource";
                    var execExpr = string.Join('.', expressionParts);

                    evaluationCtx = new(execExpr, variable.Value.First());
                }
            }
        }
        else if (ctx.CurrentContext is not null)
        {
            evaluationCtx = new(expr, ctx.CurrentContext);
        }
        else
        {
            throw new InvalidOperationException("Could not find evaluation context");
        }

        return evaluationCtx;
    }
}
