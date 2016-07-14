using Iris.NET.Collections;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace Iris.NET.Collections
{
    /// <summary>
    /// Implementation of IChannelsSubscriptionsDictionary.
    /// </summary>
    /// <typeparam name="T">The subscription type.</typeparam>
    public class IrisChannelsSubscriptionsDictionary<T> : IChannelsSubscriptionsDictionary<T>
    {
        public const char ChannelsSeparator = '/';

        private ChannelTreeNode<T> _root = new ChannelTreeNode<T>(null, null);

        #region Public
        /// <summary>
        /// Returns a list of items subscribed to the channel.
        /// </summary>
        /// <param name="channel">The name of the head channel or a hierarchy.</param>
        /// <returns>A list of items subscribed to the channel.</returns>
        public List<T> this[string channel]
        {
            get
            {
                return FindNode(channel)?.Items.ToList();
            }
        }

        /// <summary>
        /// Adds an item to a channel.
        /// </summary>
        /// <param name="item">The item to be added.</param>
        /// <param name="channel">The name of the head channel or a hierarchy.</param>
        /// <returns>Operation succeeded.</returns>
        public bool Add(T item, string channel) => Add(item, channel.Split(ChannelsSeparator));

        /// <summary>
        /// Adds an item to a channel.
        /// </summary>
        /// <param name="channelsHierarchy">The hierarchy of channels of which the last is the channel to which the item will be added.</param>
        /// <param name="item">The item to be added.</param>
        /// <returns>Operation succeeded.</returns>
        public bool Add(T item, params string[] channelsHierarchy)
        {
            if (item == null)
                throw new ArgumentNullException();

            CheckChannelsNamesValidity(channelsHierarchy);

            var rootChannel = channelsHierarchy[0];

            if (_root.Childs.ContainsKey(rootChannel))
            {
                ChannelTreeNode<T> node = _root.Childs[rootChannel];
                var firstNewChannelIndex = 1;
                for (; firstNewChannelIndex < channelsHierarchy.Length; firstNewChannelIndex++)
                {
                    var currentChannelName = channelsHierarchy[firstNewChannelIndex];
                    if (!node.Childs.ContainsKey(currentChannelName))
                        break;

                    node = node.Childs[currentChannelName];
                }

                if (firstNewChannelIndex < channelsHierarchy.Length)
                    node = CreateNewHierarchy(node, channelsHierarchy, firstNewChannelIndex);   

                if (node != null)
                {
                    node.Items.Add(item);
                    return true;
                }
            }
            else // New hierarchy
            {
                ChannelTreeNode<T> node = new ChannelTreeNode<T>(_root, rootChannel);
                _root.Childs.TryAdd(node.Name, node);
                node = CreateNewHierarchy(node, channelsHierarchy, 1);
                if (node != null)
                {
                    node.Items.Add(item);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a list of items subscribed to the channel.
        /// </summary>
        /// <param name="channel">The name of the head channel or a hierarchy.</param>
        /// <param name="includeFullHierarchy">If set to true, it will include all the subscriptions to the child channels of the specified parent channel.</param>
        /// <returns>A list of items subscribed to the channel.</returns>
        public List<T> GetSubscriptions(string channel, bool includeFullHierarchy = false)
        {
            List<T> subscriptions = null;

            var node = FindNode(channel);
            if (node != null)
            {
                subscriptions = new List<T>();
                if (includeFullHierarchy)
                {
                    GetFullSubscriptions(node, subscriptions);
                }
                else
                {
                    subscriptions.AddRange(node.Items.ToList());
                }
            }

            return subscriptions;
        }

        /// <summary>
        /// Returns a list of head channels, which are the ones that have no parent channel.
        /// </summary>
        /// <returns>A list of head channels.</returns>
        public List<string> GetChannelsHeads() => _root.Childs.Keys.ToList();

        /// <summary>
        /// Returns a list of channels, which are children of the specified parent channel.
        /// </summary>
        /// <param name="parentChannel"></param>
        /// <returns>A list of child channels.</returns>
        public List<string> GetChannelsHierarchy(string parentChannel)
        {
            var node = FindNode(parentChannel);
            if (node != null)
                return node.Childs.Keys.ToList();
            return null;
        }

        /// <summary>
        /// Removes an item from a channel.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <param name="channel">The name of the head channel or a hierarchy.</param>
        public bool Remove(T item, string channel)
        {
            var success = false;
            var node = FindNode(channel);
            if (node != null)
                success = node.Items.Remove(item);
            return success;
        }

        /// <summary>
        /// Removes all subscriptions of the item from all the channels.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        public void RemoveAll(T item)
        {
            foreach (var node in _root.Childs)
                RemoveAll(node.Value, item);
        }

        /// <summary>
        /// Removes a channel and its children.
        /// </summary>
        /// <param name="channel">The parent channel to be removed.</param>
        public bool RemoveChannel(string fullChannelName)
        {
            var success = false;
            ChannelTreeNode<T> outer;
            var node = FindNode(fullChannelName);
            if (node != null)
            {
                var channelName = fullChannelName.Split(ChannelsSeparator).Last();
                success = node.Parent?.Childs.TryRemove(channelName, out outer) ?? false;
            }
            return success;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var node in _root.Childs)
                sb.Append($"\n{node.Value}");

            var toString = sb.ToString();
            if (toString != null && toString.Length > 0)
                toString = toString.Substring(1);
            return toString;
        }

        public void Clear() => _root = new ChannelTreeNode<T>(null, null);
        #endregion

        private void RemoveAll(ChannelTreeNode<T> node, T itemToBeRemoved)
        {
            node.Items.Remove(itemToBeRemoved);
            foreach (var subnode in node.Childs)
                RemoveAll(subnode.Value, itemToBeRemoved);
        }

        private void GetFullSubscriptions(ChannelTreeNode<T> node, List<T> subscriptions)
        {
            subscriptions.AddRange(node.Items.ToList());
            foreach (var child in node.Childs)
                GetFullSubscriptions(child.Value, subscriptions);
        }

        private void CheckChannelsNamesValidity(string[] channelsHierarchy)
        {
            if (channelsHierarchy == null || channelsHierarchy.Length < 1)
                throw new ArgumentException();

            for (int i = 0; i < channelsHierarchy.Length; i++)
            {
                var name = channelsHierarchy[i];
                if (string.IsNullOrWhiteSpace(name) || name.Contains(ChannelsSeparator))
                    throw new ArgumentException();
                else
                    channelsHierarchy[i] = name.ToLower();
            }
        }

        private ChannelTreeNode<T> CreateNewHierarchy(ChannelTreeNode<T> parent, string[] channelsHierarchy, int fromIndex = 0)
        {
            var currentNode = parent;
            for (int i = fromIndex; i < channelsHierarchy.Length; i++)
                currentNode = new ChannelTreeNode<T>(currentNode, channelsHierarchy[i]);

            return currentNode;
        }

        private ChannelTreeNode<T> FindNode(string channel)
        {
            var channelsHierarchy = channel.Split(ChannelsSeparator);
            CheckChannelsNamesValidity(channelsHierarchy);

            ChannelTreeNode<T> node = _root;

            int i;
            for (i = 0; i < channelsHierarchy.Length; i++)
            {
                var subChannel = channelsHierarchy[i];
                if (!node.Childs.ContainsKey(subChannel))
                    break;

                node = node.Childs[subChannel];
            }

            return i == channelsHierarchy.Length ? node : null;
        }
    }

    /// <summary>
    /// Class used to identify a node in the channels-subscriptions data structure.
    /// </summary>
    /// <typeparam name="T">The subscription type.</typeparam>
    class ChannelTreeNode<T>
    {
        /// <summary>
        /// The name of the channel this node represents.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The name of the channel this node represents. Uses IrisChannelsSubscriptionsDictionary.ChannelsSeparator to build the string.
        /// </summary>
        public string FullName => $"{Parent?.FullName}{IrisChannelsSubscriptionsDictionary<T>.ChannelsSeparator}{Name}";

        /// <summary>
        /// Set of items that are subscribed to this channel.
        /// </summary>
        public IrisConcurrentHashSet<T> Items { get; } = new IrisConcurrentHashSet<T>();

        /// <summary>
        /// Dictionary of child nodes.
        /// </summary>
        public ConcurrentDictionary<string, ChannelTreeNode<T>> Childs { get; } = new ConcurrentDictionary<string, ChannelTreeNode<T>>();

        /// <summary>
        /// The parent node.
        /// </summary>
        public ChannelTreeNode<T> Parent { get; internal set; }

        /// <summary>
        /// Constructor. It adds itself as a child to the parent node.
        /// </summary>
        /// <param name="parent">The parent node.</param>
        /// <param name="channelName">The channel this node represents.</param>
        public ChannelTreeNode(ChannelTreeNode<T> parent, string channelName)
        {
            Parent = parent;
            Name = channelName;

            if (Parent != null)
                Parent.Childs.TryAdd(Name, this);
        }

        /// <summary>
        /// Defines equality to another node if their fullname matches.
        /// </summary>
        /// <param name="obj">Another object.</param>
        /// <returns>True if the comparison object is a node and has the same fullname.</returns>
        public override bool Equals(object obj)
        {
            ChannelTreeNode<T> treeNode = null;
            if ((treeNode = obj as ChannelTreeNode<T>) == null)
                return false;

            return treeNode.FullName?.Equals(FullName) ?? false;
        }

        /// <summary>
        /// Defines the hash code as the fullname's hash code.
        /// </summary>
        /// <returns>The hash code of this node.</returns>
        public override int GetHashCode() => FullName.GetHashCode();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{Name} ({Items.Count}) -> {{");

            foreach (var child in Childs)
            {
                sb.Append(child.Value);
            }

            sb.Append("}");
            return sb.ToString();
        }
    }
}
