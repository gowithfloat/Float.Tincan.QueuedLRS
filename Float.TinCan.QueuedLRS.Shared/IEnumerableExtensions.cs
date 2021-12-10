using System;
using System.Collections.Generic;

namespace Float.TinCan.QueuedLRS
{
    /// <summary>
    /// Extensions on the enumerable type.
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Iterates this sequence, performing an action on each element.
        /// </summary>
        /// <returns>This enumerable object.</returns>
        /// <param name="enumerable">This sequence.</param>
        /// <param name="action">An action to perform using elements from this sequence.</param>
        /// <typeparam name="T">The type of elements in the sequence.</typeparam>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            if (enumerable == null)
            {
                throw new ArgumentNullException(nameof(enumerable));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            using (var enumerator = enumerable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    action(enumerator.Current);
                }
            }

            return enumerable;
        }
    }
}
