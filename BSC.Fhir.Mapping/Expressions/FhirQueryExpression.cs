using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

using BaseList = IReadOnlyCollection<Base>;

public class FhirQueryExpression : QuestionnaireExpression<BaseList>
{
    public FhirQueryExpression(
        int id,
        string? name,
        string expr,
        Scope scope,
        QuestionnaireContextType type,
        Questionnaire.ItemComponent? questionnaireItem,
        QuestionnaireResponse.ItemComponent? questionnaireResponseItem
    )
        : base(id, name, expr, Constants.FHIR_QUERY_MIME, scope, type, questionnaireItem, questionnaireResponseItem) { }

    public override void SetValue(BaseList? value)
    {
        if (value is null)
        {
            var newResource = ConstructResourceFromExpression(Expression);
            if (newResource is not null)
            {
                value = new[] { newResource };
            }
        }

        base.SetValue(value);
    }

    private static Resource? ConstructResourceFromExpression(string extensionValue)
    {
        var resourceName = extensionValue.Split('?').First();

        var className = $"Hl7.Fhir.Model.{resourceName[0..1].ToUpper() + resourceName[1..]}";

        var asm = AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(asm => asm.GetName().Name == "Hl7.Fhir.R4.Core");

        var type = asm?.GetType(className);

        if (type is null)
        {
            return null;
        }

        return Activator.CreateInstance(type) as Resource;
    }
}
