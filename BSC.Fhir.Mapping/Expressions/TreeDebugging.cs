using System.Text;
using System.Text.Json;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

internal static class TreeDebugging
{
    public static string PrintTree(Scope scope, bool printDeps = false)
    {
        return "\n" + PrintTree(scope, "    ", printDeps: printDeps);
    }

    private static string PrintTree(
        Scope scope,
        string prefix = "",
        string linkIdPrefix = "",
        bool addIndent = false,
        bool addBar = false,
        Scope? originalScope = null,
        bool printDeps = false
    )
    {
        var str = new StringBuilder();
        if (addIndent)
        {
            str.AppendLine(prefix + "│");
        }
        str.AppendLine(prefix + linkIdPrefix + (scope.Item?.LinkId ?? "root"));

        prefix = prefix + (addBar ? "│" : " ") + (addIndent ? "     " : "");
        if (scope == originalScope)
        {
            str.AppendLine(prefix + "│");
            str.AppendLine(prefix + "Circular tree detected!");
            return str.ToString();
        }

        var hasChildren = scope.Children.Count > 0;
        if (scope.Context.Count > 0)
        {
            str.AppendLine(prefix + "│");
            str.AppendLine(prefix + "├─ Context");

            for (var i = 0; i < scope.Context.Count; i++)
            {
                var context = scope.Context[i];

                var lastContext = i == scope.Context.Count - 1;
                str.Append(
                    PrintContext(context, prefix + "│     ", lastContext ? "└─ " : "├─ ", true, !lastContext, printDeps)
                );
            }
        }

        str.AppendLine(prefix + "│");
        str.AppendLine(prefix + "├─ " + "HasRequiredAnswers");
        var childPrefix = prefix + "│     ";
        str.AppendLine(childPrefix + "│");
        str.AppendLine(childPrefix + "└─ " + (scope.HasRequiredAnswers()));

        str.AppendLine(prefix + "│");
        str.AppendLine(prefix + "├─ " + "Required");
        str.AppendLine(childPrefix + "│");
        str.AppendLine(childPrefix + "└─ " + (scope.Item?.Required ?? false));

        str.AppendLine(prefix + "│");
        str.AppendLine(prefix + (hasChildren ? "├─ " : "└─ ") + "ResponseItem Answer");
        childPrefix = prefix + (hasChildren ? "│     " : "      ");
        str.AppendLine(childPrefix + "│");
        str.AppendLine(
            childPrefix
                + "└─ "
                + (
                    scope.ResponseItem is not null && scope.ResponseItem.Answer.Count > 0
                        ? JsonSerializer.Serialize(scope.ResponseItem.Answer)
                        : "Nope"
                )
        );

        if (hasChildren)
        {
            str.AppendLine(prefix + "│");
            str.AppendLine(prefix + "└─ Children");
        }

        for (var i = 0; i < scope.Children.Count; i++)
        {
            var child = scope.Children[i];

            var lastChild = i == scope.Children.Count - 1;
            str.Append(
                PrintTree(child, prefix + "      ", lastChild ? "└─ " : "├─ ", true, !lastChild, originalScope ?? scope)
            );
        }

        return str.ToString();
    }

    public static string PrintContext<T>(
        IQuestionnaireContext<T> context,
        string prefix = "",
        string titlePrefix = "",
        bool addIndent = false,
        bool addBar = false,
        bool printDeps = false
    )
    {
        var str = new StringBuilder();
        if (addIndent)
        {
            str.AppendLine(prefix + "│");
        }
        str.AppendLine(
            prefix + titlePrefix + context.Id + $" ({(context.Name is not null ? context.Name : "anonymous")})"
        );
        prefix = prefix + (addBar ? "│" : " ") + (addIndent ? "     " : "");

        str.AppendLine(prefix + "│");
        str.AppendLine(prefix + "├─ Type");
        str.AppendLine(prefix + "│     │");
        str.AppendLine(prefix + "│     └─ " + context.Type.ToString());

        str.AppendLine(prefix + "│");
        str.AppendLine(prefix + "├─ Resolved");
        str.AppendLine(prefix + "│     │");
        str.AppendLine(
            prefix + "│     └─ " + (context.Value is not null ? JsonSerializer.Serialize(context.Value) : "Nope")
        );

        var expression = context as IQuestionnaireExpression<IReadOnlyCollection<Base>>;
        var deps = expression?.Dependencies.ToArray();
        var hasDeps = deps?.Length > 0;

        if (expression is not null)
        {
            str.AppendLine(prefix + "│");
            str.AppendLine(prefix + "├─ Dependencies Resolved");
            str.AppendLine(prefix + "│     │");
            str.AppendLine(prefix + "│     └─ " + expression.DependenciesResolved());
            str.AppendLine(prefix + "│");
            str.AppendLine(prefix + (hasDeps ? "├─ " : "└─ ") + expression.Expression);
        }

        if (hasDeps && printDeps)
        {
            var depsPrefix = prefix + "      ";
            str.AppendLine(prefix + "│");
            str.AppendLine(prefix + "└─ Dependencies");

            for (var i = 0; i < deps!.Length; i++)
            {
                var dep = deps[i];

                var lastDep = i == deps.Length - 1;
                str.Append(PrintContext(dep, depsPrefix, lastDep ? "└─ " : "├─ ", true, !lastDep));
            }
        }

        return str.ToString();
    }
}
