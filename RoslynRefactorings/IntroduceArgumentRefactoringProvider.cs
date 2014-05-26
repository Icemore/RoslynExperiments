using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using Microsoft.CodeAnalysis.Text;

namespace RoslynRefactorings
{
    [ExportCodeRefactoringProvider(RefactoringId, LanguageNames.CSharp)]
    class IntroduceArgumentRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "introduceArgument";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var node = root.FindNode(span);

            var argument = node as ArgumentSyntax;

            if (argument == null ||
                argument.Expression.CSharpKind() != SyntaxKind.IdentifierName ||
                !isInsideMethod(argument))
            {
                return null;
            }

            var invokedMethod = argument.FirstAncestorOrSelf<InvocationExpressionSyntax>().Expression;

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            if (semanticModel.GetSymbolInfo(argument.Expression).Symbol != null ||
                semanticModel.GetSymbolInfo(invokedMethod).CandidateSymbols.Length != 1 ||
                semanticModel.GetSymbolInfo(invokedMethod).CandidateSymbols[0].Kind != SymbolKind.Method)
            {
                return null;
            }

            var action = CodeAction.Create("Introduce argument", c => IntroduceArgument(document, argument, c));
            return new[] {action};
        }

        private async Task<Document> IntroduceArgument(Document document, ArgumentSyntax argument, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var identifier = argument.Expression as IdentifierNameSyntax;
            var invokedMethodSyntax = argument.FirstAncestorOrSelf<InvocationExpressionSyntax>().Expression;

            var invokedMethod = (IMethodSymbol) semanticModel.GetSymbolInfo(invokedMethodSyntax).CandidateSymbols[0];
            
            var methodDeclaration = argument.FirstAncestorOrSelf<MethodDeclarationSyntax>();

            
            var invocation = argument.FirstAncestorOrSelf<InvocationExpressionSyntax>();
            int argumentIndex = invocation.ArgumentList.Arguments.IndexOf(argument);

            var argumentType = invokedMethod.Parameters[argumentIndex].Type;
            var typeSyntax = SyntaxFactory.ParseTypeName(argumentType.ToDisplayString());
            typeSyntax = typeSyntax.WithAdditionalAnnotations(Simplifier.Annotation);

            var newParam = SyntaxFactory.Parameter(identifier.Identifier).WithType(typeSyntax);
            newParam = newParam.WithAdditionalAnnotations(Formatter.Annotation);

            var newMethodDeclaration = methodDeclaration.AddParameterListParameters(new[] {newParam});

            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = root.ReplaceNode(methodDeclaration, newMethodDeclaration);

            return document.WithSyntaxRoot(newRoot);
        }

        private bool isInsideMethod(ArgumentSyntax argument)
        {
            return argument.FirstAncestorOrSelf<MethodDeclarationSyntax>() != null;
        }
    }
}
