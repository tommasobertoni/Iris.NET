using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Collections
{
    public interface IChannelsSubscriptionsDictionary<T>
    {
        bool Add(string channel, T item);

        bool Add(T item, params string[] channelsHierarchy);

        IEnumerable<T> this[string channel] { get; }

        IEnumerable<T> GetSubscriptions(string channel, bool includeFullHierarchy = false);

        void Remove(string channel, T item);

        void RemoveAll(T item);

        void RemoveChannel(string channel);
    }
}
