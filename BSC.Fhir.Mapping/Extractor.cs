using System.Collections;
using System.Reflection;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using BSC.Fhir.Mapping.Logging;
using Hl7.Fhir.Model;
using Hl7.Fhir.Validation;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace BSC.Fhir.Mapping;

public class Extractor : IExtractor
{
    private readonly INumericIdProvider _idProvider;
    private readonly IResourceLoader _resourceLoader;
    private readonly IProfileLoader _profileLoader;
    private readonly ILogger _logger;

    public Extractor(
        IResourceLoader resourceLoader,
        IProfileLoader profileLoader,
        INumericIdProvider? idProvider = null,
        ILogger? logger = null
    )
    {
        _idProvider = idProvider ?? new NumericIdProvider();
        _resourceLoader = resourceLoader;
        _profileLoader = new CachingProfileLoader(profileLoader);
        _logger = logger ?? FhirMappingLogging.GetLogger();
    }

    public async Task<Bundle> ExtractAsync(
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

        await ExtractByDefinition(scope.Children, extractedResources, cancellationToken);

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
            if (scope.Context.Any(ctx => ctx.Type == QuestionnaireContextType.ExtractionContext))
            {
                await ExtractResourceByDefinition(scope, extractionResult, cancellationToken);
            }
            else if (scope.Item.Definition is not null)
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
            _logger.LogError("Could not extract questionnaire item with LinkId {0}", scope.Item.LinkId);
        }
    }

    private async Task ExtractResourceByDefinition(
        Scope scope,
        List<Resource> extractionResult,
        CancellationToken cancellationToken = default
    )
    {
        var context = scope.ExtractionContextValue()?.Value as Resource;

        if (context is null)
        {
            throw new InvalidOperationException("Unable to create a resource from questionnaire item");
        }

        _logger.LogDebug("Extracting Resource {FhirType}", ModelInfo.GetFhirTypeNameForType(context.GetType()));

        await ExtractByDefinition(scope.Children, extractionResult, cancellationToken);

        extractionResult.Add(context);
    }

    private Resource? GetContextResource(IReadOnlyCollection<Resource> resources, MappingContext ctx)
    {
        var keyExtension = ctx.QuestionnaireItem.GetExtension("extractionContextId");

        if (keyExtension?.Value is not Expression idExpression)
        {
            _logger.LogWarning(
                "could not find key on extractionContext for QuestionnaireItem {0}",
                ctx.QuestionnaireItem.LinkId
            );
            return null;
        }

        // var result = FhirPathMapping.EvaluateExpr(idExpression.Expression_, ctx);
        EvaluationResult? result = null;
        if (result is null || result.Result.Length == 0)
        {
            _logger.LogWarning(
                "could not resolve expression {0} on QuestionnaireItem {1}",
                idExpression.Expression_,
                ctx.QuestionnaireItem.LinkId
            );
            return null;
        }

        if (result.Result.Length > 1)
        {
            _logger.LogWarning(
                "key expression {0} resolved to more than one value for {1}",
                idExpression.Expression_,
                ctx.QuestionnaireItem.LinkId
            );
            return null;
        }

        if (result.Result.First() is not FhirString str)
        {
            _logger.LogWarning("key does not resolve to string");
            return null;
        }

        var resource = resources.FirstOrDefault(resource => resource.Id == str.Value);

        if (resource is null)
        {
            _logger.LogDebug(
                "could not find extractionContext resource with key {0} for QuestionnaireItem {1}",
                str.Value,
                ctx.QuestionnaireItem.LinkId
            );
        }
        else
        {
            _logger.LogDebug(
                "context resource found for LinkId {0}. Key: {1}",
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

        var extractionContext = scope.Parent?.ExtractionContextValue();
        if (extractionContext is null)
        {
            throw new InvalidOperationException("Scope.ExtractionContext is null");
        }

        if (scope.ResponseItem.Item.Count == 0)
        {
            _logger.LogDebug(
                "QuestionnaireResponseItem {0} has no child items. Skipping extraction of complex type...",
                scope.Item.LinkId
            );
            return;
        }

        var definition = scope.Item.Definition;
        var fieldInfo = GetField(extractionContext.Value, definition);

        if (fieldInfo is null)
        {
            _logger.LogDebug(
                "Could not find field on ExtractionContext for definition {Definition}. Checking if Extension is defined",
                definition
            );

            await UseSliceFromProfile(
                scope.Item.Definition.Split('.').Last(),
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

            if (type == typeof(Hl7.Fhir.Model.DataType))
            {
                var allowedTypes = fieldInfo.GetCustomAttribute<AllowedTypesAttribute>()?.Types;
                if (allowedTypes is not null)
                {
                    var specifiedType = scope.Item.GetExtension("FhirType").Value.ToString();
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
                var val = fieldInfo.GetValue(extractionContext.Value) as IList;

                if (val is not null && !extractionContext.DirtyFields.Contains(fieldInfo))
                {
                    _logger.LogDebug("clearing list for field {0}", fieldInfo.Name);
                    val.Clear();
                }

                val?.Add(value);
            }
            else
            {
                fieldInfo.SetValue(extractionContext.Value, value);
            }

            scope.DefinitionResolution = value;
            extractionContext.DirtyFields.Add(fieldInfo);

            await ExtractByDefinition(scope.Children, extractionResult, cancellationToken);
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

        if (!(scope.Parent?.ExtractionContextValue() is ExtractionContext extractionContext))
        {
            throw new InvalidOperationException($"ExtractionContext at LinkId {scope.Item?.LinkId ?? "root"} is null");
        }

        _logger.LogDebug(
            "Extracting primitive value for Defintion {Definition}. LinkId {LinkId}. Extraction Context: {ContextType}",
            scope.Item.Definition,
            scope.Item.LinkId,
            ModelInfo.GetFhirTypeNameForType(extractionContext.Value.GetType())
        );

        var calculatedValue =
            scope.Context.FirstOrDefault(
                ctx => ctx.Type == Core.Expressions.QuestionnaireContextType.CalculatedExpression
            ) as FhirPathExpression;
        if (calculatedValue is not null && scope.ResponseItem.Answer.Count > 0)
        {
            _logger.LogError(
                "Both calculated value and answer exist on QuestionnaireResponse item {0}",
                scope.Item?.LinkId
            );

            return;
        }

        if (scope.ResponseItem.Answer.Count == 0 && calculatedValue?.Value?.Count is 0 or null)
        {
            _logger.LogWarning("No answer or calculated value for {0}", scope.Item.LinkId);
            return;
        }

        var definition = scope.Item.Definition;

        var field = GetField(extractionContext.Value, definition);

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

        _logger.LogDebug(
            "Could not find field on ExtractionContext for definition {Definition}. Checking if Extension is defined",
            definition
        );

        await UseExtensionFromProfile(
            definition.Split('.').Last(),
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
        _logger.LogDebug("Checking slice for fieldName {0}", fieldName);

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
                _logger.LogWarning(
                    "slice '{0}' for field {1} is not defined in StructureDefinition for {2}, so field is ignored",
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
            _logger.LogError("CurrentContext is null");
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
            _logger.LogWarning(
                "extension for field {0} is not defined in StructureDefinition for {1}, so field is ignored",
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
            _logger.LogError("Scope.Item is null");
            return null;
        }

        var definition = scope.Item.Definition;
        var poundIndex = definition.LastIndexOf('#');
        if (poundIndex < 0)
        {
            _logger.LogError("no pound sign in definition: [{0}]", definition);
            return null;
        }

        var canonicalUrl = definition[..poundIndex];
        var profile = await _profileLoader.LoadProfileAsync(new Canonical(canonicalUrl));

        if (profile is null)
        {
            _logger.LogError("could not find profile for url: {0}", canonicalUrl);
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
            _logger.LogError("QuestionnaireItem with slice definition should not repeat");
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

        var fieldInfo = GetField(extractionContext.Value, baseType);

        if (fieldInfo is null)
        {
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
                _logger.LogError("expected ElementDefinition with sliceName set");
                return;
            }

            if (currentSliceName == sliceName)
            {
                slice = new SliceDefinition(elementEnumerator.Current.SliceName);
            }

            while (elementEnumerator.MoveNext() && string.IsNullOrEmpty(elementEnumerator.Current.SliceName))
            {
                var current = elementEnumerator.Current;
                var propInfo = GetField(type, current.Path);
                if (propInfo is not null)
                {
                    _logger.LogDebug("Found slice propInfo {Type} for path {Path}", propInfo.Name, current.Path);
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
            _logger.LogError("Could not find matching slice in profile");
            return;
        }

        var value = Activator.CreateInstance(type) as Base;

        if (value is null)
        {
            _logger.LogError("could not construct type [{0}]", type);
            return;
        }

        foreach (var fixedVal in slice.Fixed)
        {
            SetFieldElementValue(value, fixedVal.PropertyInfo, fixedVal.Value);
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

        scope.DefinitionResolution = value;
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
        _logger.LogDebug(
            "Updating field {FieldName} of type {FieldType} with answer of type {AnswerType}",
            field.Name,
            field.PropertyType,
            answers.GetType().NonParameterizedType()
        );
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
        if (field.PropertyType.Name == "String" && answerValue is FhirString)
        {
            field.SetValue(baseResource, answerValue.ToString());
        }
        else
        {
            field.SetValue(baseResource, answerValue);
        }
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

        stringValue = stringValue[0..1].ToUpper() + stringValue[1..];

        var enumValue = Enum.Parse(fieldType, stringValue);

        // Create a Code instance with the determined enum type
        Type codeType = typeof(Code<>).MakeGenericType(fieldType);
        var codeValue = Activator.CreateInstance(codeType, (dynamic)enumValue);

        field.SetValue(baseResource, codeValue);
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

    private PropertyInfo? GetField(Base fieldOf, string definition)
    {
        var baseType = fieldOf.GetType();
        return GetField(baseType, definition);
    }

    private PropertyInfo? GetField(Type fieldOfType, string definition)
    {
        var propName = definition.Split('.').Last();
        propName = propName[0..1].ToUpper() + propName[1..];

        var fhirTypeName = ModelInfo.GetFhirTypeNameForType(fieldOfType);
        _logger.LogDebug("Getting field for definition {Definition} on type {Type}", definition, fhirTypeName);

        Type fieldType = fieldOfType;

        var elementPropName = propName + "Element";
        var propInfo = fieldType.GetProperty(elementPropName);

        if (propInfo is null)
        {
            propInfo = fieldType.GetProperty(propName);
        }
        else
        {
            propName = elementPropName;
        }

        if (propInfo is null)
        {
            _logger.LogDebug("Could not find property {PropName} on {Type}", propName, fieldType);
        }
        else
        {
            _logger.LogDebug("Found property {PropName} on {Type}", propName, fieldType);
        }

        return propInfo;
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
