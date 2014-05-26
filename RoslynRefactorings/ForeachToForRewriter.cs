using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynRefactorings
{
    internal class ForeachToForRewriter : CSharpSyntaxRewriter
    {
        private readonly ExpressionSyntax _elemAccessExpression;
        private readonly ISymbol _elementSymbol;
        private readonly SemanticModel _semanticModel;

        public ForeachToForRewriter(ISymbol elementSymbol, string collectionName, string indexName,
            SemanticModel semanticModel)
        {
            _elementSymbol = elementSymbol;
            _semanticModel = semanticModel;


            var collection = SyntaxFactory.IdentifierName(collectionName);
            var index = SyntaxFactory.IdentifierName(indexName);
            var argument = SyntaxFactory.Argument(index);
            var argList = SyntaxFactory.BracketedArgumentList().AddArguments(new[] { argument });
            _elemAccessExpression = SyntaxFactory.ElementAccessExpression(collection, argList);
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.ValueText != _elementSymbol.Name) return node;

            var nodeSymbol = _semanticModel.GetSymbolInfo(node).Symbol;
            if (nodeSymbol != _elementSymbol) return node;

            return _elemAccessExpression
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }
    }
}