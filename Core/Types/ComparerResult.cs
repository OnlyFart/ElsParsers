namespace Core.Types {
    public class ComparerResult {
        /// <summary>
        /// Разница в "процентах" между двумя строками
        /// </summary>
        public readonly decimal Diff;

        /// <summary>
        /// Удовлетворяет ли разница критерию похожести
        /// </summary>
        public readonly bool Success;
        
        public ComparerResult(decimal diff, bool success) {
            Diff = diff;
            Success = success;
        }

        public ComparerResult() : this(0, false) {
                
        }
    }

    public class BookComparerResult {
        public ComparerResult Name;
        public ComparerResult Author;
    }
}
