using System;
using System.Collections.Generic;

namespace FullTrustProcess
{
    public static class Extension
    {
        public static void ForEach<T>(this IEnumerable<T> Source, Action<T> Action)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source));
            }
            if (Action == null)
            {
                throw new ArgumentNullException(nameof(Action));
            }

            foreach (T item in Source)
            {
                Action(item);
            }
        }
    }
}
