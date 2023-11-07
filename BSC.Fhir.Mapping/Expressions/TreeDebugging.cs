using System.Text.Json;
using BSC.Fhir.Mapping.Core.Expressions;
using Hl7.Fhir.Model;

namespace BSC.Fhir.Mapping.Expressions;

internal static class TreeDebugging
{
    public static void PrintTree(Scope scope, bool printDeps = false)
    {
        Console.WriteLine();
        PrintTree(scope, "    ", printDeps: printDeps);
        Console.WriteLine();
        Console.WriteLine();
    }

    private static void PrintTree(
        Scope scope,
        string prefix = "",
        string linkIdPrefix = "",
        bool addIndent = false,
        bool addBar = false,
        Scope? originalScope = null,
        bool printDeps = false
    )
    {
        if (addIndent)
        {
            Console.WriteLine(prefix + "│");
        }
        Console.WriteLine(prefix + linkIdPrefix + (scope.Item?.LinkId ?? "root"));

        prefix = prefix + (addBar ? "│" : " ") + (addIndent ? "     " : "");
        if (scope == originalScope)
        {
            Console.WriteLine(prefix + "│");
            Console.WriteLine(prefix + "Circular tree detected!");
            return;
        }

        var hasChildren = scope.Children.Count > 0;
        if (scope.Context.Count > 0)
        {
            Console.WriteLine(prefix + "│");
            Console.WriteLine(prefix + (hasChildren ? "├─ " : "└─ ") + "Context");

            for (var i = 0; i < scope.Context.Count; i++)
            {
                var context = scope.Context[i];

                var lastContext = i == scope.Context.Count - 1;
                PrintContext(
                    context,
                    prefix + (hasChildren ? "│     " : "      "),
                    lastContext ? "└─ " : "├─ ",
                    true,
                    !lastContext,
                    printDeps
                );
            }
        }

        Console.WriteLine(prefix + "│");
        Console.WriteLine(prefix + (hasChildren ? "├─ " : "└─ ") + "ResponseItem Answer");
        var childPrefix = prefix + (hasChildren ? "│     " : "      ");
        Console.WriteLine(childPrefix + "│");
        Console.WriteLine(
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
            Console.WriteLine(prefix + "│");
            Console.WriteLine(prefix + "└─ Children");
        }

        for (var i = 0; i < scope.Children.Count; i++)
        {
            var child = scope.Children[i];

            var lastChild = i == scope.Children.Count - 1;
            PrintTree(child, prefix + "      ", lastChild ? "└─ " : "├─ ", true, !lastChild, originalScope ?? scope);
        }
    }

    public static void PrintContext<T>(
        IQuestionnaireContext<T> context,
        string prefix = "",
        string titlePrefix = "",
        bool addIndent = false,
        bool addBar = false,
        bool printDeps = false
    )
    {
        if (addIndent)
        {
            Console.WriteLine(prefix + "│");
        }
        Console.WriteLine(
            prefix + titlePrefix + context.Id + $" ({(context.Name is not null ? context.Name : "anonymous")})"
        );
        prefix = prefix + (addBar ? "│" : " ") + (addIndent ? "     " : "");

        Console.WriteLine(prefix + "│");
        Console.WriteLine(prefix + "├─ Resolved");
        Console.WriteLine(prefix + "│     │");
        Console.WriteLine(
            prefix + "│     └─ " + (context.Value is not null ? JsonSerializer.Serialize(context.Value) : "Nope")
        );

        var expression = context as IQuestionnaireExpression<IReadOnlyCollection<Base>>;
        var deps = expression?.Dependencies.ToArray();
        var hasDeps = deps?.Length > 0;

        if (expression is not null)
        {
            Console.WriteLine(prefix + "│");
            Console.WriteLine(prefix + "├─ Dependencies Resolved");
            Console.WriteLine(prefix + "│     │");
            Console.WriteLine(prefix + "│     └─ " + expression.DependenciesResolved());
            Console.WriteLine(prefix + "│");
            Console.WriteLine(prefix + (hasDeps ? "├─ " : "└─ ") + expression.Expression);
        }

        if (hasDeps && printDeps)
        {
            var depsPrefix = prefix + "      ";
            Console.WriteLine(prefix + "│");
            Console.WriteLine(prefix + "└─ Dependencies");

            for (var i = 0; i < deps!.Length; i++)
            {
                var dep = deps[i];

                var lastDep = i == deps.Length - 1;
                PrintContext(dep, depsPrefix, lastDep ? "└─ " : "├─ ", true, !lastDep);
            }
        }
    }
}
