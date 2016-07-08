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
    public class IrisChannelsSubscriptionsDictionary<T> : IChannelsSubscriptionsDictionary<T>
    {
        public const char ChannelsSeparator = '/';

        private ChannelTreeNode<T> _root = new ChannelTreeNode<T>(null, null);
        
        public IEnumerable<T> this[string channel]
        {
            get
            {
                return FindNode(channel)?.Items;
            }
        }

        public bool Add(string channel, T item) => Add(item, channel.Split(ChannelsSeparator));

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
                ChannelTreeNode<T> node = new ChannelTreeNode<T>(null, rootChannel);
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

        public IEnumerable<T> GetSubscriptions(string channel, bool includeFullHierarchy = false)
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

        private void GetFullSubscriptions(ChannelTreeNode<T> node, List<T> subscriptions)
        {
            subscriptions.AddRange(node.Items.ToList());
            foreach (var child in node.Childs)
                GetFullSubscriptions(child.Value, subscriptions);
        }

        public void Remove(string channel, T item)
        {
            var node = FindNode(channel);
            if (node != null)
                node.Items.Remove(item);
        }

        public void RemoveAll(T item)
        {
            foreach (var node in _root.Childs)
                RemoveAll(node.Value, item);
        }

        private void RemoveAll(ChannelTreeNode<T> node, T itemToBeRemoved)
        {
            node.Items.Remove(itemToBeRemoved);
            foreach (var subnode in node.Childs)
                RemoveAll(subnode.Value, itemToBeRemoved);
        }

        public void RemoveChannel(string fullChannelName)
        {
            ChannelTreeNode<T> outer;
            var node = FindNode(fullChannelName);
            if (node != null)
            {
                var channelName = fullChannelName.Split(ChannelsSeparator).Last();
                node.Parent?.Childs.TryRemove(channelName, out outer);
            }
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
    }

    class ChannelTreeNode<T>
    {
        public string Name { get; }

        public string FullName => $"{Parent?.FullName}{IrisChannelsSubscriptionsDictionary<T>.ChannelsSeparator}{Name}";

        public IrisConcurrentHashSet<T> Items { get; internal set; } = new IrisConcurrentHashSet<T>();

        public ConcurrentDictionary<string, ChannelTreeNode<T>> Childs { get; internal set; } = new ConcurrentDictionary<string, ChannelTreeNode<T>>();

        public ChannelTreeNode<T> Parent { get; internal set; }

        public ChannelTreeNode(ChannelTreeNode<T> parent, string channelName)
        {
            Parent = parent;
            Name = channelName;

            if (Parent != null)
                Parent.Childs.TryAdd(Name, this);
        }

        public override bool Equals(object obj)
        {
            ChannelTreeNode<T> treeNode = null;
            if ((treeNode = obj as ChannelTreeNode<T>) == null)
                return false;

            return treeNode.FullName?.Equals(FullName) ?? false;
        }

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

    //class ChannelTreeNodeCollection<T> : NameObjectCollectionBase
    //{
    //    public ChannelTreeNodeCollection()
    //           : base(new ChannelTreeNodeEqualityComparer<T>())
    //    {
    //    }

    //    public ChannelTreeNode<T> this[int index]
    //    {
    //        get
    //        {
    //            return (ChannelTreeNode<T>)BaseGet(index);
    //        }

    //        set
    //        {
    //            BaseSet(index, value);
    //        }
    //    }

    //    public ChannelTreeNode<T> this[string channel]
    //    {
    //        get
    //        {
    //            return (ChannelTreeNode<T>)BaseGet(channel);
    //        }

    //        set
    //        {
    //            BaseSet(value.Name, value);
    //        }
    //    }

    //    public void Add(ChannelTreeNode<T> node)
    //    {
    //        if (node == null)
    //            throw new ArgumentNullException();

    //        if (Contains(node.Name))
    //            throw new ArgumentException();

    //        BaseAdd(node.Name, node);
    //    }

    //    public bool Contains(string channel) => BaseGet(channel) != null;

    //    public void Remove(ChannelTreeNode<T> node) => Remove(node.Name);

    //    public void Remove(string channel) => BaseRemove(channel);

    //    public void RemoveAt(int index) => BaseRemoveAt(index);

    //    public void Clear() => BaseClear();
    //}

    //class ChannelTreeNodeEqualityComparer<T> : IEqualityComparer
    //{
    //    bool IEqualityComparer.Equals(object x, object y) => (x as ChannelTreeNode<T>)?.Name == (y as ChannelTreeNode<T>)?.Name;

    //    int IEqualityComparer.GetHashCode(object obj) => (obj as ChannelTreeNode<T>)?.Name.GetHashCode() ?? 0;
    //}
}
