using System.Collections.Generic;

namespace Sandbox {
    public record BibParserConfig(ICollection<string> Authors, HashSet<string> Publishers, HashSet<string> Trash);
}
