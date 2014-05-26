using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynRefactorings
{
    class Helper
    {
        public static string GetFreeName(string name, SemanticModel semanticModel, StatementSyntax statement)
        {
            var namesDeclaredInside = semanticModel.AnalyzeDataFlow(statement).VariablesDeclared.Select(x => x.Name);
            var namesFromOutside = semanticModel.LookupSymbols(statement.Span.End - 1).Select(x => x.Name);
            var takenNames = namesDeclaredInside.Union(namesFromOutside).AsImmutable();

            if (!takenNames.Contains(name)) return name;

            var add = 1;

            while (takenNames.Contains(name + add))
            {
                add++;
            }

            return name + add;
        }
    }
}