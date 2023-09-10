using System.Collections;
using System.Reflection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Utility;

namespace BSC.Fhir.Mapping;

public static class MappingExtenstions
{
    private const string ITEM_EXTRACTION_CONTEXT_EXTENSION_URL =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemExtractionContext";
    private const string ITEM_INITIAL_EXPRESSION_URL =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-initialExpression";
    private const string ITEM_POPULATION_CONTEXT_EXTENSION_URL =
        "http://hl7.org/fhir/uv/sdc/StructureDefinition/sdc-questionnaire-itemPopulationContext";

    public static string? ItemExtractionContextExtractionValue(
        this IEnumerable<Extension> extensions
    )
    {
        var extension = extensions.FirstOrDefault(
            e => e.Url == ITEM_EXTRACTION_CONTEXT_EXTENSION_URL
        );

        return extension?.Value switch
        {
            Expression expression => expression.Expression_,
            Code code => code.Value,
            _ => null
        };
    }

    public static Resource? CreateResource(this Questionnaire questionnaire)
    {
        var extensionValue = questionnaire.Extension.ItemExtractionContextExtractionValue();
        if (string.IsNullOrEmpty(extensionValue))
        {
            return null;
        }

        return CreateResourceFromExtension(extensionValue);
    }

    public static Resource? CreateResource(this Questionnaire.ItemComponent item)
    {
        var extensionValue = item.Extension.ItemExtractionContextExtractionValue();
        if (string.IsNullOrEmpty(extensionValue))
        {
            return null;
        }

        return CreateResourceFromExtension(extensionValue);
    }

    private static Resource? CreateResourceFromExtension(string extensionValue)
    {
        var resourceName = extensionValue.Split('?').First();

        var className = $"Hl7.Fhir.Model.{resourceName[0..1].ToUpper() + resourceName[1..]}";

        var asm = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(asm => asm.GetName().Name == "Hl7.Fhir.R4.Core");

        var type = asm?.GetType(className);

        if (type is null)
        {
            return null;
        }

        return Activator.CreateInstance(type) as Resource;
    }

    public static bool IsParameterized(this PropertyInfo field)
    {
        var fieldType = field.PropertyType;
        return fieldType.IsGenericType && fieldType.GetGenericArguments().Length > 0;
    }

    public static bool IsParameterized(this Type type)
    {
        return type.IsGenericType && type.GetGenericArguments().Length > 0;
    }

    public static Type NonParameterizedType(this PropertyInfo field)
    {
        var fieldType = field.PropertyType;

        if (!field.IsParameterized())
        {
            return fieldType;
        }

        return fieldType.GetGenericArguments().First();
    }

    public static Type NonParameterizedType(this Type type)
    {
        if (!type.IsParameterized())
        {
            return type;
        }

        return type.GetGenericArguments().First();
    }

    public static bool IsNonStringEnumerable(this PropertyInfo pi)
    {
        return pi != null && pi.PropertyType.IsNonStringEnumerable();
    }

    public static bool IsNonStringEnumerable(this object instance)
    {
        return instance != null && instance.GetType().IsNonStringEnumerable();
    }

    public static bool IsNonStringEnumerable(this Type type)
    {
        if (type == null || type == typeof(string))
        {
            return false;
        }

        return typeof(IEnumerable).IsAssignableFrom(type);
    }

    public static Expression? InitialExpression(this Questionnaire.ItemComponent questionnaireItem)
    {
        return questionnaireItem.Extension
                .FirstOrDefault(extension => extension.Url == ITEM_INITIAL_EXPRESSION_URL)
                ?.Value as Expression;
    }

    public static DataType? AsExpectedType(this Base baseObj)
    {
        // TODO(jaco): we should look at doing further parsing of values that turn into codings
        if (baseObj is Id id)
        {
            return new FhirString(id.Value);
        }
        else if (baseObj is CodeableConcept codeableConcept)
        {
            return codeableConcept.Coding.First();
        }
        else
        {
            return baseObj as DataType;
        }
    }

    public static IReadOnlyCollection<Expression> GetPopulationContextExpressions(
        this Questionnaire questionnaire
    )
    {
        var expression =
            questionnaire.Extension
                .FirstOrDefault(extension => extension.Url == ITEM_POPULATION_CONTEXT_EXTENSION_URL)
                ?.Value as Expression;

        var nestedExpessions = questionnaire.Item
            .SelectMany(item => item.GetPopulationContextExpressions())
            .ToArray();

        if (expression is null)
        {
            return nestedExpessions;
        }
        else
        {
            return nestedExpessions.Concat(new[] { expression }).ToArray();
        }
    }

    public static IReadOnlyCollection<Expression> GetPopulationContextExpressions(
        this Questionnaire.ItemComponent questionnaireItem
    )
    {
        var expression =
            questionnaireItem.Extension
                .FirstOrDefault(extension => extension.Url == ITEM_POPULATION_CONTEXT_EXTENSION_URL)
                ?.Value as Expression;

        var nestedExpessions = questionnaireItem.Item
            .SelectMany(item => item.GetPopulationContextExpressions())
            .ToArray();

        if (expression is null)
        {
            return nestedExpessions;
        }
        else
        {
            return nestedExpessions.Concat(new[] { expression }).ToArray();
        }
    }
}
