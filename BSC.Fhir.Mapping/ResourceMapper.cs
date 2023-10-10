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
        foreach (var questionnaireItem in questionnaireItems)
        {
            var responseItems = questionnaireResponseItems
                .Where(responseItem => responseItem.LinkId == questionnaireItem.LinkId)
                .ToArray();

            if (!questionnaireItem.Repeats ?? false && responseItems.Length > 1)
            {
                Console.WriteLine(
                    "Error: QuestionnaireResponse should not have more than one (1) answer for '{0}'",
                    questionnaireItem.LinkId
                );
                continue;
            }

            foreach (var responseItem in responseItems)
            {
                extractionContext.QuestionnaireItem = questionnaireItem;
                extractionContext.QuestionnaireResponseItem = responseItem;
                await ExtractByDefinition(extractionContext, extractionResult, profileLoader, cancellationToken);
            }
        }
    }

    private static async Task ExtractByDefinition(
        MappingContext ctx,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (ctx.QuestionnaireItem.Type == Questionnaire.QuestionnaireItemType.Group)
        {
            if (ctx.QuestionnaireItem.Extension.ItemExtractionContextExtractionValue() is not null)
            {
                await ExtractResourceByDefinition(ctx, extractionResult, profileLoader, cancellationToken);
            }
            else if (ctx.QuestionnaireItem.Definition is not null)
            {
                if (ctx.CurrentContext is null)
                {
                    throw new InvalidOperationException(
                        $"No extraction context defined for {ctx.QuestionnaireItem.Definition}"
                    );
                }
                await ExtractComplexTypeValueByDefinition(ctx, extractionResult, profileLoader, cancellationToken);
            }
            else
            {
                await ExtractByDefinition(
                    ctx.QuestionnaireItem.Item,
                    ctx.QuestionnaireResponseItem.Item,
                    ctx,
                    extractionResult,
                    profileLoader,
                    cancellationToken
                );
            }
        }
        else if (ctx.QuestionnaireItem.Definition is not null)
        {
            if (ctx.CurrentContext is null)
            {
                throw new InvalidOperationException(
                    $"No extraction context defined for {ctx.QuestionnaireItem.Definition}"
                );
            }

            await ExtractPrimitiveTypeValueByDefinition(ctx, profileLoader, cancellationToken);
        }
        else
        {
            Console.WriteLine(
                "Error: Could not extract questionnaire item with LinkId {0}",
                ctx.QuestionnaireItem.LinkId
            );
        }
    }

    private static async Task ExtractResourceByDefinition(
        MappingContext ctx,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        var resource = ctx.QuestionnaireItem.CreateResource(ctx);

        if (resource is null)
        {
            throw new InvalidOperationException("Unable to create a resource from questionnaire item");
        }

        ctx.SetCurrentContext(resource);

        await ExtractByDefinition(
            ctx.QuestionnaireItem.Item,
            ctx.QuestionnaireResponseItem.Item,
            ctx,
            extractionResult,
            profileLoader,
            cancellationToken
        );

        extractionResult.Add(resource);
        ctx.RemoveContext();
    }

    private static async Task ExtractComplexTypeValueByDefinition(
        MappingContext ctx,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (ctx.CurrentContext is null)
        {
            throw new ArgumentException("ExtractionContext.CurrentContext is null", nameof(ctx));
        }

        var fieldName = FieldNameByDefinition(ctx.QuestionnaireItem.Definition);
        var fieldInfo = ctx.CurrentContext.GetType().GetProperty(fieldName);

        var definition = ctx.QuestionnaireItem.Definition;
        if (fieldInfo is null)
        {
            await UseSliceFromProfile(fieldName, ctx, extractionResult, profileLoader, cancellationToken);
        }
        else
        {
            var type = fieldInfo.NonParameterizedType();
            var value = Activator.CreateInstance(type) as Base;
            if (value is null)
            {
                throw new InvalidOperationException($"Unable to create instance of {type.Name}");
            }

            if (fieldInfo.IsNonStringEnumerable())
            {
                var val = fieldInfo.GetValue(ctx.CurrentContext);
                fieldInfo.PropertyType.GetMethod("Add")?.Invoke(val, new[] { value });
            }
            else
            {
                fieldInfo.SetValue(ctx.CurrentContext, value);
            }

            ctx.SetCurrentContext(value);

            await ExtractByDefinition(
                ctx.QuestionnaireItem.Item,
                ctx.QuestionnaireResponseItem.Item,
                ctx,
                extractionResult,
                profileLoader,
                cancellationToken
            );

            ctx.RemoveContext();
        }
    }

    private static async Task ExtractPrimitiveTypeValueByDefinition(
        MappingContext ctx,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (ctx.CurrentContext is null)
        {
            throw new ArgumentException("ExtractionContext.CurrentContext is null", nameof(ctx));
        }

        var calculatedValue = ctx.QuestionnaireItem.CalculatedExpressionResult(ctx);
        if (calculatedValue is not null && ctx.QuestionnaireResponseItem.Answer.Count > 0)
        {
            Console.WriteLine(
                "Error: both calculated value and answer exist on QuestionnaireResponse item {0}",
                ctx.QuestionnaireItem.LinkId
            );

            return;
        }

        if (ctx.QuestionnaireResponseItem.Answer.Count == 0 && calculatedValue?.Result.Length is 0 or null)
        {
            Console.WriteLine("Warning: no answer or calculated value for {0}", ctx.QuestionnaireItem.LinkId);
            return;
        }

        var definition = ctx.QuestionnaireItem.Definition;

        var fieldName = FieldNameByDefinition(definition, true);

        var contextType = ctx.CurrentContext.GetType();
        var field = contextType.GetProperty(fieldName);

        if (field is null)
        {
            fieldName = FieldNameByDefinition(definition, false);
            field = contextType.GetProperty(fieldName);
        }

        if (field is not null)
        {
            if (field.NonParameterizedType().IsEnum)
            {
                UpdateFieldWithEnum(ctx.CurrentContext, field, ctx.QuestionnaireResponseItem.Answer.First().Value);
            }
            else
            {
                var propertyType = field.PropertyType.NonParameterizedType();
                IEnumerable<DataType> answers;

                if (calculatedValue is null)
                {
                    answers = ctx.QuestionnaireResponseItem.Answer.Select(ans => ans.Value);
                }
                else
                {
                    var calculatedValues = calculatedValue.Result.OfType<DataType>();

                    if (field.PropertyType.NonParameterizedType() == typeof(ResourceReference))
                    {
                        var sourceType = calculatedValue.SourceResource.GetType();
                        answers = calculatedValues.Select(value => CreateResourceReference(value, sourceType));
                    }
                    else
                    {
                        answers = calculatedValues;
                    }
                }

                UpdateField(ctx.CurrentContext, field, answers);
            }

            return;
        }

        await UseExtensionFromProfile(fieldName, ctx, profileLoader);
    }

    private static async Task UseSliceFromProfile(
        string fieldName,
        MappingContext ctx,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (ctx.CurrentContext is null)
        {
            Console.WriteLine("Error: CurrentContext is null");
            return;
        }

        var definition = ctx.QuestionnaireItem.Definition;
        var profileContext = await GetProfile(ctx, profileLoader, cancellationToken);

        if (profileContext is null)
        {
            return;
        }

        if (fieldName.Contains(':'))
        {
            var colonIndex = definition.LastIndexOf(':');
            var typeToCheck = definition[(profileContext.PoundIndex + 1)..colonIndex];

            var sliceName = definition[(colonIndex + 1)..];

            if (IsSliceSupportedByProfile(profileContext.Profile, typeToCheck, sliceName))
            {
                await ExtractSlice(
                    profileContext.Profile,
                    typeToCheck,
                    sliceName,
                    ctx,
                    extractionResult,
                    profileLoader,
                    cancellationToken
                );
            }
            else
            {
                Console.WriteLine(
                    "Warning: slice '{0}' for field {1} is not defined in StructureDefinition for {2}, so field is ignored",
                    sliceName,
                    fieldName,
                    ModelInfo.GetFhirTypeNameForType(ctx.CurrentContext.GetType())
                );
            }
        }
    }

    private static async Task UseExtensionFromProfile(
        string fieldName,
        MappingContext ctx,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (ctx.CurrentContext is null)
        {
            Console.WriteLine("Error: CurrentContext is null");
            return;
        }

        var profileContext = await GetProfile(ctx, profileLoader, cancellationToken);

        if (profileContext is null)
        {
            return;
        }

        var definition = ctx.QuestionnaireItem.Definition;
        var extensionForType = definition[(profileContext.PoundIndex + 1)..definition.LastIndexOf('.')];

        if (IsExtensionSupportedByProfile(profileContext.Profile, extensionForType, fieldName))
        {
            AddDefinitionBasedCustomExtension(ctx);
        }
        else
        {
            Console.WriteLine(
                "Warning: extension for field {0} is not defined in StructureDefinition for {1}, so field is ignored",
                fieldName,
                ModelInfo.GetFhirTypeNameForType(ctx.CurrentContext.GetType())
            );
        }
    }

    private static async Task<ProfileContext?> GetProfile(
        MappingContext ctx,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (ctx.CurrentContext is null)
        {
            Console.WriteLine("Error: CurrentContext is null");
            return null;
        }

        var definition = ctx.QuestionnaireItem.Definition;
        var poundIndex = definition.LastIndexOf('#');
        if (poundIndex < 0)
        {
            Console.WriteLine("Error: no pound sign in definition: [{0}]", definition);
            return null;
        }

        var canonicalUrl = definition[..poundIndex];
        var profile = await profileLoader.LoadProfileAsync(new Canonical(canonicalUrl));

        if (profile is null)
        {
            Console.WriteLine("Error: could not find profile for url: {0}", canonicalUrl);
            return null;
        }

        return new ProfileContext(poundIndex, profile);
    }

    private static async Task ExtractSlice(
        StructureDefinition profile,
        string baseType,
        string sliceName,
        MappingContext ctx,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        if (ctx.CurrentContext is null)
        {
            Console.WriteLine("Error: MappingContext.CurrentContext is null");
            return;
        }

        if (ctx.QuestionnaireItem.Repeats == true)
        {
            Console.WriteLine("Error: QuestionnaireItem with slice definition should not repeat");
            return;
        }

        var elementEnumerator = profile.Snapshot.Element.GetEnumerator();
        elementEnumerator.MoveNext();

        // TODO: Check if this works
        while (elementEnumerator.Current.Slicing is null || elementEnumerator.Current.Path != baseType)
        {
            elementEnumerator.MoveNext();
        }

        var discriminators = elementEnumerator.Current.Slicing!.Discriminator;

        var fieldName = FieldNameByDefinition(baseType);
        var contextType = ctx.CurrentContext.GetType();
        var fieldInfo = contextType.GetProperty(fieldName);

        if (fieldInfo is null)
        {
            Console.WriteLine("Could not find property {0} on {1}", fieldName, contextType.ToString());
            return;
        }

        var type = fieldInfo.NonParameterizedType();

        SliceDefinition? slice = null;

        while (elementEnumerator.MoveNext() && elementEnumerator.Current.Path.StartsWith(baseType))
        {
            var currentSliceName = elementEnumerator.Current.SliceName;

            if (string.IsNullOrEmpty(currentSliceName))
            {
                Console.WriteLine("Error: expected ElementDefinition with sliceName set");
                return;
            }

            if (currentSliceName == sliceName)
            {
                slice = new SliceDefinition(elementEnumerator.Current.SliceName);
            }

            while (elementEnumerator.MoveNext() && string.IsNullOrEmpty(elementEnumerator.Current.SliceName))
            {
                var current = elementEnumerator.Current;
                var fixedFieldName = FieldNameByDefinition(current.Path);
                var propInfo = type.GetProperty(fixedFieldName);
                if (propInfo is not null)
                {
                    if (current.Fixed is not null)
                    {
                        slice?.Fixed.Add(new SliceDefinition.SliceFilter(propInfo, current.Fixed));
                    }
                    else if (current.Pattern is not null)
                    {
                        slice?.Pattern.Add(new SliceDefinition.SliceFilter(propInfo, current.Pattern));
                    }
                }
            }
        }

        if (slice is null)
        {
            Console.WriteLine("Error: could not find matching slice in profile");
            return;
        }

        var value = Activator.CreateInstance(type) as Base;

        if (value is null)
        {
            Console.WriteLine("Error: could not construct type [{0}]", type);
            return;
        }

        foreach (var fixedVal in slice.Fixed)
        {
            fixedVal.PropertyInfo.SetValue(value, fixedVal.Value);
        }

        if (fieldInfo.IsNonStringEnumerable())
        {
            var val = fieldInfo.GetValue(ctx.CurrentContext);
            fieldInfo.PropertyType.GetMethod("Add")?.Invoke(val, new[] { value });
        }
        else
        {
            fieldInfo.SetValue(ctx.CurrentContext, value);
        }

        ctx.SetCurrentContext(value);

        await ExtractByDefinition(
            ctx.QuestionnaireItem.Item,
            ctx.QuestionnaireResponseItem.Item,
            ctx,
            extractionResult,
            profileLoader,
            cancellationToken
        );

        ctx.RemoveContext();
    }

    private static ResourceReference CreateResourceReference(Base from, Type referenceType)
    {
        if (from is ResourceReference reference)
        {
            return reference;
        }

        var idStr = from switch
        {
            Id fromId => fromId.Value,
            FhirString fhirStr => fhirStr.Value,
            _
                => throw new InvalidOperationException(
                    $"Error: cannot create reference from {ModelInfo.GetFhirTypeNameForType(from.GetType())}"
                )
        };

        return new ResourceReference($"{ModelInfo.GetFhirTypeNameForType(referenceType)}/{idStr}");
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
        string fieldName
    )
    {
        return profile.Snapshot.Element
            .Where(element => element.Path == $"{extensionForType}.extension")
            .Any(element => element.ElementId[(element.ElementId.LastIndexOf(':') + 1)..] == fieldName);
    }

    private static bool IsSliceSupportedByProfile(StructureDefinition profile, string typeToCheck, string sliceName)
    {
        var val = profile.Snapshot.Element
            .Where(element => element.Path == typeToCheck)
            .Any(element => element.SliceName == sliceName);

        return val;
    }

    private static void AddDefinitionBasedCustomExtension(MappingContext ctx)
    {
        if (ctx.CurrentContext is DataType dataType)
        {
            dataType.AddExtension(ctx.QuestionnaireItem.Definition, ctx.QuestionnaireResponseItem.Answer.First().Value);
        }
        else if (ctx.CurrentContext is DomainResource domainResource)
        {
            domainResource.AddExtension(
                ctx.QuestionnaireItem.Definition,
                ctx.QuestionnaireResponseItem.Answer.First().Value
            );
        }
    }

    public static QuestionnaireResponse Populate(Questionnaire questionnaire, MappingContext ctx)
    {
        PopulateInitialValues(questionnaire.Item.ToArray(), ctx);

        return new QuestionnaireResponse
        {
            Item = questionnaire.Item.Select(item => item.CreateQuestionnaireResponseItem()).ToList()
        };
    }

    private static void PopulateInitialValues(Questionnaire.ItemComponent[] questionnaireItems, MappingContext ctx)
    {
        foreach (var item in questionnaireItems)
        {
            ctx.QuestionnaireItem = item;
            PopulateInitalValue(ctx);
        }
    }

    private static void PopulateInitalValue(MappingContext ctx)
    {
        var initialExpression = ctx.QuestionnaireItem.InitialExpression();
        if (!(ctx.QuestionnaireItem.Initial.Count == 0 || initialExpression is null))
        {
            throw new InvalidOperationException(
                "QuestionnaireItem is not allowed to have both intial.value and initial expression. See rule at http://build.fhir.org/ig/HL7/sdc/expressions.html#initialExpression"
            );
        }

        if (initialExpression is not null)
        {
            // TODO: Should this return null if there is more than one result?
            var evalResult = FhirPathMapping.EvaluateExpr(initialExpression.Expression_, ctx)?.Result;

            if (evalResult is null)
            {
                Console.WriteLine("Could not find a value for {0}", initialExpression.Expression_);
            }
            else
            {
                ctx.QuestionnaireItem.Initial = (ctx.QuestionnaireItem.Repeats ?? false) switch
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
        }

        PopulateInitialValues(ctx.QuestionnaireItem.Item.ToArray(), ctx);
    }
}
