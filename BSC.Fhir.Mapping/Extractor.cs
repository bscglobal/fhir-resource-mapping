using System.Collections;
using System.Reflection;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Expressions;
using Hl7.Fhir.Model;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping;

public class Extractor
{
    private readonly INumericIdProvider _idProvider;
    private readonly IResourceLoader _resourceLoader;
    private readonly IProfileLoader _profileLoader;

    public Extractor(
        IResourceLoader resourceLoader,
        IProfileLoader profileLoader,
        INumericIdProvider? idProvider = null
    )
    {
        _idProvider = idProvider ?? new NumericIdProvider();
        _resourceLoader = resourceLoader;
        _profileLoader = new CachingProfileLoader(profileLoader);
    }

    public async Task<Bundle> Extract(
        Questionnaire questionnaire,
        QuestionnaireResponse questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        CancellationToken cancellationToken = default
    )
    {
        var resolver = new DependencyResolver(
            _idProvider,
            questionnaire,
            questionnaireResponse,
            launchContext,
            _resourceLoader,
            ResolvingContext.Extraction
        );

        var rootScope = await resolver.ParseQuestionnaireAsync(cancellationToken);

        if (rootScope is null)
        {
            throw new InvalidOperationException("Could not extract resources");
        }

        return await ExtractByDefinition(rootScope, cancellationToken);
    }

    private async Task<Bundle> ExtractByDefinition(Scope scope, CancellationToken cancellationToken = default)
    {
        var rootResource = scope.ExtractionContext()?.Value?.FirstOrDefault() as Resource;
        var extractedResources = new List<Resource>();

        await ExtractByDefinition(scope, extractedResources, cancellationToken);

        if (rootResource is not null)
        {
            extractedResources.Add(rootResource);
        }

        return new Bundle
        {
            Type = Bundle.BundleType.Transaction,
            Entry = extractedResources.Select(resource => new Bundle.EntryComponent { Resource = resource }).ToList()
        };
    }

    private async Task ExtractByDefinition(
        IReadOnlyCollection<Scope> scopes,
        List<Resource> extractionResult,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var scope in scopes)
        {
            await ExtractByDefinition(scope, extractionResult, cancellationToken);
        }
    }

    private async Task ExtractByDefinition(
        Scope scope,
        List<Resource> extractionResult,
        CancellationToken cancellationToken = default
    )
    {
        if (scope.Item is null)
        {
            throw new InvalidOperationException("Questionnaire Item is null");
        }

        if (scope.ResponseItem is null)
        {
            throw new InvalidOperationException($"QuestionnaireResponse Item is null at LinkId {scope.Item.LinkId}");
        }

        if (scope.Item.Type == Questionnaire.QuestionnaireItemType.Group)
        {
            if (scope.Item.Definition is not null)
            {
                await ExtractComplexTypeValueByDefinition(scope, extractionResult, cancellationToken);
            }
            else
            {
                await ExtractByDefinition(scope.Children, extractionResult, cancellationToken);
            }
        }
        else if (scope.Item.Definition is not null)
        {
            await ExtractPrimitiveTypeValueByDefinition(scope, cancellationToken);
        }
        else
        {
            Console.WriteLine("Error: Could not extract questionnaire item with LinkId {0}", scope.Item.LinkId);
        }
    }

    // private async Task ExtractResourceByDefinition(
    //     MappingContext ctx,
    //     List<Resource> extractionResult,
    //     IProfileLoader profileLoader,
    //     CancellationToken cancellationToken = default
    // )
    // {
    //     var contextResult = ctx.QuestionnaireItem.GetContext(ctx);
    //     var context = contextResult?.Resources.FirstOrDefault() ?? contextResult?.CreateNewResource();
    //
    //     if (context is null)
    //     {
    //         throw new InvalidOperationException("Unable to create a resource from questionnaire item");
    //     }
    //
    //     ctx.SetCurrentExtractionContext(context);
    //     await ExtractByDefinition(
    //         ctx.QuestionnaireItem.Item,
    //         ctx.QuestionnaireResponseItem.Item,
    //         ctx,
    //         extractionResult,
    //         profileLoader,
    //         cancellationToken
    //     );
    //
    //     extractionResult.Add(context);
    //     ctx.PopCurrentExtractionContext();
    // }

    // private async Task ExtractResourcesByDefinition(
    //     IReadOnlyCollection<QuestionnaireResponse.ItemComponent> responseItems,
    //     MappingContext ctx,
    //     List<Resource> extractionResult,
    //     IProfileLoader profileLoader,
    //     CancellationToken cancellationToken = default
    // )
    // {
    //     var contextResult = ctx.QuestionnaireItem.GetContext(ctx);
    //
    //     if (contextResult is null)
    //     {
    //         throw new InvalidOperationException("Unable to create a resource from questionnaire item");
    //     }
    //
    //     var contexts = new List<Resource>();
    //
    //     foreach (var responseItem in responseItems)
    //     {
    //         ctx.SetQuestionnaireResponseItem(responseItem);
    //
    //         var contextResource = GetContextResource(contextResult.Resources, ctx) ?? contextResult.CreateNewResource();
    //
    //         if (contextResource is null)
    //         {
    //             Console.WriteLine(
    //                 "Warning: could not find resource for context in QuestionnaireItem {0}. Skipping this QuestionnaireResponseItem",
    //                 ctx.QuestionnaireItem.LinkId
    //             );
    //             continue;
    //         }
    //
    //         ctx.SetCurrentExtractionContext(contextResource);
    //
    //         await ExtractByDefinition(
    //             ctx.QuestionnaireItem.Item,
    //             responseItem.Item,
    //             ctx,
    //             extractionResult,
    //             profileLoader,
    //             cancellationToken
    //         );
    //
    //         ctx.PopCurrentExtractionContext();
    //         ctx.PopQuestionnaireResponseItem();
    //
    //         contexts.Add(contextResource);
    //     }
    //
    //     extractionResult.AddRange(contexts);
    // }

    private Resource? GetContextResource(IReadOnlyCollection<Resource> resources, MappingContext ctx)
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

        // var result = FhirPathMapping.EvaluateExpr(idExpression.Expression_, ctx);
        EvaluationResult? result = null;
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

    private async Task ExtractComplexTypeValueByDefinition(
        Scope scope,
        List<Resource> extractionResult,
        CancellationToken cancellationToken = default
    )
    {
        if (scope.Item is null)
        {
            throw new InvalidOperationException("Questionnaire Item is null");
        }

        if (scope.ResponseItem is null)
        {
            throw new InvalidOperationException($"QuestionnaireResponse Item is null at LinkId {scope.Item.LinkId}");
        }

        var extractionContext = scope.ExtractionContextValue();
        if (extractionContext is null)
        {
            throw new InvalidOperationException("Scope.ExtractionContext is null");
        }

        if (scope.ResponseItem.Item.Count == 0)
        {
            Console.WriteLine(
                "Debug: QuestionnaireResponseItem {0} has no child items. Skipping extraction of complex type...",
                scope.Item.LinkId
            );
            return;
        }

        var fieldName = FieldNameByDefinition(scope.Item.Definition);
        var fieldInfo = extractionContext.Value.GetType().GetProperty(fieldName);

        var definition = scope.Item.Definition;
        if (fieldInfo is null)
        {
            await UseSliceFromProfile(
                fieldName,
                extractionContext,
                scope.Item,
                extractionResult,
                scope,
                cancellationToken
            );
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
                var val = fieldInfo.GetValue(extractionContext.Value) as IList;

                if (val is not null && !extractionContext.DirtyFields.Contains(fieldInfo))
                {
                    Console.WriteLine("Debug: clearing list for field {0}", fieldInfo.Name);
                    val.Clear();
                }

                val?.Add(value);
            }
            else
            {
                fieldInfo.SetValue(extractionContext.Value, value);
            }

            extractionContext.DirtyFields.Add(fieldInfo);

            await ExtractByDefinition(scope, extractionResult, cancellationToken);
        }
    }

    private async Task ExtractPrimitiveTypeValueByDefinition(Scope scope, CancellationToken cancellationToken = default)
    {
        if (scope.Item is null)
        {
            throw new InvalidOperationException("Questionnaire Item is null");
        }

        if (scope.ResponseItem is null)
        {
            throw new InvalidOperationException($"QuestionnaireResponse Item is null at LinkId {scope.Item.LinkId}");
        }

        if (!(scope.ExtractionContextValue() is ExtractionContext extractionContext))
        {
            throw new InvalidOperationException($"ExtractionContext at LinkId {scope.Item?.LinkId ?? "root"} is null");
        }

        var calculatedValue =
            scope.Context.FirstOrDefault(
                ctx => ctx.Type == Core.Expressions.QuestionnaireContextType.CalculatedExpression
            ) as FhirPathExpression;
        if (calculatedValue is not null && scope.ResponseItem.Answer.Count > 0)
        {
            Console.WriteLine(
                "Error: both calculated value and answer exist on QuestionnaireResponse item {0}",
                scope.Item?.LinkId
            );

            return;
        }

        if (scope.ResponseItem.Answer.Count == 0 && calculatedValue?.Value?.Count is 0 or null)
        {
            Console.WriteLine("Warning: no answer or calculated value for {0}", scope.Item.LinkId);
            return;
        }

        var definition = scope.Item.Definition;

        var fieldName = FieldNameByDefinition(definition, true);

        var contextType = extractionContext.GetType();
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
                UpdateFieldWithEnum(extractionContext.Value, field, scope.ResponseItem.Answer.First().Value);
            }
            else
            {
                var propertyType = field.PropertyType.NonParameterizedType();
                IEnumerable<DataType> answers;

                if (calculatedValue?.Value is null || calculatedValue?.SourceResource is null)
                {
                    answers = scope.ResponseItem.Answer.Select(ans => ans.Value);
                }
                else
                {
                    var calculatedValues = calculatedValue.Value.OfType<DataType>();

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

                UpdateField(extractionContext, field, answers, scope);
            }

            extractionContext.DirtyFields.Add(field);

            return;
        }

        await UseExtensionFromProfile(
            fieldName,
            extractionContext,
            scope.Item,
            scope.ResponseItem,
            scope,
            cancellationToken
        );
    }

    private async Task UseSliceFromProfile(
        string fieldName,
        ExtractionContext extractionContext,
        Questionnaire.ItemComponent item,
        List<Resource> extractionResult,
        Scope scope,
        CancellationToken cancellationToken = default
    )
    {
        Console.WriteLine("Debug: Checking slice for fieldName {0}", fieldName);

        var definition = item.Definition;
        var profileContext = await GetProfile(extractionContext, scope, cancellationToken);

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
                    extractionContext,
                    item,
                    extractionResult,
                    scope,
                    cancellationToken
                );
            }
            else
            {
                Console.WriteLine(
                    "Warning: slice '{0}' for field {1} is not defined in StructureDefinition for {2}, so field is ignored",
                    sliceName,
                    fieldName,
                    ModelInfo.GetFhirTypeNameForType(extractionContext.Value.GetType())
                );
            }
        }
    }

    private async Task UseExtensionFromProfile(
        string fieldName,
        ExtractionContext extractionContext,
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem,
        Scope scope,
        CancellationToken cancellationToken = default
    )
    {
        if (extractionContext is null)
        {
            Console.WriteLine("Error: CurrentContext is null");
            return;
        }

        var profileContext = await GetProfile(extractionContext, scope, cancellationToken);

        if (profileContext is null)
        {
            return;
        }

        var definition = item.Definition;
        var extensionForType = definition[(profileContext.PoundIndex + 1)..definition.LastIndexOf('.')];

        if (IsExtensionSupportedByProfile(profileContext.Profile, extensionForType, fieldName))
        {
            AddDefinitionBasedCustomExtension(extractionContext.Value, item, responseItem);
        }
        else
        {
            Console.WriteLine(
                "Warning: extension for field {0} is not defined in StructureDefinition for {1}, so field is ignored",
                fieldName,
                ModelInfo.GetFhirTypeNameForType(extractionContext.Value.GetType())
            );
        }
    }

    private async Task<ProfileContext?> GetProfile(
        ExtractionContext extractionContext,
        Scope scope,
        CancellationToken cancellationToken = default
    )
    {
        if (scope.Item is null)
        {
            Console.WriteLine("Error: Scope.Item is null");
            return null;
        }

        var definition = scope.Item.Definition;
        var poundIndex = definition.LastIndexOf('#');
        if (poundIndex < 0)
        {
            Console.WriteLine("Error: no pound sign in definition: [{0}]", definition);
            return null;
        }

        var canonicalUrl = definition[..poundIndex];
        var profile = await _profileLoader.LoadProfileAsync(new Canonical(canonicalUrl));

        if (profile is null)
        {
            Console.WriteLine("Error: could not find profile for url: {0}", canonicalUrl);
            return null;
        }

        return new ProfileContext(poundIndex, profile);
    }

    private async Task ExtractSlice(
        StructureDefinition profile,
        string baseType,
        string sliceName,
        ExtractionContext extractionContext,
        Questionnaire.ItemComponent item,
        List<Resource> extractionResult,
        Scope scope,
        CancellationToken cancellationToken = default
    )
    {
        if (item.Repeats == true)
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
        var contextType = extractionContext.Value.GetType();
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
            var val = fieldInfo.GetValue(extractionContext.Value) as IList;
            if (val is not null && !extractionContext.DirtyFields.Contains(fieldInfo))
            {
                val.Clear();
            }
            val?.Add(value);
        }
        else
        {
            fieldInfo.SetValue(extractionContext.Value, value);
        }

        extractionContext.DirtyFields.Add(fieldInfo);

        await ExtractByDefinition(scope.Children, extractionResult, cancellationToken);
    }

    private ResourceReference CreateResourceReference(Base from, Type referenceType)
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

    private void UpdateField(
        ExtractionContext extractionContext,
        PropertyInfo field,
        IEnumerable<DataType> answers,
        Scope scope
    )
    {
        var fieldType = field.PropertyType;
        var answersOfFieldType = answers.Select(ans => WrapAnswerInFieldType(ans, fieldType)).ToArray();

        if (field.IsParameterized() && fieldType.IsNonStringEnumerable())
        {
            AddAnswerToListField(extractionContext, field, answersOfFieldType, scope);
        }
        else
        {
            SetFieldElementValue(extractionContext.Value, field, answersOfFieldType.First());
        }
    }

    private void SetFieldElementValue(Base baseResource, PropertyInfo field, DataType answerValue)
    {
        field.SetValue(baseResource, answerValue);
    }

    private void AddAnswerToListField(
        ExtractionContext extractionContext,
        PropertyInfo fieldInfo,
        IReadOnlyCollection<DataType> answerValue,
        Scope scope
    )
    {
        var propName = fieldInfo.Name;
        var field = fieldInfo.GetValue(extractionContext.Value) as IList;

        if (field is null)
        {
            return;
        }

        if (extractionContext.DirtyFields.Contains(fieldInfo) == true)
        {
            field.Clear();
        }

        foreach (var answer in answerValue)
        {
            field.Add(answer);
        }
    }

    private void UpdateFieldWithEnum(Base baseResource, PropertyInfo field, Base value)
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

    private DataType WrapAnswerInFieldType(DataType answer, Type fieldType, string? system = null)
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

    private string FieldNameByDefinition(string definition, bool isElement = false)
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

    private bool IsExtensionSupportedByProfile(StructureDefinition profile, string extensionForType, string fieldName)
    {
        return profile.Snapshot.Element
            .Where(element => element.Path == $"{extensionForType}.extension")
            .Any(element => element.ElementId[(element.ElementId.LastIndexOf(':') + 1)..] == fieldName);
    }

    private bool IsSliceSupportedByProfile(StructureDefinition profile, string typeToCheck, string sliceName)
    {
        var val = profile.Snapshot.Element
            .Where(element => element.Path == typeToCheck)
            .Any(element => element.SliceName == sliceName);

        return val;
    }

    private void AddDefinitionBasedCustomExtension(
        Base extractionContext,
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem
    )
    {
        if (extractionContext is DataType dataType)
        {
            dataType.AddExtension(item.Definition, responseItem.Answer.First().Value);
        }
        else if (extractionContext is DomainResource domainResource)
        {
            domainResource.AddExtension(item.Definition, responseItem.Answer.First().Value);
        }
    }
}
