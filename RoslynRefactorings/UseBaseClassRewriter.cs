using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Simplification;
using System.Collections.Immutable;

namespace RoslynRefactorings
{
    internal class UseBaseClassRerwiter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _semanticModel;
        private readonly ITypeSymbol _oldClass;
        private readonly ITypeSymbol _baseClass;
        private readonly Dictionary<Tuple<MethodDeclarationSyntax, IParameterSymbol>, int> _visited;

        public UseBaseClassRerwiter(SemanticModel semanticModel, ITypeSymbol oldClass, ITypeSymbol baseClass)
        {
            _semanticModel = semanticModel;
            _oldClass = oldClass;
            _baseClass = baseClass;
            _visited = new Dictionary<Tuple<MethodDeclarationSyntax, IParameterSymbol>, int>();
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var parameters = new List<ParameterSyntax>();

            foreach (var paramSyntax in node.ParameterList.Parameters)
            {
                var param = _semanticModel.GetDeclaredSymbol(paramSyntax);
                var newParamSyntax = paramSyntax;

                if (param.Type == _oldClass && canUseBase(node, param))
                {

                    var oldType = paramSyntax.Type;
                    var newType =
                        SyntaxFactory.ParseTypeName(_baseClass.ToDisplayString())
                            .WithLeadingTrivia(oldType.GetLeadingTrivia())
                            .WithTrailingTrivia(oldType.GetTrailingTrivia());
                    newType = newType.WithAdditionalAnnotations(Simplifier.Annotation);

                    newParamSyntax = paramSyntax.WithType(newType);
                }

                parameters.Add(newParamSyntax);
            }

            var newParamList = node.ParameterList.WithParameters(SyntaxFactory.SeparatedList(parameters)).WithAdditionalAnnotations(Formatter.Annotation);
            return node.WithParameterList(newParamList);
        }

        private bool canUseBase(MethodDeclarationSyntax method, IParameterSymbol param)
        {
            var tuple = Tuple.Create(method, param);
            if (_visited.ContainsKey(tuple))
            {
                return (_visited[tuple] != -1);
            }

            _visited[tuple] = 0;

            var memberAccessList = method.DescendantNodes().AsImmutable().OfType<MemberAccessExpressionSyntax>().AsImmutable();
            var invocationList = method.DescendantNodes().AsImmutable().OfType<InvocationExpressionSyntax>().AsImmutable();
            var localDeclarations = method.DescendantNodes().AsImmutable().OfType<LocalDeclarationStatementSyntax>().AsImmutable();

            var res = true;
            res = res && checkMethodAccess(memberAccessList, param);
            res = res && checkPassingAsParameter(invocationList, param);
            res = res && checkLocalDeclarations(localDeclarations, param);

            _visited[tuple] = res ? 1 : -1;

            return res;
        }

        private bool checkLocalDeclarations(ImmutableArray<LocalDeclarationStatementSyntax> localDeclarationList, IParameterSymbol param)
        {
            foreach (var localDeclarationSyntax in localDeclarationList)
            {
                var declarators = localDeclarationSyntax.Declaration.Variables;
                foreach (var declarator in declarators)
                {
                    var initializer = declarator.Initializer;
                    var initializerSymbol = _semanticModel.GetSymbolInfo(initializer.Value).Symbol;

                    if (initializerSymbol != param) continue;

                    var declaringType = localDeclarationSyntax.Declaration.Type;
                    var typeSymbol = _semanticModel.GetSymbolInfo(declaringType).Symbol;

                    if (typeSymbol != _baseClass)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool checkPassingAsParameter(ImmutableArray<InvocationExpressionSyntax> invocationList, IParameterSymbol param)
        {
            foreach (var invocation in invocationList)
            {
                var method = _semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                var argList = invocation.ArgumentList.Arguments;

                for (int i = 0; i < argList.Count; i++)
                {
                    var arg = _semanticModel.GetSymbolInfo(argList[i].Expression).Symbol;
                    if (arg != param) continue;


                    if (method.Parameters[i].Type != _baseClass &&
                        !canUseBase(method.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).OfType<MethodDeclarationSyntax>().First(), method.Parameters[i]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool checkMethodAccess(ImmutableArray<MemberAccessExpressionSyntax> memberAccessList, IParameterSymbol param)
        {
            var calls = memberAccessList.WhereAsArray(i => _semanticModel.GetSymbolInfo(i.Expression).Symbol == param);

            foreach (var call in calls)
            {
                var fun = _semanticModel.GetSymbolInfo(call).Symbol;
                if (_oldClass.GetMembers().Contains(fun))
                {
                    return false;
                }
            }

            return true;
        }
    }
}