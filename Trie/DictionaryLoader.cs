using System.IO;
using System.Text;

namespace Dictionary {
    public class DictionaryLoader {
        public readonly Trie Trie;

        public DictionaryLoader(params string[] filePath) {
            Trie = new Trie();
            foreach (var path in filePath) {
                using (var sr = new StreamReader(path, Encoding.ASCII))
                {
                    string tmp;
                    while ((tmp = sr.ReadLine()) != null){
                        Trie.Add(tmp.ToLower());
                    }
                }
            }
        }
    }
}