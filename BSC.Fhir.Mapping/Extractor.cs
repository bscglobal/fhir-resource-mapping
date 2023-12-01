using System.Collections;
using System.Reflection;
using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
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
    private readonly ILogger<Extractor> _logger;
    private readonly IScopeTreeCreator _scopeTreeCreator;

    public Extractor(
        IResourceLoader resourceLoader,
        IProfileLoader profileLoader,
        ILogger<Extractor> logger,
        IScopeTreeCreator scopeTreeCreator,
        INumericIdProvider? idProvider = null
    )
    {
        _idProvider = idProvider ?? new NumericIdProvider();
        _resourceLoader = resourceLoader;
        _profileLoader = new CachingProfileLoader(profileLoader);
        _logger = logger;
        _scopeTreeCreator = scopeTreeCreator;
    }

    public async Task<Bundle> ExtractAsync(
        Questionnaire questionnaire,
        QuestionnaireResponse questionnaireResponse,
        IDictionary<string, Resource> launchContext,
        CancellationToken cancellationToken = default
    )
    {
        var rootScope = await _scopeTreeCreator.CreateScopeTreeAsync(
            questionnaire,
            questionnaireResponse,
            launchContext,
            ResolvingContext.Extraction,
            cancellationToken
        );

        if (rootScope is null)
        {
            throw new InvalidOperationException("Could not extract resources");
        }

        // TreeDebugging.PrintTree(rootScope);

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
            Type = Bundle.BundleType.Collection,
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

        await ExtractByDefinition(scope.Children, extractionResult, cancellationToken);

        extractionResult.Add(context);
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
            return;
        }

        _logger.LogDebug(
            "Extracting Complex Type value for definition {Definition}. LinkId {LinkId}. Extraction Context: {ContextType}",
            scope.Item.Definition,
            scope.Item.LinkId,
            ModelInfo.GetFhirTypeNameForType(extractionContext.Value.GetType())
        );

        var definition = scope.Item.Definition;
        var fieldInfo = GetField(extractionContext.Value, definition);

        if (fieldInfo is null)
        {
            _logger.LogDebug(
                "Could not find field info for definition {Definition}. Checking if slice is defined",
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

                _logger.LogDebug("Enumerable: {@Value}", val);
                if (val is not null && scope.HasAnswers() && !extractionContext.DirtyFields.Contains(fieldInfo))
                {
                    _logger.LogDebug("Clearing list {Name}", fieldInfo.Name);
                    val.Clear();
                }

                if (scope.HasAnswers())
                {
                    val?.Add(value);
                }
            }
            else
            {
                if (fieldInfo.GetValue(extractionContext.Value) is null)
                {
                    fieldInfo.SetValue(extractionContext.Value, value);
                }
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
            "Extracting primitive value for Definition {Definition} on Type {Type}",
            scope.Item.Definition,
            extractionContext.Value.GetType().Name
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

        IReadOnlyCollection<DataType> answers;
        if (scope.ResponseItem.Answer.Count > 0)
        {
            answers = scope.ResponseItem.Answer.Select(answer => answer.Value).ToArray();
        }
        else if (calculatedValue?.Value is not null)
        {
            answers = calculatedValue.Value.OfType<DataType>().ToArray();
        }
        else
        {
            _logger.LogWarning("No answer or calculated value for {0}", scope.Item.LinkId);
            return;
        }

        var definition = scope.Item.Definition;

        var field = GetField(extractionContext.Value, definition);

        if (field is not null)
        {
            var propertyType = field.PropertyType.NonParameterizedType();

            if (field.PropertyType.NonParameterizedType() == typeof(ResourceReference))
            {
                Type? sourceType = null;
                if (scope.Item.GetExtension("referenceType")?.Value is FhirString referenceType)
                {
                    sourceType = ModelInfo.GetTypeForFhirType(referenceType.Value);
                }
                else if (calculatedValue?.SourceResourceType is not null)
                {
                    sourceType = calculatedValue.SourceResourceType;
                }

                if (sourceType is not null)
                {
                    _logger.LogDebug("Creating ResourceReferences");
                    answers = answers.Select(value => CreateResourceReference(value, sourceType)).ToArray();
                }
            }

            if (field.NonParameterizedType().IsEnum)
            {
                UpdateFieldWithEnum(extractionContext.Value, field, answers.First());
            }
            else
            {
                UpdateField(extractionContext, field, answers, scope);
            }

            extractionContext.DirtyFields.Add(field);

            return;
        }

        await UseExtensionFromProfile(definition.Split('.').Last(), extractionContext, scope, cancellationToken);
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
        var definition = item.Definition;
        var profileContext = await GetProfile(extractionContext, scope, cancellationToken);

        if (profileContext is null)
        {
            _logger.LogWarning("Could not find required profile for slice");
            return;
        }

        if (fieldName.Contains(':'))
        {
            var colonIndex = definition.LastIndexOf(':');
            var typeToCheck = definition[(profileContext.PoundIndex + 1)..colonIndex];

            var sliceName = definition[(colonIndex + 1)..];

            if (IsSliceSupportedByProfile(profileContext.Profile, typeToCheck, sliceName))
            {
                _logger.LogDebug("Slice {SliceName} is defined for {Type}", sliceName, typeToCheck);
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
        Scope scope,
        CancellationToken cancellationToken = default
    )
    {
        if (scope.Item is null)
        {
            throw new InvalidOperationException("Questionnaire Item is null");
        }
        var item = scope.Item;

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

        if (
            scope.ResponseItem is not null
            && extractionContext.Value is IExtendable extendable
            && IsExtensionSupportedByProfile(profileContext.Profile, extensionForType, fieldName)
        )
        {
            AddDefinitionBasedCustomExtension(extendable, item, scope.ResponseItem);
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
            _logger.LogWarning("Could not find field info for {Name}", baseType);
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

        _logger.LogDebug("Creating resource reference to {Type}", referenceType.Name);
        var referenceStr = idStr.Contains('/') ? idStr : $"{ModelInfo.GetFhirTypeNameForType(referenceType)}/{idStr}";
        return new ResourceReference(referenceStr);
    }

    private void UpdateField(
        ExtractionContext extractionContext,
        PropertyInfo field,
        IEnumerable<DataType> answers,
        Scope scope
    )
    {
        var fieldType = field.PropertyType;

        if (fieldType == typeof(Id))
        {
            _logger.LogDebug("Setting ID to {@Ids}", answers);
        }

        var answersOfFieldType = answers.Select(ans => WrapAnswerInFieldType(ans, field)).ToArray();

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

        if (!extractionContext.DirtyFields.Contains(fieldInfo) == true && scope.HasAnswers())
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

    private DataType WrapAnswerInFieldType(DataType answer, PropertyInfo propInfo, string? system = null)
    {
        var type = propInfo.PropertyType.NonParameterizedType();
        var allowedTypes = propInfo.GetCustomAttribute<AllowedTypesAttribute>()?.Types.ToList();
        return type switch
        {
            _ when type == typeof(CodeableConcept) && answer is Coding coding
                => new CodeableConcept { Coding = { coding }, Text = coding.Display, },
            _ when type == typeof(Id) && answer is FhirString idStr => new Id(idStr.Value),
            _ when type == typeof(Code) && answer is Coding code => new Code(code.Code),
            _ when type == typeof(Code) && answer is FhirString codeStr => new Code(codeStr.Value),
            _ when type == typeof(FhirUri) && answer is FhirString uriStr => new FhirUri(uriStr.Value),
            _ when type == typeof(FhirDecimal) && answer is Integer decInt => new FhirDecimal(decInt.Value),
            _ when type == typeof(Markdown) && answer is FhirString str => new Markdown(str.Value),
            _ when type == typeof(FhirDateTime) && answer is Date date => new FhirDateTime(date.Value),
            _
                when allowedTypes is not null
                    && type == typeof(DataType)
                    && answer is Date date
                    && allowedTypes.Contains(typeof(FhirDateTime))
                    && !allowedTypes.Contains(typeof(Date))
                => new FhirDateTime(date.Value),
            _ => answer
        };
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
        IExtendable extractionContext,
        Questionnaire.ItemComponent item,
        QuestionnaireResponse.ItemComponent responseItem
    )
    {
        var value = responseItem.Answer.First().Value;
        var existing = extractionContext.GetExtension(item.Definition);
        if (existing is not null)
        {
            existing.Value = value;
        }
        else
        {
            extractionContext.AddExtension(item.Definition, value);
        }
    }
}
