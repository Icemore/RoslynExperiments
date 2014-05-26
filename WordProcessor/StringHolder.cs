using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dictionary;

namespace WordProcessor {
    public class StringHolder {
        private readonly string str;
        private readonly Trie trie;
        private readonly bool isTitleCase;

        public StringHolder(String str, Trie trie) {
            this.str = str;
            this.trie = trie;
            isTitleCase = char.IsUpper(str[0]);
        }

        public string GetString() {
            return str;
        }

        public bool IsValid() {
            return trie.Contains(str);
        }

        public IEnumerable<String> GetAlternatives() {
            var lower = str.ToLower();
            var similarWords = trie.GetSimilarWords(lower);
            return isTitleCase ? similarWords.Select(_ => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(_)) : similarWords;
        }
    }
}