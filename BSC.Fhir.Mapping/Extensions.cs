using System.Collections;
using System.Reflection;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;

namespace BSC.Fhir.Mapping;

public static class MappingExtenstions
{
    public static bool HasAnswers(this QuestionnaireResponse.ItemComponent responseItem)
    {
        return responseItem.Answer.Count > 0 || responseItem.Item.Any(item => item.HasAnswers());
    }

    public static DataType? ItemExtractionContextExtractionValue(this IEnumerable<Extension> extensions)
    {
        var extension = extensions.SingleOrDefault(e => e.Url == Constants.EXTRACTION_CONTEXT);

        return extension?.Value;
    }

    public static Resource? CreateResourceFromExtension(string extensionValue)
    {
        var resourceName = extensionValue.Split('?').First();

        var className = $"Hl7.Fhir.Model.{resourceName[0..1].ToUpper() + resourceName[1..]}";

        var asm = AppDomain
            .CurrentDomain.GetAssemblies()
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

        return typeof(IList).IsAssignableFrom(type);
    }

    public static Expression? InitialExpression(this Questionnaire.ItemComponent questionnaireItem)
    {
        return questionnaireItem
                .Extension.FirstOrDefault(extension => extension.Url == Constants.INITIAL_EXPRESSION)
                ?.Value as Expression;
    }

    public static Coding EnumCodeToCoding<T>(Code<T> baseObj)
        where T : struct, Enum
    {
        var enumType = typeof(T);

        var code = baseObj.ToString();

        var field = enumType
            .GetFields()
            .FirstOrDefault(f => f.GetCustomAttribute<EnumLiteralAttribute>()?.Literal == code);

        if (field is null)
        {
            throw new InvalidOperationException($"Could not find field for code {code} on type {enumType}");
        }

        var system = field.GetCustomAttribute<EnumLiteralAttribute>()?.System;
        var display = field.GetCustomAttribute<DescriptionAttribute>()?.Description;
        if (system is null || display is null)
        {
            throw new InvalidOperationException(
                $"Could not find system or display value for code {code} on type {enumType}"
            );
        }

        return new Coding(system, code, display);
    }

    public static DataType? AsExpectedType(
        this Base baseObj,
        Questionnaire.QuestionnaireItemType questionnaireItemType,
        Type? sourceType = null
    )
    {
        // TODO(jaco): we should look at doing further parsing of values that turn into codings
        return questionnaireItemType switch
        {
            Questionnaire.QuestionnaireItemType.Choice
                when baseObj.GetType() is Type t && t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Code<>)
                => EnumCodeToCoding((dynamic)baseObj),
            Questionnaire.QuestionnaireItemType.Text when baseObj is Id id => new FhirString(id.Value),
            Questionnaire.QuestionnaireItemType.Reference when baseObj is Id id && sourceType is not null
                => new ResourceReference($"{ModelInfo.GetFhirTypeNameForType(sourceType)}/{id.Value}"),
            Questionnaire.QuestionnaireItemType.Reference when baseObj is FhirString str && sourceType is not null
                => new ResourceReference($"{ModelInfo.GetFhirTypeNameForType(sourceType)}/{str.Value}"),
            Questionnaire.QuestionnaireItemType.Reference when sourceType is null
                => throw new InvalidOperationException("Could not create ResourceReference"),
            _ when baseObj is CodeableConcept codeableConcept => codeableConcept.Coding.First(),
            _ => baseObj as DataType,
        };
        // if (baseObj is Id id)
        // {
        //     return sourceType is null
        //         ? new FhirString(id.Value)
        //         : new ResourceReference($"{ModelInfo.GetFhirTypeNameForType(sourceType)}/{id.Value}");
        // }
        // else if (baseObj is CodeableConcept codeableConcept)
        // {
        //     return codeableConcept.Coding.First();
        // }
        // else
        // {
        //     return baseObj as DataType;
        // }
    }

    public static IReadOnlyCollection<Expression> GetPopulationContextExpressions(this Questionnaire questionnaire)
    {
        var expression =
            questionnaire.Extension.FirstOrDefault(extension => extension.Url == Constants.POPULATION_CONTEXT)?.Value
            as Expression;

        var nestedExpessions = questionnaire.Item.SelectMany(item => item.GetPopulationContextExpressions()).ToArray();

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
            questionnaireItem
                .Extension.FirstOrDefault(extension => extension.Url == Constants.POPULATION_CONTEXT)
                ?.Value as Expression;

        var nestedExpessions = questionnaireItem
            .Item.SelectMany(item => item.GetPopulationContextExpressions())
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

    public static Expression? PopulationContext(this Questionnaire.ItemComponent questionnaireItem)
    {
        return (questionnaireItem as IExtendable).PopulationContext();
    }

    public static Expression? PopulationContext(this Questionnaire questionnaire)
    {
        return (questionnaire as IExtendable).PopulationContext();
    }

    public static Expression? PopulationContext(this IExtendable value)
    {
        var extension = value.GetExtension(Constants.POPULATION_CONTEXT);

        return extension?.Value as Expression;
    }
}
