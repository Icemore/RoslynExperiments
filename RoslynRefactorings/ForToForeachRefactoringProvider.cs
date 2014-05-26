using System.Collections.Generic;
using System.Linq;
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
    class ForToForeachRefactoringProvider : ICodeRefactoringProvider
    {
        public const string RefactoringId = "forToForeach";

        public async Task<IEnumerable<CodeAction>> GetRefactoringsAsync(Document document, TextSpan textSpan, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var node = root.FindNode(textSpan);

            var loopSt = node as ForStatementSyntax;
            if (loopSt == null)
            {
                return null;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            ISymbol indexSymbol, collectionSymbol;
            if (!canBeConverted(loopSt, semanticModel, out indexSymbol, out collectionSymbol)) return null;

            var action = CodeAction.Create("Convert for to foreach", c => DoAction(document, loopSt, c));
            return new[] {action};
        }

        private async Task<Document> DoAction(Document document, ForStatementSyntax loopSt, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            ISymbol indexSymbol, collectionSymbol;
            canBeConverted(loopSt, semanticModel, out indexSymbol, out collectionSymbol);

            var iteratorName = Helper.GetFreeName("it", semanticModel, loopSt.Statement);

            var rewriter = new ForToForeachRewriter(indexSymbol, collectionSymbol, iteratorName, semanticModel);
            var newBody = (StatementSyntax) rewriter.Visit(loopSt.Statement);

            var foreachStatement = (ForEachStatementSyntax)
                SyntaxFactory.ParseStatement(string.Format("foreach(var {0} in {1})", iteratorName,
                    collectionSymbol.Name));
            foreachStatement = foreachStatement.WithStatement(newBody)
                .WithLeadingTrivia(loopSt.GetLeadingTrivia())
                .WithTrailingTrivia(loopSt.GetTrailingTrivia())
                .WithCloseParenToken(loopSt.CloseParenToken);

            var newRoot = root.ReplaceNode((StatementSyntax) loopSt, foreachStatement);

            return document.WithSyntaxRoot(newRoot);
        }

        private bool canBeConverted(ForStatementSyntax loopSt, SemanticModel semanticModel, out ISymbol indexSymbol, out ISymbol collectionSymbol)
        {
            collectionSymbol = indexSymbol = null;
            IdentifierNameSyntax collectionSyntax;
            if(!checkForStatement(loopSt, semanticModel, out collectionSyntax)) return false;

            var indexSyntax = loopSt.Declaration.Variables[0];

            indexSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, indexSyntax);
            collectionSymbol = semanticModel.GetSymbolInfo(collectionSyntax).Symbol;

            if (!checkIndex(loopSt, semanticModel, indexSymbol, collectionSymbol)) return false;
            if (!checkCollection(collectionSyntax, loopSt, semanticModel)) return false;

            return true;
        }

        private bool checkCollection(IdentifierNameSyntax collectionSyntax, ForStatementSyntax loopSt, SemanticModel semanticModel)
        {
            var collectionType = semanticModel.GetTypeInfo(collectionSyntax).Type;

            if (collectionType == null || (
                collectionType.TypeKind != TypeKind.ArrayType &&
                collectionType.OriginalDefinition.ToDisplayString() != "System.Collections.Generic.List<T>"))
            {
                return false;
            }

            var collectionSymbol = semanticModel.GetSymbolInfo(collectionSyntax).Symbol;

            var collectionOccurences = loopSt.Statement.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(x => semanticModel.GetSymbolInfo(x).Symbol == collectionSymbol);

            return collectionOccurences.All(x => x.Parent.CSharpKind() == SyntaxKind.ElementAccessExpression);
        }

        private static bool checkIndex(ForStatementSyntax loopSt, SemanticModel semanticModel, ISymbol indexSymbol,
            ISymbol collectionSymbol)
        {
            var indexOccurrences =
                loopSt.Statement.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(x => x.Identifier.ValueText == indexSymbol.Name)
                    .Where(x => ModelExtensions.GetSymbolInfo(semanticModel, x).Symbol == indexSymbol)
                    .Select(x => x.Parent);


            foreach (var node in indexOccurrences)
            {
                var argSyntax = node as ArgumentSyntax;
                if (argSyntax == null) return false;

                var bracketedArgList = argSyntax.Parent as BracketedArgumentListSyntax;
                if (bracketedArgList == null) return false;

                var elemAccess = bracketedArgList.Parent as ElementAccessExpressionSyntax;
                if (elemAccess == null) return false;

                if (ModelExtensions.GetSymbolInfo(semanticModel, elemAccess.Expression).Symbol != collectionSymbol)
                    return false;
            }

            return true;
        }

        private bool checkForStatement(ForStatementSyntax loopSt, SemanticModel semanticModel, out IdentifierNameSyntax collection)
        {
            collection = null;

            if (loopSt.Initializers.Count != 0 ||
                loopSt.Declaration.Variables.Count != 1 ||
                loopSt.Incrementors.Count != 1) return false;

            var indexSyntax = loopSt.Declaration.Variables[0].Identifier;

            if (loopSt.Declaration.Variables[0].Initializer.Value.ToString() != "0") return false;
            if (!checkIncrementor(loopSt.Incrementors[0], indexSyntax)) return false;
            if (!checkCondition(loopSt.Condition, indexSyntax, out collection)) return false;

            return true;
        }

        private bool checkCondition(ExpressionSyntax expression, SyntaxToken identifier, out IdentifierNameSyntax collection)
        {
            collection = null;
            if (expression.CSharpKind() != SyntaxKind.LessThanExpression) return false;
            
            // shoud be "i < a.Count"
            var lessExpr = (BinaryExpressionSyntax) expression;
            
            //check left
            var left = lessExpr.Left as IdentifierNameSyntax;
            if (left == null || !left.Identifier.IsEquivalentTo(identifier)) return false;

            //check right
            var right = lessExpr.Right as MemberAccessExpressionSyntax;
            if (right == null) return false;
            if(right.Name.Identifier.ValueText != "Count" && right.Name.Identifier.ValueText != "Length") return false;
            if (right.Expression.CSharpKind() != SyntaxKind.IdentifierName) return false;

            collection = right.Expression as IdentifierNameSyntax;
            return true;
        }

        private bool checkIncrementor(ExpressionSyntax incementor, SyntaxToken identyfier)
        {
            string[] acceptableIncrementors = {"{0}++", "++{0}", "{0}+=1", "{0}={0}+1"};

            foreach (var str in acceptableIncrementors)
            {
                var expr = SyntaxFactory.ParseExpression(string.Format(str, identyfier.ValueText));
                if (incementor.IsEquivalentTo(expr)) return true;
            }

            return false;
        }
    }
}
