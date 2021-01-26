using System.Collections.Generic;

namespace Book.Comparer.Logic.Configs {
    public class NormalizerConfig {
        public Regexes Regexes { get; set; }
        public Lists Lists { get; set; }
    }

    public class Regexes {
        public string NonSignWords { get; set; }
        public string Vowels { get; set; }
    }

    public class Lists {
        public List<string> NonSingAuthorWords { get; set; }
    }
}
