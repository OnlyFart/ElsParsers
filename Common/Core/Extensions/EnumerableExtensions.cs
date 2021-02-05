using System.Collections.Generic;
using System.Linq;

namespace Core.Extensions {
    public static class EnumerableExtensions {
        public static IEnumerable<IEnumerable<T>> AllPermutations<T>(this IEnumerable<T> source) {
            return Permutations(source.ToArray());
        }

        private static IEnumerable<IEnumerable<T>> Permutations<T>(IEnumerable<T> source) {
            var c = source.Count();
            if (c == 1) {
                yield return source;
            } else {
                for (var i = 0; i < c; i++) {
                    foreach (var p in Permutations(source.Take(i).Concat(source.Skip(i + 1)))) {
                        yield return source.Skip(i).Take(1).Concat(p);
                    }
                }
            }
        }

        public static string StrJoin<T>(this IEnumerable<T> collection, string separator) {
            return collection == null ? string.Empty : string.Join(separator, collection);
        }

        public static bool IsNullOrEmpty<T>(this ICollection<T> collection) {
            return collection == null || collection.Count == 0;
        }
    }
}
