# Iris.NET
*Current release: [v0.1 (beta 1)](https://github.com/tommasobertoni/Iris.NET/releases/latest)*
<br><br>
Iris.NET is a TCP-based, Pub/Sub C# library. It was developed to allow an easy-to-use, channel-separated communication on a LAN infrastructure.
<br><br>
The library provides a server and two client types: one for network communication and one for local communication.
<br>
The client types are also called *nodes* and they implement the [**IIrisNode**](/Iris.NET/Iris.NET.Common/Nodes/IIrisNode.cs) interface, which defines the pub/sub methods (basically: *Subscribe*, *Unsubscribe* and *Send*).
<br><br>
The **IrisClientNode** type is responsible for the network communication and talks to the **IrisServer** through a socket connection. It can be found in the *Iris.NET.Client* namespace.
<br><br>
The **IrisServerLocalNode** type, instead, is used to communicate locally (without sockets) with the same pub/sub infrastructure.
<br>
The **IrisServer** type handles the connections coming from the client nodes. This and IrisServerLocalNode can be found in the *Iris.NET.Server* namespace.
<br><br>
## *How To*
- Create and start a server:<br>
```
IrisServer server = new IrisServer();
server.Start(22000); // Start on port 22000
```
- Create and connect a local node:<br>
```
// If you have a server and you want to use the same subscription network
IrisServerConfig config = server.GetServerConfig();
```
```
// If you just want to communicate locally or on a different local network
IrisServerConfig config = new IrisServerConfig(new IrisPubSubRouter());
```
```
IrisServerLocalNode node = new IrisServerLocalNode();
node.Connect(config);
```
- Create and connect a network node:<br>
```
IrisClientConfig config = new IrisClientConfig() { Hostname = "127.0.0.1", Port = 22000 };
IrisClientNode node = new IrisClientNode();
node.Connect(config);
```
- Subscribe to a channel
```
IDisposableSubscription subscription = node.Subscribe("worldnews", MyContentHandler);
```
- Send a message
```
node.Send("worldnews", "something good happened");
```
- Unsubscribe from a channel<br>
```
// If you have a reference to the IDisposableSubscription returned by the Subscribe method
// you can simply write
subscription.Dispose();
```
```
// Otherwise you can use the Unsubscribe method
node.Unsubscribe("worldnews", MyContentHandler);
```
<br>
For more examples and use cases head over to the *[Iris.NET.Demo project](Iris.NET.Demo/Iris.NET.Demo)*.
<br><br><br>
## Technology info
This project was developed using *Visual Studio 2015* and *C# 6*.<br>
**The library targets the .NET Framework 4.0.**
<br><br><br>
## License
**Iris.NET** is distributed under the [MIT License](https://github.com/tommasobertoni/Iris.NET/blob/master/LICENCE).
