using System.Collections.Generic;
using System.Linq;

namespace AxarDB.Extensions
{
    public static class LinqExtensions
    {
        /// <summary>
        /// Determines whether a sequence contains a specified element. (Similar to LINQ Contains)
        /// Provided for compatibility with JS-like syntax (e.g. x.rowNumber.includes(cats))
        /// </summary>
        public static bool includes<T>(this T item, IEnumerable<T> collection)
        {
            if (collection == null) return false;
            return collection.Contains(item);
        }

        /// <summary>
        /// Determines whether two sequences are equal by comparing their elements. (Similar to LINQ SequenceEqual)
        /// Provided for compatibility with JS-like syntax.
        /// </summary>
        public static bool includes<T>(this IEnumerable<T> source, IEnumerable<T> second)
        {
            if (source == null && second == null) return true;
            if (source == null || second == null) return false;
            return source.SequenceEqual(second);
        }
    }
}
