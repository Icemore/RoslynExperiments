using System.Collections.Generic;

namespace Dictionary {
    public class TrieNode {
        public char NodeKey { get; set; }
        public int NoOfPrefix { get; set; }
        public Dictionary<char, TrieNode> Children { get; set; }
        public bool IsWord { get; set; }
    }
}