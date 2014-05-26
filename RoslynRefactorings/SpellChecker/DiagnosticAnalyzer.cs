using System;
using System.Collections.Immutable;
using System.Threading;
using Dictionary;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using WordProcessor;

namespace RoslynRefactorings.SpellChecker {
    [DiagnosticAnalyzer]
    [ExportDiagnosticAnalyzer(DiagnosticId, LanguageNames.CSharp)]
    public class DiagnosticAnalyzer : ISymbolAnalyzer {
        internal const string DiagnosticId = "SpellChecker";
        internal const string Description = "Must be a typo here";
        internal const string MessageFormat = "Typo in '{0}' detected";
        internal const string Category = "Naming";
        internal static readonly Trie Trie = new DictionaryLoader("dictionary/english.dic", "dictionary/jetbrains.dic").Trie;

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Description, MessageFormat, Category, DiagnosticSeverity.Warning);

        public ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics {
            get { return ImmutableArray.Create(Rule); }
        }

        public ImmutableArray<SymbolKind> SymbolKindsOfInterest {
            get { return ImmutableArray.Create(SymbolKind.NamedType, SymbolKind.Method); }
        }

        public void AnalyzeSymbol(ISymbol symbol, Compilation compilation, Action<Diagnostic> addDiagnostic, CancellationToken cancellationToken) {
            var namedTypeSymbol = symbol;
            if (!new WordSplitter(Trie, namedTypeSymbol.Name).IsValid()) {
                addDiagnostic(Diagnostic.Create(Rule, namedTypeSymbol.Locations[0], namedTypeSymbol.Name));
            }
        }
    }
}