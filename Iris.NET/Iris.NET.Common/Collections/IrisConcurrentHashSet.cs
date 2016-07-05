using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Collections
{
    /// <summary>
    /// Basic implementation of a generic concurrent hash set.
    /// It uses a System.Collections.Concurrent.ConcurrentDictionary<T, byte> to store the unique values as keys.
    /// </summary>
    /// <typeparam name="T">The generic type.</typeparam>
    public class IrisConcurrentHashSet<T> : IEnumerable<T>
    {
        private ConcurrentDictionary<T, byte> _items = new ConcurrentDictionary<T, byte>();

        /// <summary>
        /// The items count.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Adds an item to the hash set.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        /// <returns>Operation succeeded.</returns>
        public bool Add(T item) => _items.TryAdd(item, 0);

        /// <summary>
        /// Removes the item from the hash set.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <returns>Operation succeeded.</returns>
        public bool Remove(T item)
        {
            byte temp;
            return _items.TryRemove(item, out temp);
        }

        /// <summary>
        /// Checks if the hash set contains the item.
        /// </summary>
        /// <param name="item">The target item.</param>
        /// <returns>Item is contained in the hash set.</returns>
        public bool Contains(T item) => _items.ContainsKey(item);

        /// <summary>
        /// Clear all the items.
        /// </summary>
        public void Clear() => _items.Clear();

        /// <summary>
        /// Returns the generic enumerator.
        /// </summary>
        /// <returns>The generic enumerator.</returns>
        public IEnumerator<T> GetEnumerator() => new Enumerator<T>(this);

        /// <summary>
        /// Returns the enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Enumerator of IrisConcurrentHashSet
        /// </summary>
        /// <typeparam name="U">The type of the IrisConcurrentHashSet.</typeparam>
        public class Enumerator<U> : IEnumerator<U>, IDisposable, IEnumerator
        {
            private IrisConcurrentHashSet<U> _concurrentHashSet;
            private IEnumerator<KeyValuePair<U, byte>> _dictionaryEnumerator;

            /// <summary>
            /// Returns the current item.
            /// </summary>
            public U Current => _dictionaryEnumerator.Current.Key;

            /// <summary>
            /// Returns the current item as object.
            /// </summary>
            object IEnumerator.Current => Current;

            internal Enumerator(IrisConcurrentHashSet<U> concurrentHashSet)
            {
                _concurrentHashSet = concurrentHashSet;
                _dictionaryEnumerator = _concurrentHashSet._items.GetEnumerator();
            }

            /// <summary>
            /// Disposes the enumerator.
            /// </summary>
            public void Dispose()
            {
                _concurrentHashSet = null;
                _dictionaryEnumerator = null;
            }

            /// <summary>
            /// Moves to the next item.
            /// </summary>
            /// <returns>If the next item is not null.</returns>
            public bool MoveNext() => _dictionaryEnumerator?.MoveNext() ?? false;

            /// <summary>
            /// Resets the head of the enumerator.
            /// </summary>
            public void Reset() => _dictionaryEnumerator = _concurrentHashSet._items.GetEnumerator();
        }
    }
}
