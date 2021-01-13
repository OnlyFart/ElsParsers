using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NLog;

namespace Core.Extensions {
    public static class DataflowExtension {
        public static async Task WaitBlocks(params IDataflowBlock[] blocks) {
            foreach (var block in blocks) {
                block.Complete();
                await block.Completion;
            }
        }

        public static void CompleteMessage(this IDataflowBlock block, Logger logger, string message) {
            block.Completion.ContinueWith(task => logger.Info(message)).GetAwaiter();
        }
    }
}
