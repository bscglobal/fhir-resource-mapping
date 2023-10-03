/*
 *
 *  This implementation is based on the Android FHIR implementation:
 *  https://github.com/google/android-fhir/blob/master/datacapture/src/main/java/com/google/android/fhir/datacapture/mapping/ResourceMapper.kt
 *
 */

using System.Reflection;
using System.Text.Json;
using BSC.Fhir.Mapping.Core;
using Hl7.Fhir.Model;
using FhirPath = Hl7.Fhir.FhirPath;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping;

public static class ResourceMapper
{
    public static async Task<Bundle> Extract(
        Questionnaire questionnaire,
        QuestionnaireResponse questionnaireResponse,
        MappingContext extractionContext,
        IProfileLoader? profileLoader = null,
        CancellationToken cancellationToken = default
    )
    {
        return await ExtractByDefinition(
            questionnaire,
            questionnaireResponse,
            extractionContext,
            profileLoader,
            cancellationToken
        );
    }

    private static async Task<Bundle> ExtractByDefinition(
        Questionnaire questionnaire,
        QuestionnaireResponse questionnaireResponse,
        MappingContext extractionContext,
        IProfileLoader? profileLoader = null,
        CancellationToken cancellationToken = default
    )
    {
        extractionContext.Questionnaire = questionnaire;
        var rootResource = questionnaire.CreateResource(extractionContext);
        var extractedResources = new List<Resource>();
        if (rootResource is not null)
        {
            extractionContext.SetCurrentContext(rootResource);
        }

        await ExtractByDefinition(
            questionnaire.Item,
            questionnaireResponse.Item,
            extractionContext,
            extractedResources,
            new CachingProfileLoader(profileLoader),
            cancellationToken
        );

        if (rootResource is not null)
        {
            extractedResources.Add(rootResource);
            extractionContext.RemoveContext();
        }
        extractionContext.Questionnaire = null;

        return new Bundle
        {
            Type = Bundle.BundleType.Transaction,
            Entry = extractedResources.Select(resource => new Bundle.EntryComponent { Resource = resource }).ToList()
        };
    }

    private static async Task ExtractByDefinition(
        IReadOnlyCollection<Questionnaire.ItemComponent> questionnaireItems,
        IReadOnlyCollection<QuestionnaireResponse.ItemComponent> questionnaireResponseItems,
        MappingContext extractionContext,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        var questionnaireItemsEnumarator = questionnaireItems.AsEnumerable().GetEnumerator();
        var questionnaireResponseItemsEnumarator = questionnaireResponseItems.AsEnumerable().GetEnumerator();

        while (questionnaireItemsEnumarator.MoveNext() && questionnaireResponseItemsEnumarator.MoveNext())
        {
            var currentResponseItem = questionnaireResponseItemsEnumarator.Current;
            var currentQuestionnaireItem = questionnaireItemsEnumarator.Current;

            while (
                currentQuestionnaireItem.LinkId != currentResponseItem.LinkId && questionnaireItemsEnumarator.MoveNext()
            )
            {
                currentQuestionnaireItem = questionnaireItemsEnumarator.Current;
            }

            if (currentQuestionnaireItem.LinkId == currentResponseItem.LinkId)
            {
                await ExtractByDefinition(
                    currentQuestionnaireItem,
                    currentResponseItem,
                    extractionContext,
                    extractionResult,
                    profileLoader,
                    cancellationToken
                );
            }
        }
    }

    private static async Task ExtractByDefinition(
        Questionnaire.ItemComponent questionnaireItem,
        QuestionnaireResponse.ItemComponent questionnaireResponseItem,
        MappingContext extractionContext,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (questionnaireItem.Type is Questionnaire.QuestionnaireItemType.Group)
        {
            if (questionnaireItem.Extension.ItemExtractionContextExtractionValue() is not null)
            {
                await ExtractResourceByDefinition(
                    questionnaireItem,
                    questionnaireResponseItem,
                    extractionContext,
                    extractionResult,
                    profileLoader,
                    cancellationToken
                );
            }
            else if (questionnaireItem.Definition is not null)
            {
                if (extractionContext.CurrentContext is null)
                {
                    throw new InvalidOperationException(
                        $"No extraction context defined for {questionnaireItem.Definition}"
                    );
                }
                await ExtractComplexTypeValueByDefinition(
                    questionnaireItem,
                    questionnaireResponseItem,
                    extractionContext,
                    extractionResult,
                    profileLoader,
                    cancellationToken
                );
            }
            else
            {
                await ExtractByDefinition(
                    questionnaireItem.Item,
                    questionnaireResponseItem.Item,
                    extractionContext,
                    extractionResult,
                    profileLoader,
                    cancellationToken
                );
            }
        }
        else if (questionnaireItem.Definition is not null)
        {
            if (extractionContext is null)
            {
                throw new InvalidOperationException(
                    $"No extraction context defined for {questionnaireItem.Definition}"
                );
            }

            ExtractPrimitiveTypeValueByDefinition(
                questionnaireItem,
                questionnaireResponseItem,
                extractionContext,
                profileLoader,
                cancellationToken
            );
        }
    }

    private static async Task ExtractResourceByDefinition(
        Questionnaire.ItemComponent questionnaireItem,
        QuestionnaireResponse.ItemComponent questionnaireResponseItem,
        MappingContext extractionContext,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        var resource = questionnaireItem.CreateResource(extractionContext);

        if (resource is null)
        {
            throw new InvalidOperationException("Unable to create a resource from questionnaire item");
        }

        extractionContext.SetCurrentContext(resource);

        await ExtractByDefinition(
            questionnaireItem.Item,
            questionnaireResponseItem.Item,
            extractionContext,
            extractionResult,
            profileLoader,
            cancellationToken
        );

        extractionResult.Add(resource);
        extractionContext.RemoveContext();
    }

    private static async Task ExtractComplexTypeValueByDefinition(
        Questionnaire.ItemComponent questionnaireItem,
        QuestionnaireResponse.ItemComponent questionnaireResponseItem,
        MappingContext extractionContext,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (extractionContext.CurrentContext is null)
        {
            throw new ArgumentException("ExtractionContext.CurrentContext is null", nameof(extractionContext));
        }

        var fieldName = FieldNameByDefinition(questionnaireItem.Definition);
        var fieldInfo = extractionContext.CurrentContext.GetType().GetProperty(fieldName);

        var definition = questionnaireItem.Definition;
        if (fieldInfo is null)
        {
            var poundIndex = definition.LastIndexOf('#');
            if (poundIndex < 0)
            {
                Console.WriteLine("Could not find pound index for [{0}]", definition);
                return;
            }
            var canonicalUrl = definition[..poundIndex];
            var profile = await profileLoader.LoadProfileAsync(new Canonical(canonicalUrl));

            if (profile is not null)
            {
                UseProfile(
                    questionnaireItem,
                    questionnaireResponseItem,
                    fieldName,
                    profile,
                    extractionContext,
                    definition,
                    poundIndex
                );
            }
            else
            {
                Console.WriteLine("profile null for [{0}]", definition);
            }

            return;
        }

        var type = fieldInfo.NonParameterizedType();
        var value = Activator.CreateInstance(type) as Base;
        if (value is null)
        {
            throw new InvalidOperationException($"Unable to create instance of {type.Name}");
        }

        if (fieldInfo.IsNonStringEnumerable())
        {
            var val = fieldInfo.GetValue(extractionContext.CurrentContext);
            fieldInfo.PropertyType.GetMethod("Add")?.Invoke(val, new[] { value });
        }
        else
        {
            fieldInfo.SetValue(extractionContext.CurrentContext, value);
        }

        extractionContext.SetCurrentContext(value);

        await ExtractByDefinition(
            questionnaireItem.Item,
            questionnaireResponseItem.Item,
            extractionContext,
            extractionResult,
            profileLoader,
            cancellationToken
        );

        extractionContext.RemoveContext();
    }

    private static void ExtractPrimitiveTypeValueByDefinition(
        Questionnaire.ItemComponent questionnaireItem,
        QuestionnaireResponse.ItemComponent questionnaireResponseItem,
        MappingContext context,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (context.CurrentContext is null)
        {
            throw new ArgumentException("ExtractionContext.CurrentContext is null", nameof(context));
        }

        if (questionnaireResponseItem.Answer.Count == 0)
        {
            return;
        }

        var definition = questionnaireItem.Definition;

        var fieldName = FieldNameByDefinition(definition, true);

        var contextType = context.CurrentContext.GetType();
        var field = contextType.GetProperty(fieldName);

        if (field is null)
        {
            fieldName = FieldNameByDefinition(definition, false);
            field = contextType.GetProperty(fieldName);
        }

        var calculatedValue = questionnaireItem.CalculatedExpressionResult(context);

        if (field is not null)
        {
            if (field.NonParameterizedType().IsEnum)
            {
                UpdateFieldWithEnum(context.CurrentContext, field, questionnaireResponseItem.Answer.First().Value);
            }
            else
            {
                // NOTE: Should we overwrite submitted answer with calculated value or visa versa?
                UpdateField(
                    context.CurrentContext,
                    field,
                    calculatedValue?.Select(calc => calc as DataType).OfType<DataType>()
                        ?? questionnaireResponseItem.Answer.Select(ans => ans.Value)
                );
            }

            return;
        }
    }

    private static void UseProfile(
        Questionnaire.ItemComponent questionnaireItem,
        QuestionnaireResponse.ItemComponent questionnaireResponseItem,
        string fieldName,
        StructureDefinition profile,
        MappingContext context,
        string? definition = null,
        int? poundIndex = null
    )
    {
        if (context.CurrentContext is null)
        {
            throw new ArgumentException(nameof(context), "CurrentContext in MappingContext is null");
        }

        definition ??= questionnaireItem.Definition;
        poundIndex ??= definition.LastIndexOf('#');

        if (poundIndex is null)
        {
            throw new ArgumentException("Invalid definition");
        }

        if (fieldName.Contains(':'))
        {
            var colonIndex = definition.LastIndexOf(':');
            var typeToCheck = definition[(poundIndex.Value + 1)..colonIndex];

            var sliceName = definition[(colonIndex + 1)..];

            if (IsSliceSupportedByProfile(profile, typeToCheck, sliceName))
            {
                ExtractSlice(questionnaireItem, questionnaireResponseItem, profile, context, typeToCheck, sliceName);
            }
        }
        else
        {
            var extensionForType = definition[(poundIndex.Value + 1)..definition.LastIndexOf('.')];

            if (IsExtensionSupportedByProfile(profile, extensionForType, fieldName))
            {
                AddDefinitionBasedCustomExtension(questionnaireItem, questionnaireResponseItem, context.CurrentContext);
            }
        }
    }

    private static void ExtractSlice(
        Questionnaire.ItemComponent questionnaireItem,
        QuestionnaireResponse.ItemComponent questionnaireResponseItem,
        StructureDefinition profile,
        MappingContext context,
        string baseType,
        string sliceName
    )
    {
        Console.WriteLine("extracting slice");
        var elementEnumerator = profile.Snapshot.Element.GetEnumerator();
        elementEnumerator.MoveNext();

        // TODO: Check if this works
        while (elementEnumerator.Current.Slicing is null || elementEnumerator.Current.Path != baseType)
        {
            elementEnumerator.MoveNext();
        }

        // TODO: check for slices with the name equal to `sliceName`
        var discriminators = elementEnumerator.Current.Slicing!.Discriminator;
        Console.WriteLine(JsonSerializer.Serialize(discriminators, new JsonSerializerOptions { WriteIndented = true }));

        SliceDefinition? slice = null;

        while (elementEnumerator.Current is not null && elementEnumerator.Current.Path.StartsWith(baseType))
        {
            elementEnumerator.MoveNext();
            var currentSliceName = elementEnumerator.Current.SliceName;

            if (string.IsNullOrEmpty(currentSliceName))
            {
                throw new InvalidOperationException("Expected ElementDefinition with sliceName");
            }

            if (currentSliceName == sliceName)
            {
                slice = new SliceDefinition(elementEnumerator.Current.SliceName);
            }

            elementEnumerator.MoveNext();
            while (elementEnumerator.Current is not null && string.IsNullOrEmpty(elementEnumerator.Current.SliceName))
            {
                slice?.Fixed.Add(elementEnumerator.Current.Fixed);
                slice?.Pattern.Add(elementEnumerator.Current.Pattern);

                elementEnumerator.MoveNext();
            }
        }

        Console.WriteLine(JsonSerializer.Serialize(slice, new JsonSerializerOptions { WriteIndented = true }));

        if (slice is null)
        {
            throw new InvalidOperationException("Could not find matching slice in profile");
        }

        throw new NotImplementedException();
    }

    private static void UpdateField(Base resource, PropertyInfo field, IEnumerable<DataType> answers)
    {
        var fieldType = field.PropertyType;
        var answersOfFieldType = answers.Select(ans => WrapAnswerInFieldType(ans, fieldType)).ToArray();

        if (field.IsParameterized() && fieldType.IsNonStringEnumerable())
        {
            AddAnswerToListField(resource, field, answersOfFieldType);
        }
        else
        {
            SetFieldElementValue(resource, field, answersOfFieldType.First());
        }
    }

    private static void SetFieldElementValue(Base baseResource, PropertyInfo field, DataType answerValue)
    {
        field.SetValue(baseResource, answerValue);
    }

    private static void AddAnswerToListField(
        Base baseResource,
        PropertyInfo fieldInfo,
        IReadOnlyCollection<DataType> answerValue
    )
    {
        var propName = fieldInfo.Name;
        var field = fieldInfo.GetValue(baseResource);
        var method = field?.GetType().GetMethod("Add");

        if (method is null)
        {
            return;
        }

        foreach (var answer in answerValue)
        {
            method.Invoke(field, new[] { answer });
        }
    }

    private static void UpdateFieldWithEnum(Base baseResource, PropertyInfo field, Base value)
    {
        var fieldType = field.NonParameterizedType();
        var stringValue = value switch
        {
            Coding coding => coding.Code,
            _ => field.ToString()
        };

        if (string.IsNullOrEmpty(stringValue))
        {
            return;
        }
        // FIXME: this probably isn't going to work
        field.SetValue(baseResource, Enum.Parse(fieldType, stringValue));
    }

    private static DataType WrapAnswerInFieldType(DataType answer, Type fieldType, string? system = null)
    {
        var type = fieldType.NonParameterizedType();
        if (type == typeof(CodeableConcept) && answer is Coding coding)
        {
            return new CodeableConcept { Coding = { coding }, Text = coding.Display, };
        }
        else if (type == typeof(Id) && answer is FhirString idStr)
        {
            return new Id(idStr.Value);
        }
        else if (type == typeof(Code))
        {
            if (answer is Coding code)
            {
                return new Code(code.Code);
            }
            else if (answer is FhirString codeStr)
            {
                return new Code(codeStr.Value);
            }
        }
        else if (type == typeof(FhirUri) && answer is FhirString uriStr)
        {
            return new FhirUri(uriStr.Value);
        }
        else if (type == typeof(FhirDecimal) && answer is Integer decInt)
        {
            return new FhirDecimal(decInt.Value);
        }

        return answer;
    }

    private static string FieldNameByDefinition(string definition, bool isElement = false)
    {
        var last = definition.Substring(definition.LastIndexOf('.') + 1);

        if (string.IsNullOrEmpty(last))
        {
            throw new InvalidOperationException($"Invalid field definition: {definition}");
        }

        last = last[0..1].ToUpper() + last[1..];
        if (isElement)
        {
            last += "Element";
        }

        return last;
    }

    private static bool IsExtensionSupportedByProfile(
        StructureDefinition profile,
        string extensionForType,
        ReadOnlySpan<char> fieldName
    )
    {
        var elementDefinitions = profile.Snapshot.Element.Where(
            element => element.Path == $"{extensionForType}.extension"
        );
        foreach (var definition in elementDefinitions)
        {
            if (
                MemoryExtensions.Equals(
                    fieldName,
                    definition.ElementId.AsSpan()[(definition.ElementId.LastIndexOf(':') + 1)..],
                    StringComparison.Ordinal
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSliceSupportedByProfile(StructureDefinition profile, string typeToCheck, string sliceName)
    {
        var val = profile.Snapshot.Element
            .Where(element => element.Path == typeToCheck)
            .Any(element => element.SliceName == sliceName);

        return val;
    }

    private static void AddDefinitionBasedCustomExtension(
        Questionnaire.ItemComponent questionnaireItem,
        QuestionnaireResponse.ItemComponent questionnaireResponseItem,
        Base baseObj
    )
    {
        if (baseObj is DataType dataType)
        {
            dataType.AddExtension(questionnaireItem.Definition, questionnaireResponseItem.Answer.First().Value);
        }
        else if (baseObj is DomainResource domainResource)
        {
            domainResource.AddExtension(questionnaireItem.Definition, questionnaireResponseItem.Answer.First().Value);
        }
    }

    public static QuestionnaireResponse Populate(Questionnaire questionnaire, params Resource[] resources)
    {
        PopulateInitialValues(questionnaire.Item.ToArray(), resources);

        return new QuestionnaireResponse
        {
            Item = questionnaire.Item.Select(item => item.CreateQuestionnaireResponseItem()).ToList()
        };
    }

    private static void PopulateInitialValues(Questionnaire.ItemComponent[] questionnaireItems, Resource[] resources)
    {
        foreach (var item in questionnaireItems)
        {
            PopulateInitalValue(item, resources);
        }
    }

    private static void PopulateInitalValue(Questionnaire.ItemComponent questionnaireItem, Resource[] resources)
    {
        var initialExpression = questionnaireItem.InitialExpression();
        if (!(questionnaireItem.Initial.Count == 0 || initialExpression is null))
        {
            throw new InvalidOperationException(
                "QuestionnaireItem is not allowed to have both intial.value and initial expression. See rule at http://build.fhir.org/ig/HL7/sdc/expressions.html#initialExpression"
            );
        }

        if (initialExpression is not null)
        {
            var populationContext = SelectPopulationContext(resources, initialExpression.Expression_);
            if (populationContext is null)
            {
                return;
            }

            // TODO: Should this return null if there is more than one result?
            var exp = initialExpression.Expression_.TrimStart('%');
            exp = exp[0..1].ToUpper() + exp[1..];
            var evalResult = FhirPath.FhirPathExtensions.Select(populationContext, exp);

            questionnaireItem.Initial = (questionnaireItem.Repeats ?? false) switch
            {
                false
                    => evalResult.SingleOrDefault() switch
                    {
                        null => null,
                        var x => new() { new() { Value = x.AsExpectedType() } }
                    },
                true
                    => evalResult
                        .Select(result => new Questionnaire.InitialComponent() { Value = result.AsExpectedType() })
                        .ToList()
            };
        }

        PopulateInitialValues(questionnaireItem.Item.ToArray(), resources);
    }

    public static Resource? SelectPopulationContext(IReadOnlyCollection<Resource> resources, string initialExpression)
    {
        var resourceType = initialExpression.Split('.').FirstOrDefault()?.TrimStart('%');

        if (resourceType is null)
        {
            return null;
        }

        return resources.SingleOrDefault(resource => resource.TypeName.ToLower() == resourceType.ToLower())
            ?? resources.FirstOrDefault();
    }
}
