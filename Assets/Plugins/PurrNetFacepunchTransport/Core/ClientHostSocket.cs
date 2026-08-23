using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PurrNet.Logging;
using PurrNet.Transports;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{

	public class ClientHostSocket : CommonSocket
	{
		private readonly Queue<LocalPacket> m_incoming = new();
		private CancellationTokenSource m_cts;
		private ServerSocket m_server;

		~ClientHostSocket()
		{
			m_cts?.Dispose();
		}

		internal void StartConnection(ServerSocket serverSocket)
		{
			m_server = serverSocket;
			m_server.SetClientHostSocket(this);

			if (m_server.SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			SetLocalConnectionState(ConnectionState.Connecting, false);

			m_cts = new();

			CheckSetStartedAsync().Forget();
		}

		private async UniTaskVoid CheckSetStartedAsync()
		{
			try
			{
				while (!m_cts.IsCancellationRequested)
				{
					await UniTask.Yield(m_cts.Token);

					if (m_server != null && SocketConnectionState == ConnectionState.Connecting)
					{
						if (m_server.SocketConnectionState == ConnectionState.Connected)
						{
							SetLocalConnectionState(ConnectionState.Connected, false);

							break;
						}
					}
				}
			}
			catch (OperationCanceledException) { }
			catch (Exception e)
			{
				PurrLogger.LogException(e);
			}
		}

		protected override void SetLocalConnectionState(ConnectionState connectionState, bool server)
		{
			base.SetLocalConnectionState(connectionState, server);

			if (connectionState == ConnectionState.Connected)
			{
				m_server.OnClientHostState(true);
			}
			else
			{
				m_server.OnClientHostState(false);
			}
		}

		internal void StopConnection()
		{
			if (SocketConnectionState == ConnectionState.Disconnected || SocketConnectionState == ConnectionState.Disconnecting)
			{
				return;
			}

			ClearQueue(m_incoming);

			SetLocalConnectionState(ConnectionState.Disconnecting, false);
			SetLocalConnectionState(ConnectionState.Disconnected, false);

			m_server.SetClientHostSocket(null);

			m_cts?.Cancel();
		}

		internal void IterateIncoming()
		{
			if (SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			while (m_incoming.Count > 0)
			{
				LocalPacket packet = m_incoming.Dequeue();
				var segment = new ArraySegment<byte>(packet.Data, 0, packet.Length);
				Transport.RaiseDataReceived(new Connection(-1), new ByteData(segment), false);
				packet.Dispose();
			}
		}

		internal void ReceivedFromLocalServer(LocalPacket packet)
		{
			m_incoming.Enqueue(packet);
		}

		internal void SendToServer(ArraySegment<byte> segment)
		{
			if (SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			if (m_server.SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			LocalPacket packet = new(segment);

			m_server.ReceivedFromClientHost(packet);
		}
	}
}