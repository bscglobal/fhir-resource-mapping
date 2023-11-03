/*
 *
 *  This implementation is based on the Android FHIR implementation:
 *  https://github.com/google/android-fhir/blob/master/datacapture/src/main/java/com/google/android/fhir/datacapture/mapping/ResourceMapper.kt
 *
 */

using System.Collections;
using System.Reflection;
using System.Text.Json;
using BSC.Fhir.Mapping.Core;
using Hl7.Fhir.Model;
using Hl7.Fhir.Validation;
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
        var context = questionnaire.GetContext(extractionContext);
        var rootResource = context?.Resources.SingleOrDefault() ?? context?.CreateNewResource();
        // Console.WriteLine("Debug: root: {0}", rootResource?.ToString() ?? "null");
        var extractedResources = new List<Resource>();

        if (rootResource is not null)
        {
            extractionContext.SetCurrentExtractionContext(rootResource);
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
            extractionContext.PopCurrentExtractionContext();
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

            if (responseItems.Length == 0)
            {
                Console.WriteLine("Debug: could not find responseItem for LinkId {0}", questionnaireItem.LinkId);

                continue;
            }

            if (!(questionnaireItem.Repeats ?? false) && responseItems.Length > 1)
            {
                Console.WriteLine(
                    "Error: QuestionnaireResponse should not have more than one (1) answer for '{0}'",
                    questionnaireItem.LinkId
                );
                continue;
            }

            extractionContext.SetQuestionnaireItem(questionnaireItem);
            if (
                questionnaireItem.Type == Questionnaire.QuestionnaireItemType.Group
                && (questionnaireItem.Repeats ?? false)
                && extractionContext.QuestionnaireItem.Extension.ItemExtractionContextExtractionValue() is not null
            )
            {
                await ExtractResourcesByDefinition(
                    responseItems,
                    extractionContext,
                    extractionResult,
                    profileLoader,
                    cancellationToken
                );
            }
            else
            {
                if (responseItems.Length > 1)
                {
                    Console.WriteLine(
                        "Debug: ResponseItems length for {0} - {1}",
                        questionnaireItem.LinkId,
                        responseItems.Length
                    );
                }
                foreach (var responseItem in responseItems)
                {
                    extractionContext.SetQuestionnaireResponseItem(responseItem);
                    await ExtractByDefinition(extractionContext, extractionResult, profileLoader, cancellationToken);
                    extractionContext.PopQuestionnaireResponseItem();
                }
            }
            extractionContext.PopQuestionnaireItem();
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
        var contextResult = ctx.QuestionnaireItem.GetContext(ctx);
        var context = contextResult?.Resources.FirstOrDefault() ?? contextResult?.CreateNewResource();

        if (context is null)
        {
            throw new InvalidOperationException("Unable to create a resource from questionnaire item");
        }

        ctx.SetCurrentExtractionContext(context);
        await ExtractByDefinition(
            ctx.QuestionnaireItem.Item,
            ctx.QuestionnaireResponseItem.Item,
            ctx,
            extractionResult,
            profileLoader,
            cancellationToken
        );

        extractionResult.Add(context);
        ctx.PopCurrentExtractionContext();
    }

    private static async Task ExtractResourcesByDefinition(
        IReadOnlyCollection<QuestionnaireResponse.ItemComponent> responseItems,
        MappingContext ctx,
        List<Resource> extractionResult,
        IProfileLoader profileLoader,
        CancellationToken cancellationToken = default
    )
    {
        var contextResult = ctx.QuestionnaireItem.GetContext(ctx);

        if (contextResult is null)
        {
            throw new InvalidOperationException("Unable to create a resource from questionnaire item");
        }

        var contexts = new List<Resource>();

        foreach (var responseItem in responseItems)
        {
            ctx.SetQuestionnaireResponseItem(responseItem);

            var contextResource = GetContextResource(contextResult.Resources, ctx) ?? contextResult.CreateNewResource();

            if (contextResource is null)
            {
                Console.WriteLine(
                    "Warning: could not find resource for context in QuestionnaireItem {0}. Skipping this QuestionnaireResponseItem",
                    ctx.QuestionnaireItem.LinkId
                );
                continue;
            }

            ctx.SetCurrentExtractionContext(contextResource);

            await ExtractByDefinition(
                ctx.QuestionnaireItem.Item,
                responseItem.Item,
                ctx,
                extractionResult,
                profileLoader,
                cancellationToken
            );

            ctx.PopCurrentExtractionContext();
            ctx.PopQuestionnaireResponseItem();

            contexts.Add(contextResource);
        }

        extractionResult.AddRange(contexts);
    }

    private static Resource? GetContextResource(IReadOnlyCollection<Resource> resources, MappingContext ctx)
    {
        var keyExtension = ctx.QuestionnaireItem.GetExtension("extractionContextId");

        if (keyExtension?.Value is not Expression idExpression)
        {
            Console.WriteLine(
                "Warning: could not find key on extractionContext for QuestionnaireItem {0}",
                ctx.QuestionnaireItem.LinkId
            );
            return null;
        }

        var result = FhirPathMapping.EvaluateExpr(idExpression.Expression_, ctx);

        if (result is null || result.Result.Length == 0)
        {
            Console.WriteLine(
                "Warning: could not resolve expression {0} on QuestionnaireItem {1}",
                idExpression.Expression_,
                ctx.QuestionnaireItem.LinkId
            );
            return null;
        }

        if (result.Result.Length > 1)
        {
            Console.WriteLine(
                "Warning: key expression {0} resolved to more than one value for {1}",
                idExpression.Expression_,
                ctx.QuestionnaireItem.LinkId
            );
            return null;
        }

        if (result.Result.First() is not FhirString str)
        {
            Console.WriteLine("Warning: key does not resolve to string");
            return null;
        }

        var resource = resources.FirstOrDefault(resource => resource.Id == str.Value);

        if (resource is null)
        {
            Console.WriteLine(
                "Debug: could not find extractionContext resource with key {0} for QuestionnaireItem {1}",
                str.Value,
                ctx.QuestionnaireItem.LinkId
            );
        }
        else
        {
            Console.WriteLine(
                "Debug: context resource found for LinkId {0}. Key: {1}",
                ctx.QuestionnaireItem.LinkId,
                str.Value
            );
        }

        return resource;
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

        if (ctx.QuestionnaireResponseItem.Item.Count == 0)
        {
            Console.WriteLine(
                "Debug: QuestionnaireResponseItem {0} has no child items. Skipping extraction of complex type...",
                ctx.QuestionnaireResponseItem.LinkId
            );
            return;
        }

        var fieldName = FieldNameByDefinition(ctx.QuestionnaireItem.Definition);
        var fieldInfo = ctx.CurrentContext.Value.GetType().GetProperty(fieldName);

        var definition = ctx.QuestionnaireItem.Definition;
        if (fieldInfo is null)
        {
            await UseSliceFromProfile(fieldName, ctx, extractionResult, profileLoader, cancellationToken);
        }
        else
        {
            var type = fieldInfo.NonParameterizedType();

            if (type == typeof(Hl7.Fhir.Model.DataType))
            {
                var allowedTypes = fieldInfo.GetCustomAttribute<AllowedTypesAttribute>()?.Types;
                if (allowedTypes is not null)
                {
                    var specifiedType = ctx.QuestionnaireItem.GetExtension("FhirType").Value.ToString();
                    type = allowedTypes.FirstOrDefault(type => type.Name == specifiedType);
                    if (type is null)
                    {
                        throw new InvalidOperationException(
                            $"Error: type sepcified in extension is {specifiedType}, which does not match any AllowedTypesAttribute defined for {fieldInfo.Name}"
                        );
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Error: no AllowedTypesAttribute defined for {fieldInfo.Name}"
                    );
                }
            }

            var value = Activator.CreateInstance(type) as Base;
            if (value is null)
            {
                throw new InvalidOperationException($"Unable to create instance of {type.Name}");
            }

            if (fieldInfo.IsNonStringEnumerable())
            {
                var val = fieldInfo.GetValue(ctx.CurrentContext.Value) as IList;

                if (val is not null && !ctx.CurrentContext.DirtyFields.Contains(fieldInfo))
                {
                    Console.WriteLine("Debug: clearing list for field {0}", fieldInfo.Name);
                    val.Clear();
                }

                val?.Add(value);
            }
            else
            {
                fieldInfo.SetValue(ctx.CurrentContext.Value, value);
            }

            ctx.CurrentContext.DirtyFields.Add(fieldInfo);

            ctx.SetCurrentExtractionContext(value);

            await ExtractByDefinition(
                ctx.QuestionnaireItem.Item,
                ctx.QuestionnaireResponseItem.Item,
                ctx,
                extractionResult,
                profileLoader,
                cancellationToken
            );

            ctx.PopCurrentExtractionContext();
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
                ctx.QuestionnaireResponseItem.LinkId
            );

            return;
        }

        if (ctx.QuestionnaireResponseItem.Answer.Count == 0 && calculatedValue?.Result.Length is 0 or null)
        {
            Console.WriteLine("Warning: no answer or calculated value for {0}", ctx.QuestionnaireResponseItem.LinkId);
            return;
        }

        var definition = ctx.QuestionnaireItem.Definition;

        var fieldName = FieldNameByDefinition(definition, true);

        var contextType = ctx.CurrentContext.Value.GetType();
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
                UpdateFieldWithEnum(
                    ctx.CurrentContext.Value,
                    field,
                    ctx.QuestionnaireResponseItem.Answer.First().Value
                );
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

                UpdateField(ctx.CurrentContext.Value, field, answers, ctx);
            }

            ctx.CurrentContext.DirtyFields.Add(field);

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
        Console.WriteLine("Debug: Checking slice for fieldName {0}", fieldName);
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
                    ModelInfo.GetFhirTypeNameForType(ctx.CurrentContext.Value.GetType())
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
                ModelInfo.GetFhirTypeNameForType(ctx.CurrentContext.Value.GetType())
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
        var contextType = ctx.CurrentContext.Value.GetType();
        var fieldInfo = contextType.GetProperty(fieldName);

        if (fieldInfo is null)
        {
            Console.WriteLine("Could not find property {0} on {1}", fieldName, contextType.ToString());
            return;
        }

        var type = fieldInfo.NonParameterizedType();

        SliceDefinition? slice = null;
        elementEnumerator.MoveNext();
        while (
            elementEnumerator.Current is not null
            && elementEnumerator.Current.Path.StartsWith(baseType)
            && slice is null
        )
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
                var fixedFieldName = FieldNameByDefinition(current.Path, true);
                var propInfo = type.GetProperty(fixedFieldName);
                if (propInfo is null)
                {
                    fixedFieldName = FieldNameByDefinition(current.Path, false);
                    propInfo = type.GetProperty(fixedFieldName);
                }
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
            SetFieldElementValue(value, fixedVal.PropertyInfo, fixedVal.Value);
        }

        if (fieldInfo.IsNonStringEnumerable())
        {
            var val = fieldInfo.GetValue(ctx.CurrentContext.Value) as IList;
            if (val is not null && !ctx.CurrentContext.DirtyFields.Contains(fieldInfo))
            {
                val.Clear();
            }
            val?.Add(value);
        }
        else
        {
            fieldInfo.SetValue(ctx.CurrentContext.Value, value);
        }

        ctx.CurrentContext.DirtyFields.Add(fieldInfo);

        ctx.SetCurrentExtractionContext(value);

        await ExtractByDefinition(
            ctx.QuestionnaireItem.Item,
            ctx.QuestionnaireResponseItem.Item,
            ctx,
            extractionResult,
            profileLoader,
            cancellationToken
        );

        ctx.PopCurrentExtractionContext();
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

    private static void UpdateField(
        Base resource,
        PropertyInfo field,
        IEnumerable<DataType> answers,
        MappingContext ctx
    )
    {
        var fieldType = field.PropertyType;
        var answersOfFieldType = answers.Select(ans => WrapAnswerInFieldType(ans, fieldType)).ToArray();

        if (field.IsParameterized() && fieldType.IsNonStringEnumerable())
        {
            AddAnswerToListField(resource, field, answersOfFieldType, ctx);
        }
        else
        {
            SetFieldElementValue(resource, field, answersOfFieldType.First());
        }
    }

    private static void SetFieldElementValue(Base baseResource, PropertyInfo field, DataType answerValue)
    {
        if (field.PropertyType.Name == "String" && answerValue is FhirString)
        {
            field.SetValue(baseResource, answerValue.ToString());
        }
        else
        {
            field.SetValue(baseResource, answerValue);
        }
    }

    private static void AddAnswerToListField(
        Base baseResource,
        PropertyInfo fieldInfo,
        IReadOnlyCollection<DataType> answerValue,
        MappingContext ctx
    )
    {
        var propName = fieldInfo.Name;
        var field = fieldInfo.GetValue(baseResource) as IList;

        if (field is null)
        {
            return;
        }

        if (ctx.CurrentContext?.DirtyFields.Contains(fieldInfo) == true)
        {
            field.Clear();
        }

        foreach (var answer in answerValue)
        {
            field.Add(answer);
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
        stringValue = stringValue[0..1].ToUpper() + stringValue[1..];

        var enumValue = Enum.Parse(fieldType, stringValue);

        // Create a Code instance with the determined enum type
        Type codeType = typeof(Code<>).MakeGenericType(fieldType);
        var codeValue = Activator.CreateInstance(codeType, (dynamic)enumValue);

        field.SetValue(baseResource, codeValue);
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
        if (ctx.CurrentContext?.Value is DataType dataType)
        {
            dataType.AddExtension(ctx.QuestionnaireItem.Definition, ctx.QuestionnaireResponseItem.Answer.First().Value);
        }
        else if (ctx.CurrentContext?.Value is DomainResource domainResource)
        {
            domainResource.AddExtension(
                ctx.QuestionnaireItem.Definition,
                ctx.QuestionnaireResponseItem.Answer.First().Value
            );
        }
    }

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
            var tempContext = false;
            if (!ctx.NamedExpressions.TryGetValue(populationContextExpression.Name, out var context))
            {
                var result = FhirPathMapping.EvaluateExpr(populationContextExpression.Expression_, ctx);
                if (result is null)
                {
                    Console.WriteLine(
                        "Warning: could not resolve expression {0} for {1}",
                        populationContextExpression.Expression_,
                        ctx.QuestionnaireItem.LinkId
                    );
                    return null;
                }

                context = new(result.Result, populationContextExpression.Name);
                ctx.NamedExpressions.Add(populationContextExpression.Name, context);
                tempContext = true;
            }

            var contextValues = context.Value;
            var responseItems = contextValues
                .Select(value =>
                {
                    var questionnaireResponseItem = new QuestionnaireResponse.ItemComponent
                    {
                        LinkId = ctx.QuestionnaireItem.LinkId
                    };
                    ctx.SetQuestionnaireResponseItem(questionnaireResponseItem);

                    context.Value = new[] { value };

                    CreateQuestionnaireResponseItems(ctx.QuestionnaireItem.Item, questionnaireResponseItem.Item, ctx);
                    ctx.PopQuestionnaireResponseItem();

                    return questionnaireResponseItem;
                })
                .ToArray();
            context.Value = contextValues;

            if (tempContext)
            {
                ctx.NamedExpressions.Remove(populationContextExpression.Name);
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
            var evalResult = FhirPathMapping.EvaluateExpr(initialExpression.Expression_, ctx);

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
        else if (ctx.QuestionnaireItem.Initial.Count > 0)
        {
            return ctx.QuestionnaireItem.Initial
                .Select(initial => new QuestionnaireResponse.AnswerComponent() { Value = initial.Value })
                .ToList();
        }

        return null;
        // CreateQuestionnaireResponseItems(ctx.QuestionnaireItem.Item.ToArray(), ctx);
    }
}
