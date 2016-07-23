using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Iris.NET.Collections
{
    /// <summary>
    /// Interface for a channels-subscriptions handler class.
    /// </summary>
    /// <typeparam name="T">The subscription type.</typeparam>
    public interface IChannelsSubscriptionsDictionary<T>
    {
        /// <summary>
        /// Adds an item to a channel.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        /// <param name="channel">The name of the head channel or a hierarchy.</param>
        /// <returns>True if the operation succeeded.</returns>
        bool Add(T item, string channel);

        /// <summary>
        /// Adds an item to a channel.
        /// </summary>
        /// <param name="channelsHierarchy">The hierarchy of channels of which the last is the channel to which the item will be added.</param>
        /// <param name="item">The item to be added.</param>
        /// <returns>True if the operation succeeded.</returns>
        bool Add(T item, params string[] channelsHierarchy);

        /// <summary>
        /// Returns a list of items subscribed to the channel.
        /// </summary>
        /// <param name="channel">The name of the head channel or a hierarchy.</param>
        /// <returns>A list of items subscribed to the channel.</returns>
        List<T> this[string channel] { get; }

        /// <summary>
        /// Returns a list of items subscribed to the channel.
        /// </summary>
        /// <param name="channel">The name of the head channel or a hierarchy.</param>
        /// <param name="includeFullHierarchy">If set to true, it will include all the subscriptions to the child channels of the specified parent channel.</param>
        /// <returns>A list of items subscribed to the channel.</returns>
        List<T> GetSubscriptions(string channel, bool includeFullHierarchy = false);

        /// <summary>
        /// Returns a list of head channels, which are the ones that have no parent channel.
        /// </summary>
        /// <returns>A list of head channels.</returns>
        List<string> GetChannelsHeads();

        /// <summary>
        /// Returns a list of channels, which are children of the specified parent channel.
        /// </summary>
        /// <param name="parentChannel"></param>
        /// <returns>A list of child channels.</returns>
        List<string> GetChannelsHierarchy(string parentChannel);

        /// <summary>
        /// Removes an item from a channel.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <param name="channel">The name of the head channel or a hierarchy.</param>
        /// <returns>True if the operation succeeded.</returns>
        bool Remove(T item, string channel);

        /// <summary>
        /// Removes all subscriptions of the item from all the channels.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <returns>True if the operation succeeded.</returns>
        bool RemoveAll(T item);

        /// <summary>
        /// Removes a channel and its children.
        /// </summary>
        /// <param name="channel">The parent channel to be removed.</param>
        /// <param name="includeFullHierarchy">If set to true, it will remove all the subscriptions to the child channels of the specified parent channel.</param>
        /// <returns>True if the operation succeeded.</returns>
        bool RemoveChannel(string channel, bool includeFullHierarchy = false);

        /// <summary>
        /// Clears all the channels and subscriptions.
        /// </summary>
        void Clear();
    }
}
