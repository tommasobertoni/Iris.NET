# Iris.NET
[![Iris.NET on SourceBrowser](https://img.shields.io/badge/source-browser-2B9CC6.svg?style=flat-square)](http://sourcebrowser.io/Browse/tommasobertoni/Iris.NET)
[![Iris.NET on NuGet](https://img.shields.io/badge/nuget-v0.2.0-blue.svg?style=flat-square)](https://www.nuget.org/packages/Iris.NET/)
<br><br>
Iris.NET is a TCP-based, Pub/Sub C# library. It was developed to allow an easy-to-use, channel-separated communication on a LAN infrastructure.
<br><br>
The library provides a server and two client types: one for network communication and one for local communication.
<br>
The client types are also called *nodes* and they implement the [**IIrisNode**](/Iris.NET/Iris.NET.Common/Nodes/IIrisNode.cs) interface, which defines the pub/sub methods (basically: *Publish*, *Subscribe* and *Unsubscribe*).
<br><br>
The **IrisClientNode** type is responsible for the network communication and talks to the **IrisServer** through a socket connection. It can be found in the *Iris.NET.Client* namespace.
<br><br>
The **IrisLocalNode** type, instead, is used to communicate locally (without sockets) with the same pub/sub infrastructure of the server.

The **IrisServer** type handles the connections coming from the client nodes. This and *IrisLocalNode* can be found in the *Iris.NET.Server* namespace.
<br><br>
## *How To*
### Create and start a server:<br>
```csharp
IrisServer server = new IrisServer();
server.Start(2200); // Start on port 2200
```
### Create and connect a node over the network:<br>
```csharp
IrisClientConfig config = new IrisClientConfig() { Hostname = "127.0.0.1", Port = 2200 };
IrisClientNode node = new IrisClientNode();
node.Connect(config);
```
### Subscribe to a channel
```csharp
Task<IDisposableSubscription> asyncSubscriptionRequest = node.Subscribe("worldnews", MyContentHandler);
// The task is resolved when the subscription request is sent over the network stream
IDisposableSubscription subscription = await asyncSubscriptionRequest;
```
### Publish a message
```csharp
await node.Publish("worldnews", "something good happened");
```

A broader documentation is available in the [How-To](https://github.com/tommasobertoni/Iris.NET/wiki/How-To) page in the wiki.

To see the new features that are being developed and the breaking changes head over to the ***[Changelog](https://github.com/tommasobertoni/Iris.NET/wiki/Changelog)*** in the wiki.
<br>
New features proposals and bug reports are displayed in the ***[Issues](https://github.com/tommasobertoni/Iris.NET/issues)*** section.
<br><br><br>
## Technology info
This project was developed using *Visual Studio 2015* and *C# 6*.<br>

**The library targets the .NET Framework 4.0.**
<br><br>
## License
**Iris.NET** is distributed under the [MIT License](https://github.com/tommasobertoni/Iris.NET/blob/master/LICENCE).
