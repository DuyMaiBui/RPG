using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using AuraEngine.Core;
using AuraEngine.Networking;
using AuraEngine.Physics;
using AuraEngine.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace AuraEngine.Tests
{
    public sealed class NetworkingTransportTests
    {
        [Test]
        public void UdpTransport_SendReceivePredictServer()
        {
            using var server = new UdpServerLoopback(out var port);

            using var client = UdpAuraTransport.CreateClient("127.0.0.1", port);
            client.Send(AuraNetCodec.EncodeInput(1u, 1u, 2, 3));

            var payload = PollServer(server, TimeSpan.FromSeconds(2));
            Assert.IsNotNull(payload, "the server did not receive the UDP datagram.");
            Assert.IsTrue(AuraNetCodec.TryDecodeInput(payload, out var command));
            Assert.AreEqual(1u, command.ClientId);
            Assert.AreEqual(2, command.MoveX);

            server.SendToLast(payload);
            var echo = PollClient(client, TimeSpan.FromSeconds(2));
            Assert.IsNotNull(echo, "the client did not receive the UDP response.");
        }

        [Test]
        public void UdpTransport_PredictionMatchesServerOverLoopback()
        {
            var serverWorld = new AuraSimulationWorld(ManagedPhysicsBackend.Instance, new AuraWorldDefinition(initialBodyCapacity: 8));
            var serverHandler = new AuraInputCommandHandler(1f);
            serverWorld.RegisterCommandHandler(serverHandler);

            using var server = new UdpServerLoopback(out var port);
            using var client = UdpAuraTransport.CreateClient("127.0.0.1", port);
            var netServer = new AuraNetServer(serverWorld, server, 60);
            var entity = AddKinematicActor(serverWorld, serverHandler);

            for (var frame = 0; frame < 30; frame++)
            {
                client.Send(AuraNetCodec.EncodeInput(1u, (uint)(frame + 1), 1, 0));
                netServer.PumpFrom(server, TimeSpan.FromMilliseconds(200));
                netServer.Step();
                PollClient(client, TimeSpan.FromMilliseconds(200));
                serverWorld.TryGetBodyState(entity, out _);
            }

            Assert.IsNull(netServer.Fault, netServer.Fault == null ? "server faulted" : netServer.Fault.ToString());
            Assert.AreEqual(29, (int)netServer.CurrentTick.Value);
            ((System.IDisposable)serverWorld).Dispose();
        }

        [Test]
        public void WebSocketTransport_SendReceive()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var acceptTask = listener.AcceptTcpClientAsync();
            using var clientTcp = new TcpClient();
            clientTcp.Connect(IPAddress.Loopback, port);
            using var serverTcp = acceptTask.Wait(TimeSpan.FromSeconds(5)) ? acceptTask.Result : null;
            Assert.IsNotNull(serverTcp, "tcp accept timed out.");

            using var serverSocket = WebSocket.CreateFromStream(serverTcp.GetStream(), false, null, TimeSpan.FromSeconds(5));
            using var clientSocket = WebSocket.CreateFromStream(clientTcp.GetStream(), true, null, TimeSpan.FromSeconds(5));

            using var serverTransport = new WebSocketAuraTransport(serverSocket);
            using var clientTransport = new WebSocketAuraTransport(clientSocket);

            clientTransport.Send(AuraNetCodec.EncodeInput(2u, 5u, -1, 4));
            var payload = PollWebSocket(serverTransport, TimeSpan.FromSeconds(3));
            Assert.IsNotNull(payload, "the websocket server did not receive a frame.");
            Assert.IsTrue(AuraNetCodec.TryDecodeInput(payload, out var command));
            Assert.AreEqual(2u, command.ClientId);
            Assert.AreEqual(-1, command.MoveX);

            serverTransport.Send(payload);
            var echo = PollWebSocket(clientTransport, TimeSpan.FromSeconds(3));
            Assert.IsNotNull(echo, "the websocket client did not receive the echo.");

            listener.Stop();
        }

        private static byte[] PollServer(UdpServerLoopback server, TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                if (server.TryReceive(out var payload))
                    return payload;
                Thread.Sleep(5);
            }

            return null;
        }

        private static byte[] PollClient(UdpAuraTransport client, TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                if (client.TryReceive(out var payload))
                    return payload;
                Thread.Sleep(5);
            }

            return null;
        }

        private static byte[] PollWebSocket(WebSocketAuraTransport transport, TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                if (transport.TryReceive(out var payload))
                    return payload;
                Thread.Sleep(10);
            }

            return null;
        }

        private static SimulationEntityId AddKinematicActor(AuraSimulationWorld world, AuraInputCommandHandler handler)
        {
            var entity = world.CreateEntity();
            world.AttachBody(
                entity,
                AuraPhysicsBodyDefinition.CreateKinematic(
                    new AuraPose(AuraVector3.Zero, AuraQuaternion.Identity),
                    AuraPhysicsLayer.Default,
                    AuraPhysicsLayerMask.All,
                    AuraPhysicsShapeDefinition.Box(new AuraVector3(0.5f, 0.5f, 0.5f))));
            handler.Entity = entity;
            return entity;
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        private sealed class UdpServerLoopback : IAuraTransport, IDisposable
        {
            private readonly Socket _socket;
            private EndPoint _lastSource;
            private byte[] _lastPayload;

            public UdpServerLoopback(out int port)
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                port = ((IPEndPoint)_socket.LocalEndPoint).Port;
            }

            public bool IsConnected => true;

            public event Action<byte[]> Received;
            public event Action Disconnected;

            public void Send(ReadOnlySpan<byte> payload) => SendToLast(payload.ToArray());

            public void SendToLast(byte[] payload)
            {
                if (_lastSource != null)
                    _socket.SendTo(payload, SocketFlags.None, _lastSource);
            }

            public bool TryReceive(out byte[] payload)
            {
                payload = null;
                if (!_socket.Poll(0, SelectMode.SelectRead))
                    return false;

                var buffer = new byte[64 * 1024];
                EndPoint source = new IPEndPoint(IPAddress.Any, 0);
                var count = _socket.ReceiveFrom(buffer, ref source);
                if (count <= 0)
                    return false;

                payload = new byte[count];
                Array.Copy(buffer, payload, count);
                _lastPayload = payload;
                _lastSource = source;
                Received?.Invoke(payload);
                return true;
            }

            public void Disconnect() => Disconnected?.Invoke();

            public void Dispose() => _socket.Dispose();
        }
    }
}
