using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using FluentAssertions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests.Expressions;

public class ScopeTests
{
    [Fact]
    public void HasRequiredAnswers_ReturnsTrue_WhenInitialAnswersArePresent_OnRequiredField()
    {
        // Arrange
        var questionnaireItem = new Questionnaire.ItemComponent
        {
            LinkId = "1",
            Item =
            {
                new()
                {
                    LinkId = "1.1",
                    Required = true,
                    Initial = { new Questionnaire.InitialComponent { Value = new FhirString("answer1") } }
                }
            }
        };

        var questionnaireResponseItem = new QuestionnaireResponse.ItemComponent
        {
            LinkId = "1",
            Item = { new() { LinkId = "1.1", } }
        };

        var idProvider = new NumericIdProvider();

        var parentScope = new Scope(new Questionnaire(), new QuestionnaireResponse(), idProvider);
        var scope1 = new Scope(parentScope, questionnaireItem, questionnaireResponseItem, idProvider);
        var scope1_1 = new Scope(scope1, questionnaireItem.Item[0], questionnaireResponseItem.Item[0], idProvider);

        // Act
        var result = scope1.HasRequiredAnswers();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasRequiredAnswers_ReturnsFalse_WhenInitialAnswersArePresent_OnNonRequiredField()
    {
        // Arrange
        var questionnaireItem = new Questionnaire.ItemComponent
        {
            LinkId = "1",
            Item =
            {
                new()
                {
                    LinkId = "1.1",
                    Required = false,
                    Initial = { new Questionnaire.InitialComponent { Value = new FhirString("answer1") } }
                }
            }
        };

        var questionnaireResponseItem = new QuestionnaireResponse.ItemComponent
        {
            LinkId = "1",
            Item = { new() { LinkId = "1.1", } }
        };

        var idProvider = new NumericIdProvider();

        var parentScope = new Scope(new Questionnaire(), new QuestionnaireResponse(), idProvider);
        var scope1 = new Scope(parentScope, questionnaireItem, questionnaireResponseItem, idProvider);
        var scope1_1 = new Scope(scope1, questionnaireItem.Item[0], questionnaireResponseItem.Item[0], idProvider);

        // Act
        var result = scope1.HasRequiredAnswers();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasRequiredAnswers_ReturnsTrue_WhenCalculatedExpressionIsPresent_OnRequiredField()
    {
        // Arrange
        var questionnaireItem = new Questionnaire.ItemComponent
        {
            LinkId = "1",
            Item =
            {
                new()
                {
                    LinkId = "1.1",
                    Required = true,
                    Extension =
                    {
                        new() { Url = Constants.CALCULATED_EXPRESSION, Value = new Expression() }
                    }
                }
            }
        };

        var questionnaireResponseItem = new QuestionnaireResponse.ItemComponent
        {
            LinkId = "1",
            Item = { new() { LinkId = "1.1", } }
        };

        var idProvider = new NumericIdProvider();

        var parentScope = new Scope(new Questionnaire(), new QuestionnaireResponse(), idProvider);
        var scope1 = new Scope(parentScope, questionnaireItem, questionnaireResponseItem, idProvider);
        var scope1_1 = new Scope(scope1, questionnaireItem.Item[0], questionnaireResponseItem.Item[0], idProvider);
        scope1_1.Context.Add(
            new QuestionnaireExpression<IReadOnlyCollection<Base>>(
                idProvider.GetId(),
                null,
                "",
                "",
                scope1_1,
                QuestionnaireContextType.CalculatedExpression,
                null,
                null
            )
        );

        // Act
        var result = scope1.HasRequiredAnswers();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasRequiredAnswers_ReturnsFalse_WhenCalculatedExpressionIsPresent_OnNonRequiredField()
    {
        // Arrange
        var questionnaireItem = new Questionnaire.ItemComponent
        {
            LinkId = "1",
            Item =
            {
                new()
                {
                    LinkId = "1.1",
                    Required = false,
                    Extension =
                    {
                        new() { Url = Constants.CALCULATED_EXPRESSION, Value = new Expression() }
                    }
                }
            }
        };

        var questionnaireResponseItem = new QuestionnaireResponse.ItemComponent
        {
            LinkId = "1",
            Item = { new() { LinkId = "1.1", } }
        };

        var idProvider = new NumericIdProvider();

        var parentScope = new Scope(new Questionnaire(), new QuestionnaireResponse(), idProvider);
        var scope1 = new Scope(parentScope, questionnaireItem, questionnaireResponseItem, idProvider);
        var scope1_1 = new Scope(scope1, questionnaireItem.Item[0], questionnaireResponseItem.Item[0], idProvider);
        scope1_1.Context.Add(
            new QuestionnaireExpression<IReadOnlyCollection<Base>>(
                idProvider.GetId(),
                null,
                "",
                "",
                scope1_1,
                QuestionnaireContextType.CalculatedExpression,
                null,
                null
            )
        );

        // Act
        var result = scope1.HasRequiredAnswers();

        // Assert
        result.Should().BeFalse();
    }
}
