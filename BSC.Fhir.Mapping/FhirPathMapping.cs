using System.Text.Json;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;

namespace BSC.Fhir.Mapping;

using BaseList = IReadOnlyCollection<Base>;

public record EvaluationResult(Base[] Result, Base? SourceResource, string? Name);

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

    public static EvaluationResult? EvaluateExpr(FhirPathExpression expr, string? expressionName = null)
    {
        var evaluationCtx = GetEvaluationContext(expr);
        if (evaluationCtx is null)
        {
            return null;
        }

        try
        {
            var compiler = new FhirPathCompiler(new SymbolTable().AddStandardFP().AddFhirExtensions());
            var cache = new FhirPathCompilerCache(compiler);
            var element = evaluationCtx.Resource?.ToTypedElement() ?? ElementNode.ForPrimitive(true);
            var result = cache
                .Select(
                    element,
                    evaluationCtx.Expression,
                    evaluationCtx.Resource is not null
                        ? new FhirEvaluationContext(ElementNodeExtensions.ToScopedNode(element))
                        : null
                )
                .ToFhirValues()
                .ToArray();

            return new EvaluationResult(result, evaluationCtx.Resource, expressionName);
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

    public static Type? EvaluateTypeFromExpr(FhirPathExpression expr)
    {
        var evaluationCtx = GetEvaluationContext(expr);

        if (evaluationCtx?.Resource is not null)
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

        return null;
    }

    private static EvaluationContext? GetEvaluationContext(FhirPathExpression expr)
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
            evaluationCtx = new(expr.Expression, context.Value?.FirstOrDefault());
        }
        else
        {
            Console.WriteLine("Error: Could not find evaluation context for expression {0}", expr);
            evaluationCtx = new(expr.Expression);
        }

        return evaluationCtx;
    }

    private static EvaluationContext ResourceEvaluationSource(string expr, Scope scope)
    {
        return new(expr, scope.ResponseItem);
    }

    private static EvaluationContext QuestionnaireEvaluationSource(string[] exprParts, Scope scope)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        return new(execExpr, scope.Questionnaire);
    }

    private static EvaluationContext ContextEvaluationSource(string[] exprParts, Scope scope)
    {
        exprParts[0] = "%resource";
        var execExpr = string.Join('.', exprParts);

        Base? source = scope.ResponseItem as Base ?? scope.ResponseItem;

        return new(execExpr, source);
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

        return new(execExpr, source);
    }

    private static EvaluationContext? VariableEvaluationSource(string[] exprParts, FhirPathExpression expression)
    {
        EvaluationContext? evaluationCtx = null;
        var variableName = exprParts[0][1..];
        if (
            expression.Dependencies.FirstOrDefault(dep => dep.Resolved() && dep.Name == variableName)
                is IQuestionnaireContext<BaseList> context
            && context.Value is not null
        )
        {
            exprParts[0] = "%resource";
            var execExpr = string.Join('.', exprParts);
            evaluationCtx = new(execExpr, context.Value.First());
        }
        else
        {
            Console.WriteLine("Error: Cannot find context in scope for variable {0}", variableName);
        }

        return evaluationCtx;
    }
}
