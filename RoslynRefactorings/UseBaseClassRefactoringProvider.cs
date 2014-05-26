using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace RoslynRefactorings
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    internal class UseBaseClassRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "useBaseClass";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            // Find the node at the selection.
            var node = root.FindNode(textSpan);

            var typeDecl = node as TypeDeclarationSyntax;
            if (typeDecl == null || typeDecl.BaseList == null)
            {
                return null;
            }

            var action = CodeAction.Create("Use base class when possible", c => UseBaseClass(document, typeDecl, c));
            return new[] { action };
        }

        private async Task<Document> UseBaseClass(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var oldTypeSymbol = semanticModel.GetDeclaredSymbol(typeDecl);

            var baseClassSyntax = typeDecl.BaseList.Types.First();
            var baseClassType = semanticModel.GetTypeInfo(baseClassSyntax).Type;

            var rewriter = new UseBaseClassRerwiter(semanticModel, oldTypeSymbol, baseClassType);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var newRoot = rewriter.Visit(root);

            return document.WithSyntaxRoot(newRoot);
        }

        private async Task<Solution> ReverseTypeNameAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
        {
            // Produce a reversed version of the type declaration's identifier token.
            var identifierToken = typeDecl.Identifier;
            var newName = new string(identifierToken.Text.Reverse().ToArray());

            // Get the symbol representing the type to be renamed.
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);

            // Produce a new solution that has all references to that type renamed, including the declaration.
            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.GetOptions();
            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);

            // Return the new solution with the now-uppercase type name.
            return newSolution;
        }
    }
}