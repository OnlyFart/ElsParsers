namespace Core.Types {
    public struct ComparerResult {
        /// <summary>
        /// Разница в "процентах" между двумя строками
        /// </summary>
        public double Diff;

        /// <summary>
        /// Удовлетворяет ли разница критерию похожести
        /// </summary>
        public bool Success;
        
        public ComparerResult(double diff, bool success) {
            Diff = diff;
            Success = success;
        }
    }

    public struct BookComparerResult {
        public bool Success;
        public double Coeff;
    }
}
