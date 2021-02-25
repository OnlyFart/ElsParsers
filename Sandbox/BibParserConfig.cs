using System.Collections.Generic;

namespace Sandbox {
    public class BibParserConfig {
        public readonly ICollection<string> Authors;
        public readonly IEnumerable<string> Publishers;
        public readonly HashSet<string> Trash;
        
        public BibParserConfig(ICollection<string> authors, IEnumerable<string> publishers, HashSet<string> trash) {
            Authors = authors;
            Publishers = publishers;
            Trash = trash;
        }
    }
}
