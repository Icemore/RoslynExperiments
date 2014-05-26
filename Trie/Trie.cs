using System;
using System.Collections.Generic;
using System.Linq;

namespace Dictionary {
    public class Trie {
        private readonly int maxSearchCount = 100;

        private readonly TrieNode root;

        public Trie() {
            root = new TrieNode {NodeKey = ' '};
        }

        public Trie(int maxSearchCount) {
            root = new TrieNode() {NodeKey = ' '};
            this.maxSearchCount = maxSearchCount;
        }

        public void Add(string str) {
            var cur = root;
            foreach (var ch in str) {
                if (cur.Children == null)
                    cur.Children = new Dictionary<char, TrieNode>();

                if (!cur.Children.Keys.Contains(ch)) {
                    var tmp = new TrieNode() {NodeKey = ch};
                    cur.Children.Add(ch, tmp);
                }

                cur = cur.Children[ch];
                cur.NoOfPrefix += 1;
            }

            cur.IsWord = true;
        }

        public List<string> SearchPrefix(string str, int top = -1) {
            var result = new List<string>();
            var cur = root;
            var prefix = String.Empty;
            var fail = false;

            foreach (var ch in str) {
                if (cur.Children == null) {
                    fail = true;
                    break;
                }

                if (cur.Children.Keys.Contains(ch)) {
                    prefix += ch;
                    cur = cur.Children[ch];
                } else {
                    fail = true;
                    break;
                }
            }

            if (cur.IsWord && !fail && result.Count < top)
                result.Add(prefix);

            top = (top == -1) ? maxSearchCount : top;
            GetMoreWords(cur, result, prefix, top);

            return result;
        }

        public int GetPrefixCount(string str) {
            var cur = root;
            foreach (var ch in str) {
                if (cur.Children.Keys.Contains(ch))
                    cur = cur.Children[ch];
                else
                    return 0;
            }

            return cur.NoOfPrefix;
        }

        public bool Contains(string str) {
            str = str.ToLower();
            var contains = true;
            var cur = root;

            foreach (var ch in str) {
                if (cur.Children != null && cur.Children.Keys.Contains(ch))
                    cur = cur.Children[ch];
                else {
                    contains = false;
                    break;
                }
            }

            return contains && cur.IsWord;
        }

        private void GetMoreWords(TrieNode cur, List<string> result, string prefix, int top) {
            if (cur.Children == null)
                return;

            foreach (var node in cur.Children.Values) {
                var tmp = prefix + node.NodeKey;
                if (node.IsWord) {
                    if (result.Count >= top)
                        break;
                    else
                        result.Add(tmp);
                }
                GetMoreWords(node, result, tmp, top);
            }
        }

        public IEnumerable<string> GetSimilarWords(string str, int errorCount = 1) {
            return GetSimilarWordsRec(root, str.ToLower(), errorCount, "");
        }

        private IEnumerable<string> GetSimilarWordsRec(TrieNode node, string str, int errorCount, string prefix) {
            if (string.IsNullOrEmpty(str)) {
                return node.IsWord ? new List<string> {prefix} : new List<string>();
            }

            if (node.Children == null) {
                return new List<string>();
            }

            var result = new List<IEnumerable<string>>();
            var ch = str[0];
            if (node.Children.ContainsKey(ch)) {
                result.Add(GetSimilarWordsRec(node.Children[ch], str.Remove(0, 1), errorCount, prefix + ch));
            }

            if (errorCount == 0) {
                return result.SelectMany(_ => _);
            }

            foreach (var key in node.Children.Keys.Where(_ => _ != ch)) {
                result.Add(GetSimilarWordsRec(node.Children[key], str.Remove(0, 1), errorCount - 1, prefix + key));
            }

            foreach (var key in node.Children.Keys.Where(_ => _ != ch)) {
                result.Add(GetSimilarWordsRec(node.Children[key], str, errorCount - 1, prefix + key));
            }

            return result.SelectMany(_ => _);
        }
    }
}