using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace RoslynRefactorings
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class ForeachToForRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "foreachToFor";


        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan textSpan,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(textSpan);

            var loopSt = node as ForEachStatementSyntax;
            if (loopSt == null)
            {
                return null;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (!canBeConverted(loopSt, semanticModel)) return null;

            var action = CodeAction.Create("Convert foreach to for", c => DoAction(document, loopSt, c));
            return new[] {action};
        }

        private async Task<Document> DoAction(Document document, ForEachStatementSyntax loopSt,
            CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var elementSymbol = semanticModel.GetDeclaredSymbol(loopSt);
            var collectionSymbol = semanticModel.GetSymbolInfo(loopSt.Expression).Symbol;

            var indexName = Helper.GetFreeName("i", semanticModel, loopSt.Statement);

            var rewriter = new ForeachToForRewriter(elementSymbol, collectionSymbol.Name, indexName, semanticModel);
            var newBody = (StatementSyntax) rewriter.Visit(loopSt.Statement);

            var newLoop = ((ForStatementSyntax)
                SyntaxFactory.ParseStatement(string.Format("for(int {0} = 0; {0} < {1}.{2}; {0}++)", indexName,
                    collectionSymbol.Name, getSizeMethodName(loopSt.Expression, semanticModel))));
            newLoop = newLoop.WithStatement(newBody)
                .WithLeadingTrivia(loopSt.GetLeadingTrivia())
                .WithTrailingTrivia(loopSt.GetTrailingTrivia())
                .WithCloseParenToken(loopSt.CloseParenToken);

            var newRoot = root.ReplaceNode((StatementSyntax) loopSt, newLoop);

            return document.WithSyntaxRoot(newRoot);
        }

        private string getSizeMethodName(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var collectionType = semanticModel.GetTypeInfo(expression).Type;

            return collectionType.TypeKind == TypeKind.ArrayType ? "Length" : "Count";
        }

        private bool canBeConverted(ForEachStatementSyntax loopStatementSyntax, SemanticModel semanticModel)
        {
            var collectionType = semanticModel.GetTypeInfo(loopStatementSyntax.Expression).Type;

            if (collectionType != null && (
                collectionType.TypeKind == TypeKind.ArrayType ||
                collectionType.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.List<T>"))
            {
                return true;
            }


            return false;
        }
    }
}