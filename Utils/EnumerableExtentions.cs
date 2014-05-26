using System.Collections.Generic;

namespace Utils {
    public static class EnumerableExtentions {
        public static IEnumerable<T> ToEnumerable<T>(this T t) {
            yield return t;
        }
    }
}