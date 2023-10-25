using BSC.Fhir.Mapping.Core.Expressions;

namespace BSC.Fhir.Mapping.Expressions;

public class ResolvedContext<T>
{
    public int Id { get; }
    public string? Name { get; }
    public T Value { get; }

    public ResolvedContext(IQuestionnaireContext<T> context)
    {
        if (context.Value is null)
        {
            throw new ArgumentException("Passed context has null Value", nameof(context));
        }

        Name = context.Name;
        Id = context.Id;
        Value = context.Value;
    }
}
