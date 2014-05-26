using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dictionary;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using WordProcessor;

namespace RoslynRefactorings.SpellChecker {
    [ExportCodeFixProvider(DiagnosticAnalyzer.DiagnosticId, LanguageNames.CSharp)]
    internal class CodeFixProvider : ICodeFixProvider {
        private static readonly Trie trie = new DictionaryLoader("dictionary/english.dic", "dictionary/jetbrains.dic").Trie;
        private const int limitOfSuggestions = 5;

        public IEnumerable<string> GetFixableDiagnosticIds() {
            return new[] {DiagnosticAnalyzer.DiagnosticId};
        }

        public async Task<IEnumerable<CodeAction>> GetFixesAsync(Document document, TextSpan span, IEnumerable<Diagnostic> diagnostics, CancellationToken cancellationToken) {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var diagnosticSpan = diagnostics.First().Location.SourceSpan;

            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();
            var identifierTokenText = declaration.Identifier.Text;

            var splitter = new WordSplitter(trie, identifierTokenText);

            if (splitter.IsValid()) {
                return null;
            }

            return splitter.GetAlternatives().Select(suggestion => CodeAction.Create(string.Format("Rename to '{0}'", suggestion), c => RenameAsync(document, declaration, suggestion, c))).Take(limitOfSuggestions);
        }

        private async Task<Solution> RenameAsync(Document document, SyntaxNode declaration, string alternative, CancellationToken cancellationToken) {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var typeSymbol = semanticModel.GetDeclaredSymbol(declaration, cancellationToken);

            var originalSolution = document.Project.Solution;
            var optionSet = originalSolution.Workspace.GetOptions();
            var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, alternative, optionSet, cancellationToken).ConfigureAwait(false);

            return newSolution;
        }
    }
}