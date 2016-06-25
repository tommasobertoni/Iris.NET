using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET
{
    public class IrisConcurrentHashSet<T>
    {
        private ConcurrentDictionary<T, byte> _items = new ConcurrentDictionary<T, byte>();

        public int Count => _items.Count;

        public bool Add(T item) => _items.TryAdd(item, 0);

        public bool Remove(T item)
        {
            byte temp;
            return _items.TryRemove(item, out temp);
        }

        public bool Contains(T item) => _items.ContainsKey(item);

        public Enumerator GetEnumerator() => new Enumerator(_items);
        
        public class Enumerator : IEnumerator<T>, IDisposable, IEnumerator
        {
            private ConcurrentDictionary<T, byte> _dictionary;
            private IEnumerator<KeyValuePair<T, byte>> _dictionaryEnumerator;

            public T Current => _dictionaryEnumerator.Current.Key;

            object IEnumerator.Current => Current;

            internal Enumerator(ConcurrentDictionary<T, byte> dictionary)
            {
                _dictionary = dictionary;
                _dictionaryEnumerator = _dictionary.GetEnumerator();
            }

            public void Dispose()
            {
                _dictionary = null;
                _dictionaryEnumerator = null;
            }

            public bool MoveNext() => _dictionaryEnumerator?.MoveNext() ?? false;

            public void Reset() => _dictionaryEnumerator = _dictionary.GetEnumerator();
        }
    }
}
