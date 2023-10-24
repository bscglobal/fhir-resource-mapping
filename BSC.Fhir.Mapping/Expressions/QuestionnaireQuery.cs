using System.Diagnostics.CodeAnalysis;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

public class QuestionnaireQuery
{
    private class Comparer : EqualityComparer<QuestionnaireQuery>
    {
        public override bool Equals(QuestionnaireQuery? x, QuestionnaireQuery? y)
        {
            return x?.Id == y?.Id;
        }

        public override int GetHashCode([DisallowNull] QuestionnaireQuery obj)
        {
            return obj.Id.GetHashCode();
        }
    }

    private static Comparer _comparer = new();

    private readonly HashSet<QuestionnaireQuery> _dependencies = new(_comparer);
    private readonly HashSet<QuestionnaireQuery> _dependants = new(_comparer);

    public int Id { get; }
    public Expression Expression { get; set; }
    public bool PopulationDependant { get; private set; } = false;
    public QuestionnaireQueryType QueryType { get; }
    public Questionnaire.ItemComponent? QuestionnaireItem { get; }
    public QuestionnaireResponse.ItemComponent? QuestionnaireResponseItem { get; }

    public QuestionnaireQuery(
        int id,
        Expression expression,
        QuestionnaireQueryType type,
        Questionnaire.ItemComponent? questionnaireItem,
        QuestionnaireResponse.ItemComponent? questionnaireResponseItem
    )
    {
        Id = id;
        Expression = expression;
        QueryType = type;
        QuestionnaireItem = questionnaireItem;
        QuestionnaireResponseItem = questionnaireResponseItem;
    }

    public bool AddDependency(QuestionnaireQuery dependency, bool recursive = true)
    {
        if (dependency.Id == Id)
        {
            return false;
        }

        _dependencies.Add(dependency);

        foreach (var dependant in _dependants)
        {
            if (!dependant.AddDependency(dependency))
            {
                return false;
            }
        }

        if (recursive)
        {
            return dependency.AddDependant(this, false);
        }
        else
        {
            return true;
        }
    }

    public bool AddDependant(QuestionnaireQuery dependant, bool recursive = true)
    {
        if (dependant.Id == Id)
        {
            return false;
        }

        _dependants.Add(dependant);

        foreach (var dependency in _dependencies)
        {
            if (!dependency.AddDependant(dependant))
            {
                return false;
            }
        }

        if (recursive)
        {
            return dependant.AddDependency(this, false);
        }
        else
        {
            return true;
        }
    }

    public void MakePopulationDependant()
    {
        PopulationDependant = true;

        foreach (var dep in _dependants)
        {
            dep.MakePopulationDependant();
        }
    }

    public QuestionnaireQuery[] Dependants() => _dependants.ToArray();

    public QuestionnaireQuery[] Dependencies() => _dependencies.ToArray();
}
