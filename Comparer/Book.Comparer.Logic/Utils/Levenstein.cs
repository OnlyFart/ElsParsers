namespace Book.Comparer.Logic.Utils {
    public class Levenstein {
        private static int Minimum(int a, int b, int c) => (a = a < b ? a : b) < c ? a : c;
        
        public static double Distance(string firstWord, string secondWord) {
            var n = firstWord.Length + 1;
            var m = secondWord.Length + 1;
            var matrixD = new int[n, m];

            const int DELETION_COST = 1;
            const int INSERTION_COST = 1;

            for (var i = 0; i < n; i++) {
                matrixD[i, 0] = i;
            }

            for (var j = 0; j < m; j++) {
                matrixD[0, j] = j;
            }

            for (var i = 1; i < n; i++) {
                for (var j = 1; j < m; j++) {
                    var substitutionCost = firstWord[i - 1] == secondWord[j - 1] ? 0 : 1;

                    matrixD[i, j] = Minimum(matrixD[i - 1, j] + DELETION_COST, matrixD[i, j - 1] + INSERTION_COST, matrixD[i - 1, j - 1] + substitutionCost);
                }
            }

            return matrixD[n - 1, m - 1];
        }
    }
}
