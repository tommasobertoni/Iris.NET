using Iris.NET.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Iris.NET.Demo
{
    class LocalTestClass
    {
        private IrisServerConfig _config = new IrisServerConfig(new IrisPubSubRouter());
        private IrisServerLocalNode localNode1 = new IrisServerLocalNode();

        private IrisServerLocalNode localNode2 = new IrisServerLocalNode();
        private IrisServerLocalNode localNode3 = new IrisServerLocalNode();
        private List<IDisposableSubscription> _disposableSubscriptions = new List<IDisposableSubscription>();

        private string _channel = "chatroom";
        private string _nestedChannelName = "programming";

        private bool _IDontBelieveIt = true;

        private Regex _amazingRegex = new Regex(@"(?=\p{Lu}\p{Ll})|(?<=\p{Ll})(?=\p{Lu})");
        // source http://stackoverflow.com/questions/21326963/splitting-camelcase-with-regex#answer-21327150

        public void RunFullTest()
        {
            ExecuteAndPrettifyVoid(nameof(Setup), Setup);

            ExecuteAndPrettify(nameof(UseCase_BroadcastCommunication), UseCase_BroadcastCommunication);

            ExecuteAndPrettify(nameof(UseCase_ChatRoom), UseCase_ChatRoom);

            ExecuteAndPrettify(nameof(UseCase_NestedChannelsSubscriptions), UseCase_NestedChannelsSubscriptions);

            ExecuteAndPrettifyVoid(nameof(UseCase_FullHierarchyCommunication), UseCase_FullHierarchyCommunication);

            ExecuteAndPrettify(nameof(UseCase_ComplexCommunication), UseCase_ComplexCommunication);

            ExecuteAndPrettifyVoid(nameof(Unsubscribe), Unsubscribe);
        }

        public void Setup()
        {
            // Connect all nodes using the IrisServerConfig
            bool result1 = localNode1.Connect(_config);
            bool result2 = localNode2.Connect(_config);
            bool result3 = localNode3.Connect(_config);

            CheckThatEverythingIsOk(result1, result2, result3);
            Console.WriteLine($"All the nodes are connected.");
        }

        public List<IDisposableSubscription> UseCase_BroadcastCommunication()
        {
            List<IDisposableSubscription> disposableSubscriptions = new List<IDisposableSubscription>();

            // Subscribe the nodes to the broadcast communication
            // Every subscription requires a delegate with the following parameters: object content, IrisContextHook hook.
            // The content is the data that is sent by the nodes, the IrisContextHook is an internal managed object that gives context
            // information to the handler about the content and the subscription state.
            // GenericContentHandler is a local method that simply prints to the console the content in a pretty and standard way.
            var subscription1 = localNode1.SubscribeToBroadcast((c, h) => GenericContentHandler(nameof(localNode1), "broadcast", c, h));
            bool result1 = subscription1 != null;
            var subscription2 = localNode2.SubscribeToBroadcast((c, h) => GenericContentHandler(nameof(localNode2), "broadcast", c, h));
            bool result2 = subscription2 != null;

            CheckThatEverythingIsOk(result1, result2);
            disposableSubscriptions.Add(subscription1);
            disposableSubscriptions.Add(subscription2);
            Console.WriteLine($"The nodes {nameof(localNode1)}, {nameof(localNode2)} are subscribed to the broadcast.");

            string message = "Hello!";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" in broadcast.");
            // The message will be received by all the nodes subscribed to the broadcast, except the sender.
            result1 = localNode1.SendToBroadcast(message);

            CheckThatEverythingIsOk(result1);
            return disposableSubscriptions;
        }

        public List<IDisposableSubscription> UseCase_ChatRoom()
        {
            List<IDisposableSubscription> disposableSubscriptions = new List<IDisposableSubscription>();

            // In this test, subscription1 is actually useless because localNode1 is sending the messages, so it won't receive any content.
            // Note that a subscription to a channel is not needed to send a content to that channel, and the same goes for the broadcast.
            var subscription1 = localNode1.Subscribe(_channel, (c, h) => GenericContentHandler(nameof(localNode1), _channel, c, h));
            bool result1 = subscription1 != null;

            var subscription2 = localNode2.Subscribe(_channel, (c, h) => GenericContentHandler(nameof(localNode2), _channel, c, h));
            bool result2 = subscription2 != null;

            var subscription3 = localNode3.Subscribe(_channel, (c, h) => GenericContentHandler(nameof(localNode3), _channel, c, h));
            bool result3 = subscription3 != null;

            CheckThatEverythingIsOk(result1, result2, result3);
            Console.WriteLine($"The nodes {nameof(localNode1)}, {nameof(localNode2)}, {nameof(localNode3)} are subscribed to the \"{_channel}\" channel.");
            disposableSubscriptions.Add(subscription1);
            disposableSubscriptions.Add(subscription2);
            disposableSubscriptions.Add(subscription3);

            string message = $"Hi! my name is {nameof(localNode1)}";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" to the \"{_channel}\" channel.");
            result1 = localNode1.Send(_channel, message);
            CheckThatEverythingIsOk(result1);

            WaitALittle();
            Console.WriteLine();

            message = $"{message} [in broadcast]";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" in broadcast.");
            Console.WriteLine($"// Notice that {nameof(localNode3)} won't receive the message because it isn't subscribed to the broadcast communication.");
            result1 = localNode1.SendToBroadcast(message);
            CheckThatEverythingIsOk(result1);

            return disposableSubscriptions;
        }

        public List<IDisposableSubscription> UseCase_NestedChannelsSubscriptions()
        {
            List<IDisposableSubscription> disposableSubscriptions = new List<IDisposableSubscription>();

            // The library interprets the nested channels using the path convention and the slash '/' as the delimiting character.
            // So the "nestedChannelHierarchy" describes that the "_nestedChannelName" is a nested channel (or a child channel) of "_channel".
            var nestedChannelHierarchy = $"{_channel}/{_nestedChannelName}";

            string message = $"Hey, I'm {nameof(localNode1)}. Those who want to talk about programming head over to the {nestedChannelHierarchy} channel!";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" to the \"{_channel}\" channel.");
            bool result1 = localNode1.Send(_channel, message);
            CheckThatEverythingIsOk(result1);

            WaitALittle();
            Console.WriteLine();

            var subscription1 = localNode1.Subscribe($"{nestedChannelHierarchy}", (c, h) => GenericContentHandler(nameof(localNode1), nestedChannelHierarchy, c, h));
            result1 = subscription1 != null;

            var subscription3 = localNode3.Subscribe($"{nestedChannelHierarchy}", (c, h) => GenericContentHandler(nameof(localNode3), nestedChannelHierarchy, c, h));
            bool result3 = subscription3 != null;

            CheckThatEverythingIsOk(result1, result3);
            Console.WriteLine($"The nodes {nameof(localNode1)}, {nameof(localNode3)} are subscribed to the \"{nestedChannelHierarchy}\" channel.");
            disposableSubscriptions.Add(subscription1);
            disposableSubscriptions.Add(subscription3);

            message = "Let's talk about programming!";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" to the \"{nestedChannelHierarchy}\" channel.");
            result1 = localNode1.Send(nestedChannelHierarchy, message);
            CheckThatEverythingIsOk(result1);

            return disposableSubscriptions;
        }

        public void UseCase_FullHierarchyCommunication()
        {
            string message = $"System announcement: this channel and all its subchannels will be closed soon.";
            Console.WriteLine($"{nameof(localNode1)} sends \"{message}\" to the \"{_channel}\" channel and all its subchannels.");
            // By setting the "propagateThroughHierarchy" parameter to "true" the content will be sent to every content handler in the target channel
            // and to every content handler in all the child channels, recursively.
            bool result1 = localNode1.Send(_channel, message, propagateThroughHierarchy: true);
            CheckThatEverythingIsOk(result1);
        }

        public List<IDisposableSubscription> UseCase_ComplexCommunication()
        {
            List<IDisposableSubscription> disposableSubscriptions = new List<IDisposableSubscription>();

            var subscription1 = localNode1.Subscribe($"{localNode1.Id.ToString()}", (c, h) => GenericContentHandler(nameof(localNode1), localNode1.Id.ToString(), c, h));
            bool result1 = subscription1 != null;

            var subscription2 = localNode2.Subscribe($"{localNode2.Id.ToString()}", (c, h) => GenericContentHandler(nameof(localNode2), localNode2.Id.ToString(), c, h));
            bool result2 = subscription2 != null;

            var subscription3 = localNode3.Subscribe($"{localNode3.Id.ToString()}", (c, h) => GenericContentHandler(nameof(localNode3), localNode3.Id.ToString(), c, h));
            bool result3 = subscription3 != null;

            CheckThatEverythingIsOk(result1, result2, result3);
            Console.WriteLine($"The nodes {nameof(localNode1)}, {nameof(localNode2)}, {nameof(localNode3)} are subscribed to their \"personal\" channel (based on their Id).");
            disposableSubscriptions.Add(subscription1);
            disposableSubscriptions.Add(subscription2);
            disposableSubscriptions.Add(subscription3);

            // Until now the test only sent strings, but the content expected by the Send method is actually of type object.

            double d = 0.42;
            Console.WriteLine($"{nameof(localNode1)} sends \"{d}\" to the {nameof(localNode2)} personal channel.");
            result1 = localNode1.Send(localNode2.Id.ToString(), d);
            CheckThatEverythingIsOk(result1);

            WaitALittle();
            Console.WriteLine();

            var obj = new TestType($"\"A present for {nameof(localNode3)}\"") { Count = 42 };
            Console.WriteLine($"{nameof(localNode2)} sends \"{obj}\" to the {nameof(localNode3)} personal channel.");
            result2 = localNode2.Send(localNode3.Id.ToString(), obj);
            CheckThatEverythingIsOk(result2);

            return disposableSubscriptions;
        }

        public void Unsubscribe()
        {
            // To remove a subscription you can use the Unsubscribe method, or dispose the IDisposableSubscription returned by the Subscribe method.

            _disposableSubscriptions.ForEach(ds => ds.Dispose());
            Console.WriteLine("All the subscriptions have been disposed.");
            if (_IDontBelieveIt)
            {
                Console.WriteLine("You don't believe it? Let's send a couple of messages...");
                var message = "Super-important-communication-that-cannot-be-ignored";
                localNode1.SendToBroadcast(message);
                localNode1.Send(_channel, message);
                localNode1.Send($"{_channel}/{_nestedChannelName}", message);
                WaitALittle();
                Console.WriteLine("Nobody should have received any message.");
            }
        }

        #region Helper methods
        public void ExecuteAndPrettifyVoid(string targetFunction, Action action)
        {
            ExecuteAndPrettify(targetFunction, () =>
            {
                action();
                return null;
            });
        }

        public void ExecuteAndPrettify(string targetFunction, Func<List<IDisposableSubscription>> function)
        {
            Console.WriteLine("--------------------------------------------------");

            var title = targetFunction.Replace("_", ":");
            var words = _amazingRegex.Split(title);
            var finalTitle = string.Join(" ", words);
            Console.WriteLine($"-{finalTitle}\n");

            var result = function();
            if (result != null)
                _disposableSubscriptions.AddRange(result);

            WaitALittle();

            Console.WriteLine("\n");
        }

        private void GenericContentHandler(string receiverName, string subscribedChannel, object content, IrisContextHook hook)
        {
            Console.WriteLine($"{receiverName} received \"{content.ToString()}\" from \"{subscribedChannel}\"");
        }

        private void CheckThatEverythingIsOk(params bool[] results)
        {
            if (results.Any(r => !r))
            {
                Console.Write("Quit");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }

        private void WaitALittle()
        {
            Thread.Sleep(300); // Gives some time to the asyncronous operations to complete
        }
        #endregion
    }

    class TestType
    {
        public int Count { get; set; }

        private string _name;

        public TestType(string name)
        {
            _name = name;
        }

        public override string ToString() => $"I am a {nameof(TestType)} instance and my name is {_name}. Also I count {Count}.";
    }
}
