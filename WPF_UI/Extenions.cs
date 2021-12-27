using System;
using System.Collections.Generic;
using System.Linq;

namespace WPF_UI
{
    static class Extenions
    {
        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? elements)
            => elements ?? Enumerable.Empty<T>();
    }
}
