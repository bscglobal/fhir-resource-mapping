using System.Text.RegularExpressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class DependencyResolver
{
    private class ResolverState
    {
        private readonly Stack<Questionnaire.ItemComponent> _items = new();
        private readonly Stack<QuestionnaireResponse.ItemComponent> _responseItems = new();

        public string LinkId { get; private set; } = "root";

        public Questionnaire.ItemComponent? CurrentItem => _items.TryPeek(out var item) ? item : null;
        public QuestionnaireResponse.ItemComponent? CurrentResponseItem =>
            _responseItems.TryPeek(out var responseItem) ? responseItem : null;

        public void PushQuestionnaireItem(Questionnaire.ItemComponent item)
        {
            _items.Push(item);
        }

        public void PopQuestionnaireItem()
        {
            _items.Pop();
        }

        public void PushQuestionnaireResponseItem(QuestionnaireResponse.ItemComponent responseItem)
        {
            _responseItems.Push(responseItem);
        }

        public void PopQuestionnaireResponseItem()
        {
            _responseItems.Pop();
        }
    }

    private readonly NumericIdProvider _idProvider = new();
    private readonly ResolverState _state = new();
    private readonly List<QuestionnaireQuery> _queries = new();
    private readonly Questionnaire _questionnaire;
    private readonly QuestionnaireResponse? _questionnaireResponse;

    public static void Resolve(Questionnaire questionnaire, QuestionnaireResponse? questionnaireResponse = null)
    {
        var resolver = new DependencyResolver(questionnaire, questionnaireResponse);
        resolver.ParseQuestionnaire();
    }

    private DependencyResolver(Questionnaire questionnaire, QuestionnaireResponse? questionnaireResponse = null)
    {
        _questionnaire = questionnaire;
        _questionnaireResponse = questionnaireResponse;
    }

    private bool ParseQuestionnaire()
    {
        _queries.Clear();
        var rootExtensions = _questionnaire.AllExtensions();

        ParseExtensions(rootExtensions.ToArray());
        ParseQuestionnaireItems(_questionnaire.Item);
        var graphResult = CreateDependencyGraph();
        if (graphResult is not null)
        {
            // _logger.LogError(
            //     $"Circular dependency detected for {graphResult.Expression.Expression_} in {graphResult.LinkId}"
            // );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Go through all found queries and calculate what each query depends on.
    /// </summary>
    private QuestionnaireQuery? CreateDependencyGraph()
    {
        for (var i = 0; i < _queries.Count; i++)
        {
            var query = _queries[i];
            var expression = query.Expression;

            QuestionnaireQuery? result = null;
            if (expression.Language == Constants.FHIR_QUERY_MIME)
            {
                result = CalculateFhirQueryDependencies(query);
            }
            else if (expression.Language == Constants.FHIRPATH_MIME)
            {
                result = CalculateFhirPathDependencies(query);
            }

            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private QuestionnaireQuery? CalculateFhirQueryDependencies(QuestionnaireQuery query)
    {
        var expression = query.Expression.Expression_;
        var embeddedFhirpathRegex = @"\{\{(.*)\}\}";
        var matches = Regex.Matches(expression, embeddedFhirpathRegex);

        foreach (Match match in matches)
        {
            var fhirpathExpression = match.Groups.Values.FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(fhirpathExpression))
            {
                // _logger.LogWarning($"Invalid embedded query {match.Value}");
                continue;
            }

            fhirpathExpression = Regex.Replace(fhirpathExpression, "[{}]", "");

            var expr = new Expression { Language = Constants.FHIRPATH_MIME, Expression_ = fhirpathExpression };
            var embeddedQuery = CreateQuery(expr, QuestionnaireQueryType.Embedded, query);
            _queries.Add(embeddedQuery);

            if (!query.AddDependency(embeddedQuery))
            {
                return query;
            }
        }

        return null;
    }

    private QuestionnaireQuery? CalculateFhirPathDependencies(QuestionnaireQuery query)
    {
        var fhirpathRegex = @"([^.]+(\((.+\..+)+\)))?([^.]+)?";
        var expression = query.Expression.Expression_;

        var parts = Regex.Matches(expression, fhirpathRegex).Select(match => match.Value);
        var variables = parts.Where(part => part.StartsWith('%'));

        foreach (var variable in variables)
        {
            if (Constants.POPULATION_DEPENDANT_CONTEXT.Contains(variable))
            {
                query.MakePopulationDependant();
                continue;
            }

            var varName = variable[1..];
            var dep = _queries.FirstOrDefault(query => query.Expression.Name == varName);

            if (dep is not null)
            {
                if (!query.AddDependency(dep))
                {
                    return query;
                }
            }
        }

        return null;
    }

    private void ParseQuestionnaireItems(IReadOnlyCollection<Questionnaire.ItemComponent> items)
    {
        foreach (var item in items)
        {
            ParseQuestionnaireItem(item);
        }
    }

    private void ParseQuestionnaireItem(Questionnaire.ItemComponent item)
    {
        if (string.IsNullOrEmpty(item.LinkId))
        {
            // _logger.LogWarning("Questionnaire item does not have LinkId, skipping...");
            return;
        }

        var extensions = item.AllExtensions();
        ParseExtensions(extensions.ToArray(), item.LinkId);

        ParseQuestionnaireItems(item.Item);
    }

    private void ParseExtensions(IReadOnlyCollection<Extension> extensions, string? currentLinkId = null)
    {
        var queries = extensions.Select(extension => ParseExtension(extension)).OfType<QuestionnaireQuery>();
        _queries.AddRange(queries);
    }

    private QuestionnaireQuery? ParseExtension(Extension extension)
    {
        return extension.Url switch
        {
            Constants.POPULATION_CONTEXT
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME, Constants.FHIR_QUERY_MIME },
                    QuestionnaireQueryType.PopulationContext
                ),
            Constants.EXTRACTION_CONTEXT
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME, Constants.FHIR_QUERY_MIME },
                    QuestionnaireQueryType.ExtractionContext
                ),
            Constants.INITIAL_EXPRESSION
                => ParseExpressionExtension(
                    extension,
                    new[] { Constants.FHIRPATH_MIME },
                    QuestionnaireQueryType.InitialExpression
                ),
            _ => null
        };
    }

    private QuestionnaireQuery? ParseExpressionExtension(
        Extension extension,
        string[] supportedLanguages,
        QuestionnaireQueryType extensionType
    )
    {
        var errorMessage = (string message) =>
            $"{message} for {extensionType} in Questionnaire.Item {_state.LinkId}. Skipping resolution for this extension...";

        if (extension.Value is not Expression expression)
        {
            var type = ModelInfo.GetFhirTypeNameForType(extension.Value.GetType());
            // _logger.LogWarning(errorMessage($"Unexpected type {type}. Expected Expression"));
            return null;
        }

        if (!supportedLanguages.Contains(expression.Language))
        {
            // _logger.LogWarning(errorMessage($"Unsupported expression language {expression.Language}"));
            return null;
        }

        if (string.IsNullOrEmpty(expression.Expression_))
        {
            // _logger.LogWarning(errorMessage("Empty expression"));
            return null;
        }

        var query = CreateQuery(expression, extensionType);
        expression.AddExtension("ExpressionId", new Id { Value = query.Id.ToString() });

        return query;
    }

    private QuestionnaireQuery CreateQuery(
        Expression expr,
        QuestionnaireQueryType queryType,
        QuestionnaireQuery? from = null
    ) =>
        new(
            _idProvider.GetId(),
            expr,
            queryType,
            from is not null ? from.QuestionnaireItem : _state.CurrentItem,
            from is not null ? from.QuestionnaireResponseItem : _state.CurrentResponseItem
        );
}
