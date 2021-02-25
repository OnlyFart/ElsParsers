using Book.Comparer.Configs;

namespace Sandbox {
    public class ComparerConfig : IComparerConfig {
        public int MaxThread { get; set; }

        public ComparerConfig(int maxThread) {
            MaxThread = maxThread;
        }
    }
}
