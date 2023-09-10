using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Tests;

public class ExtensionsTests
{
    private const string ITEM_CONTEXT_EXTENSION_URL =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext";
    private const string ITEM_INITIAL_EXPRESSION_URL =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression";

    [Theory]
    [InlineData("Patient", typeof(Patient))]
    [InlineData("Composition", typeof(Composition))]
    [InlineData("Observation", typeof(Observation))]
    public void CreateResource_CreatesCorrectResourceType(string expressionType, Type resourceType)
    {
        var questionaire = new Questionnaire();
        questionaire.SetExtension(
            ITEM_CONTEXT_EXTENSION_URL,
            new Expression
            {
                Language = "application/x-fhir-query",
                Expression_ = $"{expressionType}?_id={{patient.id}}"
            }
        );

        var resource = questionaire.CreateResource();

        Assert.IsType(resourceType, resource);
    }

    [Fact]
    public void IsNonStringEnumerable_TrueIfList()
    {
        var list = new List<string>();

        var actual = list.GetType().IsNonStringEnumerable();

        Assert.True(actual);
    }

    [Fact]
    public void IsNonStringEnumerable_FalseIfBool()
    {
        var value = true;

        var actual = value.GetType().IsNonStringEnumerable();

        Assert.False(actual);
    }

    [Fact]
    public void IsNonStringEnumerable_FalseIfString()
    {
        var value = "This is a string";

        var actual = value.GetType().IsNonStringEnumerable();

        Assert.False(actual);
    }

    [Fact]
    public void IsParameterizedType_TrueIfList()
    {
        var value = new TestClass();
        var fieldInfo = value.GetType().GetProperty("ParameterizedTypeField")!;

        var actual = fieldInfo.IsParameterized();

        Assert.True(actual);
    }

    [Fact]
    public void IsParameterizedType_FalseIfString()
    {
        var value = new TestClass();
        var fieldInfo = value.GetType().GetProperty("UnparameterizedTypeField")!;

        var actual = fieldInfo.IsParameterized();

        Assert.False(actual);
    }

    [Fact]
    public void NonParameterizedType_InnerTypeOfList()
    {
        var value = new TestClass();

        var fieldInfo = value.GetType().GetProperty("ParameterizedTypeField")!;
        var expected = typeof(string);
        var actual = fieldInfo.NonParameterizedType();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NonParameterizedType_TypeIfNotParameterized()
    {
        var value = new TestClass();

        var fieldInfo = value.GetType().GetProperty("UnparameterizedTypeField")!;
        var expected = typeof(string);
        var actual = fieldInfo.NonParameterizedType();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InitialExpression_ReturnsCorrectExpression()
    {
        var expression = new Expression
        {
            Language = "text/fhirpath",
            Expression_ = "%relative.id"
        };
        var extension = new Extension { Url = ITEM_INITIAL_EXPRESSION_URL, Value = expression };
        var questionaireItem = new Questionnaire.ItemComponent { Extension = { extension } };

        var actual = questionaireItem.InitialExpression();

        Assert.Same(expression, actual);
    }
}

class TestClass
{
    public List<string> ParameterizedTypeField { get; set; } = new();
    public string UnparameterizedTypeField { get; set; } = string.Empty;
}
