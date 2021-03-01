using System.Collections.Generic;

namespace Book.Comparer.Logic.Configs {
    public record BibParserConfig(ICollection<string> Authors, HashSet<string> Publishers);
}
