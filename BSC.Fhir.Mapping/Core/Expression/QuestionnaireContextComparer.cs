using System.Diagnostics.CodeAnalysis;

namespace BSC.Fhir.Mapping.Core.Expressions;

public class QuestionnaireContextComparer<T> : EqualityComparer<IQuestionnaireContext<T>>
{
    public static new QuestionnaireContextComparer<T> Default = new();

    public override bool Equals(IQuestionnaireContext<T>? x, IQuestionnaireContext<T>? y)
    {
        return x?.Id == y?.Id;
    }

    public override int GetHashCode([DisallowNull] IQuestionnaireContext<T> obj)
    {
        return obj.Id.GetHashCode();
    }
}
