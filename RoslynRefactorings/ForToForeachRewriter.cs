using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RoslynRefactorings
{
    class ForToForeachRewriter : CSharpSyntaxRewriter
    {
        private readonly ISymbol _indexSymbol;
        private readonly ISymbol _collectionSymbol;
        private readonly string _iteratorName;
        private readonly SemanticModel _semanticModel;

        public ForToForeachRewriter(ISymbol indexSymbol, ISymbol collectionSymbol, string iteratorName, SemanticModel semanticModel)
        {
            _indexSymbol = indexSymbol;
            _collectionSymbol = collectionSymbol;
            _iteratorName = iteratorName;
            _semanticModel = semanticModel;
        }

        public override SyntaxNode VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (_semanticModel.GetSymbolInfo(node.Expression).Symbol != _collectionSymbol ||
                _semanticModel.GetSymbolInfo(node.ArgumentList.Arguments[0].Expression).Symbol != _indexSymbol)
            {
                return base.VisitElementAccessExpression(node);
            }

            return SyntaxFactory.IdentifierName(_iteratorName)
                .WithLeadingTrivia(node.GetLeadingTrivia())
                .WithTrailingTrivia(node.GetTrailingTrivia());
        }
    }
}
