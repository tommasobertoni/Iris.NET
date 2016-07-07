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

        private ConcurrentDictionary<string, ChannelTreeNode<T>> _nodes = new ConcurrentDictionary<string, ChannelTreeNode<T>>();

        public IEnumerable<T> this[string channel]
        {
            get
            {
                ChannelTreeNode<T> node = null;
                _nodes.TryGetValue(channel, out node);
                return node?.Items;
            }
        }

        public bool Add(string channel, T item) => Add(channel.Split(ChannelsSeparator), item);

        public bool Add(string[] channelsHierarchy, T item)
        {
            if (channelsHierarchy == null || item == null)
                throw new ArgumentNullException();

            if (channelsHierarchy.Length < 1)
                throw new ArgumentException();

            CheckChannelsNamesValidity(channelsHierarchy);

            var rootChannel = $"{ChannelsSeparator}{channelsHierarchy[0]}";

            if (_nodes.ContainsKey(rootChannel))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(rootChannel);

                ChannelTreeNode<T> node = _nodes[rootChannel];
                var firstNewChannelIndex = 1;
                for (; firstNewChannelIndex < channelsHierarchy.Length; firstNewChannelIndex++)
                {
                    sb.Append($"{ChannelsSeparator}{channelsHierarchy[firstNewChannelIndex]}");
                    var currentChannelName = sb.ToString();

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
                _nodes.TryAdd(node.FullChannelName, node);
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
            StringBuilder sb = new StringBuilder();
            if (parent != null)
                sb.Append(parent.FullChannelName);

            var currentNode = parent;
            for (int i = fromIndex; i < channelsHierarchy.Length; i++)
            {
                sb.Append($"{ChannelsSeparator}{channelsHierarchy[i]}");
                currentNode = new ChannelTreeNode<T>(currentNode, sb.ToString());
            }

            return currentNode;
        }

        public IEnumerable<T> GetSubscriptions(string channel, bool includeFullHierarchy = false)
        {
            throw new NotImplementedException();
        }

        public void Remove(string channel, T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAll(T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveChannel(string channel)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var node in _nodes)
                sb.Append($"{node.Value}\n");

            return sb.ToString();
        }
    }

    class ChannelTreeNode<T>
    {
        public string FullChannelName { get; }

        public IrisConcurrentHashSet<T> Items { get; internal set; } = new IrisConcurrentHashSet<T>();

        public ConcurrentDictionary<string, ChannelTreeNode<T>> Childs { get; internal set; } = new ConcurrentDictionary<string, ChannelTreeNode<T>>();

        public ChannelTreeNode<T> Parent { get; internal set; }

        public ChannelTreeNode(ChannelTreeNode<T> parent, string fullChannelName)
        {
            if (fullChannelName == null)
                throw new ArgumentNullException();

            if (string.IsNullOrWhiteSpace(fullChannelName) ||
                !fullChannelName.StartsWith(IrisChannelsSubscriptionsDictionary<T>.ChannelsSeparator.ToString()))
                throw new ArgumentException();

            Parent = parent;
            FullChannelName = fullChannelName;

            if (Parent != null)
                Parent.Childs.TryAdd(FullChannelName, this);
        }

        public override bool Equals(object obj)
        {
            ChannelTreeNode<T> treeNode = null;
            if ((treeNode = obj as ChannelTreeNode<T>) == null)
                return false;

            return treeNode.FullChannelName?.Equals(FullChannelName) ?? false;
        }

        public override int GetHashCode() => FullChannelName?.GetHashCode() ?? 0;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{FullChannelName} ({Items.Count}) -> {{");

            foreach (var child in Childs)
            {
                sb.Append(child);
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
