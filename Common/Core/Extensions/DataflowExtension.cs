using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NLog;

namespace Core.Extensions {
    /// <summary>
    /// Расширения для Dataflow блоков
    /// </summary>
    public static class DataflowExtension {
        /// <summary>
        /// Дождаться завершения всех блоков
        /// </summary>
        /// <param name="blocks"></param>
        /// <returns></returns>
        public static async Task WaitBlocks(params IDataflowBlock[] blocks) {
            foreach (var block in blocks) {
                block.Complete();
                await block.Completion;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="block"></param>
        /// <param name="logger"></param>
        /// <param name="message"></param>
        public static void CompleteMessage(this IDataflowBlock block, Logger logger, string message) {
            block.Completion.ContinueWith(task => logger.Info(message));
        }
    }
}
