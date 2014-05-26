using System;
using System.Collections.Generic;
using System.Linq;
using Dictionary;

namespace WordProcessor {
    public class WordSplitter {
        private readonly Trie trie;
        private readonly string str;
        private readonly List<StringHolder> splittedString;

        public WordSplitter(Trie trie, String str) {
            this.trie = trie;
            this.str = str;
            splittedString = SplitString();
        }

        private List<StringHolder> SplitString() {
            var begin = 0;
            var result = new List<StringHolder>();
            for (var i = 0; i < str.Length; i++) {
                if (char.IsUpper(str[i])) {
                    if (begin != i) {
                        result.Add(new StringHolder(str.Substring(begin, i - begin), trie));
                        begin = i;
                    }
                }
            }

            result.Add(new StringHolder(str.Substring(begin, str.Length - begin), trie));
            return result;
        }

        public bool IsValid() {
            return splittedString.All(_ => _.IsValid());
        }

        public IEnumerable<string> GetAlternatives() {
            var results = new List<string>() {""};

            var fixd = false;

            
            foreach (var holder in splittedString)
            {
                if (holder.IsValid() || fixd) {
                    results = results.Select(_ => _ + holder.GetString()).ToList();
                } else {
                    results = results.SelectMany(_ => holder.GetAlternatives().Select(__ => _ + __)).ToList();
                    fixd = true;
                }
            }

            return results;
        }
    }
}