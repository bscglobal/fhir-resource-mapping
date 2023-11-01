using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping;

public class Populator
{
    public static QuestionnaireResponse Populate(Questionnaire questionnaire, MappingContext ctx)
    {
        var response = new QuestionnaireResponse();
        ctx.QuestionnaireResponse = response;

        CreateQuestionnaireResponseItems(questionnaire.Item, response.Item, ctx);

        return response;
    }

    private static void CreateQuestionnaireResponseItems(
        IReadOnlyCollection<Questionnaire.ItemComponent> questionnaireItems,
        List<QuestionnaireResponse.ItemComponent> responseItems,
        MappingContext ctx,
        bool debug = false
    )
    {
        var responses = questionnaireItems.SelectMany(item =>
        {
            if (debug)
            {
                Console.WriteLine("Debug: CreatingQuestionnaireResponseItems for {0}", item.LinkId);
            }
            ctx.SetQuestionnaireItem(item);
            var responseItem = GenerateQuestionnaireResponseItem(ctx);
            ctx.PopQuestionnaireItem();
            return responseItem;
        });

        responseItems.AddRange(responses);
    }

    private static QuestionnaireResponse.ItemComponent[] GenerateQuestionnaireResponseItem(MappingContext ctx)
    {
        QuestionnaireResponse.ItemComponent[]? responseItems = null;
        if (ctx.QuestionnaireItem.Type == Questionnaire.QuestionnaireItemType.Group)
        {
            responseItems = CreateGroupQuestionnaireResponseItem(ctx);
        }
        else
        {
            var questionnaireResponseItem = new QuestionnaireResponse.ItemComponent
            {
                LinkId = ctx.QuestionnaireItem.LinkId,
                Answer = CreateQuestionnaireResponseItemAnswers(ctx)
            };

            responseItems = new[] { questionnaireResponseItem };
        }

        return responseItems ?? Array.Empty<QuestionnaireResponse.ItemComponent>();
    }

    private static QuestionnaireResponse.ItemComponent[]? CreateGroupQuestionnaireResponseItem(MappingContext ctx)
    {
        var populationContextExpression = ctx.QuestionnaireItem.PopulationContext();

        if (populationContextExpression is not null)
        {
            var tempContext = true;
            if (!ctx.CurrentContext.TryGetValue(populationContextExpression.Name, out var context))
            {
                EvaluationResult? result;

                try
                {
                    // result = FhirPathMapping.EvaluateExpr(populationContextExpression.Expression_, ctx);
                    result = null;
                }
                catch
                {
                    result = null;
                }
                if (result is null)
                {
                    Console.WriteLine(
                        "Warning: could not resolve expression {0} for {1}, with name {2}",
                        populationContextExpression.Expression_,
                        ctx.QuestionnaireItem.LinkId,
                        populationContextExpression.Name
                    );
                    return null;
                }

                context = new(result.Result, populationContextExpression.Name);
            }
            else
            {
                tempContext = false;
                ctx.RemoveContext(populationContextExpression.Name);
            }

            var responseItems = context.Value
                .Select(value =>
                {
                    var questionnaireResponseItem = new QuestionnaireResponse.ItemComponent
                    {
                        LinkId = ctx.QuestionnaireItem.LinkId
                    };
                    ctx.AddContext(populationContextExpression.Name, new(value, populationContextExpression.Name));
                    ctx.SetQuestionnaireResponseItem(questionnaireResponseItem);

                    CreateQuestionnaireResponseItems(ctx.QuestionnaireItem.Item, questionnaireResponseItem.Item, ctx);
                    ctx.PopQuestionnaireResponseItem();
                    ctx.RemoveContext(populationContextExpression.Name);

                    return questionnaireResponseItem;
                })
                .ToArray();

            if (!tempContext)
            {
                ctx.AddContext(populationContextExpression.Name, context);
            }

            return responseItems;
        }

        Console.WriteLine("Warning: could not find population context for group {0}", ctx.QuestionnaireItem.LinkId);
        return null;
    }

    private static List<QuestionnaireResponse.AnswerComponent>? CreateQuestionnaireResponseItemAnswers(
        MappingContext ctx
    )
    {
        if (ctx.QuestionnaireItem.Type == Questionnaire.QuestionnaireItemType.Group)
        {
            return null;
        }

        var initialExpression = ctx.QuestionnaireItem.InitialExpression();
        if (!(ctx.QuestionnaireItem.Initial.Count == 0 || initialExpression is null))
        {
            throw new InvalidOperationException(
                "QuestionnaireItem is not allowed to have both intial.value and initial expression. See rule at http://build.fhir.org/ig/HL7/sdc/expressions.html#initialExpression"
            );
        }

        if (initialExpression is not null)
        {
            // var evalResult = FhirPathMapping.EvaluateExpr(initialExpression.Expression_, ctx);
            EvaluationResult? evalResult = null;

            if (evalResult is null)
            {
                Console.WriteLine("Could not find a value for {0}", initialExpression.Expression_);
            }
            else
            {
                if (ctx.QuestionnaireItem.Repeats ?? false)
                {
                    return evalResult.Result
                        .Select(
                            result =>
                                new QuestionnaireResponse.AnswerComponent()
                                {
                                    Value = result.AsExpectedType(
                                        ctx.QuestionnaireItem.Type ?? Questionnaire.QuestionnaireItemType.Text,
                                        evalResult.SourceResource is Resource resource ? resource.GetType() : null
                                    )
                                }
                        )
                        .ToList();
                }
                else if (evalResult.Result.Length > 1)
                {
                    Console.WriteLine(
                        "Warning: expression {0} resolved to more than one answer. LinkId: {1}",
                        initialExpression.Expression_,
                        ctx.QuestionnaireItem.LinkId
                    );
                }
                else
                {
                    return evalResult.Result.SingleOrDefault() switch
                    {
                        null => null,
                        var x
                            => new()
                            {
                                new()
                                {
                                    Value = x.AsExpectedType(
                                        ctx.QuestionnaireItem.Type ?? Questionnaire.QuestionnaireItemType.Text,
                                        evalResult.SourceResource is Resource resource ? resource.GetType() : null
                                    )
                                }
                            }
                    };
                }
            }
        }

        return null;
    }
}
