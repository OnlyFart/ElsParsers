using System;
using System.Collections.Generic;
using System.Linq;

namespace Book.Comparer.Logic.Extensions {
    public static class EnumerableExtensions {
        // <summary>
        /// Returns all permutations of the input <see cref="IEnumerable{T}"/>.
        /// </summary>
        /// <param name="source">The list of items to permute.</param>
        /// <returns>A collection containing all permutations of the input <see cref="IEnumerable&lt;T&gt;"/>.</returns>
        public static IEnumerable<IEnumerable<T>> AllPermutations<T>(this IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            // Ensure that the source IEnumerable is evaluated only once
            return Permutations(source.ToArray());
        }

        private static IEnumerable<IEnumerable<T>> Permutations<T>(IEnumerable<T> source) {
            var c = source.Count();
            if (c == 1)
                yield return source;
            else
                for (var i = 0; i < c; i++) {
                    foreach (var p in Permutations(source.Take(i).Concat(source.Skip(i + 1)))) {
                        yield return source.Skip(i).Take(1).Concat(p);
                    }
                }
        }

        public static bool IsNullOrEmpty<T>(this ICollection<T> collection) {
            return collection == null || collection.Count == 0;
        }
    }
}
