using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Core.Extensions {
    public static class DataflowExtension {
        public static async Task WaitBlocks(params IDataflowBlock[] blocks) {
            foreach (var block in blocks) {
                block.Complete();
                await block.Completion;
            }
        }
    }
}
