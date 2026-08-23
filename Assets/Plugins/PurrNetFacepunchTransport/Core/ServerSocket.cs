using System;
using System.Collections.Generic;
using PurrNet.Logging;
using PurrNet.Transports;
using Steamworks;
using Steamworks.Data;
using Connection = Steamworks.Data.Connection;
using ConnectionState = PurrNet.Transports.ConnectionState;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{

	public class ServerSocket : CommonSocket
	{
		private readonly Queue<int> m_cachedConnectionIds = new();
		private readonly Queue<LocalPacket> m_clientHostIncoming = new();
		private readonly BidirectionalDictionary<Connection, int> m_steamConnections = new();
		private readonly BidirectionalDictionary<SteamId, int> m_steamIds = new();
		private ClientHostSocket m_clientHost;
		private bool m_clientHostStarted = false;
		private int m_nextConnectionId;
		private FacepunchSocketManager m_socketManager;

		internal ConnectionState GetRemoteConnectionState(int connectionId)
		{
			return m_steamConnections.Second.ContainsKey(connectionId)
				? ConnectionState.Connected
				: ConnectionState.Disconnected;
		}

		internal void ResetInvalidSocket()
		{
			if (m_socketManager == null)
			{
				base.SetLocalConnectionState(ConnectionState.Disconnected, true);
			}
		}

		internal bool StartConnection(string address, ushort port)
		{
			SteamNetworkingSockets.OnConnectionStatusChanged += OnRemoteConnectionState;

			m_nextConnectionId = 1;
			m_cachedConnectionIds.Clear();

			base.SetLocalConnectionState(ConnectionState.Connecting, true);

			if (m_socketManager != null)
			{
				m_socketManager?.Close();
				m_socketManager = null;
			}

#if UNITY_SERVER
            m_socketManager = SteamNetworkingSockets.CreateNormalSocket<FacepunchSocketManager>(NetAddress.From(address, port));
#else
			m_socketManager = SteamNetworkingSockets.CreateRelaySocket<FacepunchSocketManager>();
#endif
			m_socketManager.ForwardMessage = OnMessageReceived;

			base.SetLocalConnectionState(ConnectionState.Connected, true);

			return true;
		}

		internal void StopConnection()
		{
			if (SocketConnectionState == ConnectionState.Disconnected)
			{
				return;
			}

			base.SetLocalConnectionState(ConnectionState.Disconnecting, true);

			if (m_socketManager != null)
			{
				SteamNetworkingSockets.OnConnectionStatusChanged -= OnRemoteConnectionState;
				m_socketManager?.Close();
				m_socketManager = null;
			}

			base.SetLocalConnectionState(ConnectionState.Disconnected, true);
		}

		internal void StopConnection(int connectionId)
		{
			if (connectionId == NetworkConstants.CLIENT_HOST_ID)
			{
				if (m_clientHost != null)
				{
					m_clientHost.StopConnection();

					return;
				}

				return;
			}

			if (m_steamConnections.Second.TryGetValue(connectionId, out Connection steamConn))
			{
				StopConnection(connectionId, steamConn);

				return;
			}

			PurrLogger.LogError($"Steam connection not found for connectionId {connectionId}.");
		}
		
		private void StopConnection(int connectionId, Connection socket)
		{
			socket.Close(false, 0, "Graceful disconnect");
			m_steamConnections.Remove(connectionId);
			m_steamIds.Remove(connectionId);
			PurrLogger.Log($"Client with ConnectionID {connectionId} disconnected.");
			Transport.RemoveConnectionFromList(connectionId);
			Transport.HandleRemotePlayerDisconnect(connectionId, true, DisconnectReason.ClientRequest);
			m_cachedConnectionIds.Enqueue(connectionId);
		}
		
		private void OnRemoteConnectionState(Connection conn, ConnectionInfo info)
		{
			ulong clientSteamID = info.Identity.SteamId;

			if (info.State == Steamworks.ConnectionState.Connecting)
			{
				if (m_steamConnections.Count >= NetworkConstants.MAX_PLAYERS)
				{
					PurrLogger.Log($"Incoming connection {clientSteamID} was rejected because would exceed the maximum connection count.");

					conn.Close(false, 0, "Max Connection Count");
					return;
				}

				Result res;

				if ((res = conn.Accept()) == Result.OK)
				{
					PurrLogger.Log($"Accepting connection {clientSteamID}");
				}
				else
				{
					PurrLogger.Log($"Connection {clientSteamID} could not be accepted: {res.ToString()}");
				}
			}
			else if (info.State == Steamworks.ConnectionState.Connected)
			{
				int connectionId = (m_cachedConnectionIds.Count > 0) ? m_cachedConnectionIds.Dequeue() : m_nextConnectionId++;
				m_steamConnections.Add(conn, connectionId);
				m_steamIds.Add(clientSteamID, connectionId);

				PurrLogger.Log($"Client with SteamID {clientSteamID} connected. Assigning connection id {connectionId}");
				Transport.AddConnectionToList(connectionId);
				Transport.HandleRemotePlayerConnect(connectionId, true);
			}
			else if (info.State == Steamworks.ConnectionState.ClosedByPeer || info.State == Steamworks.ConnectionState.ProblemDetectedLocally)
			{
				if (m_steamConnections.TryGetValue(conn, out int connId))
				{
					StopConnection(connId, conn);
				}
			}
			else
			{
				PurrLogger.Log($"Connection {clientSteamID} state changed: {info.State.ToString()}");
			}
		}

		internal void IterateOutgoing()
		{
			if (SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			foreach (Connection conn in m_steamConnections.FirstTypes)
			{
				conn.Flush();
			}
		}

		internal void IterateIncoming()
		{
			if (SocketConnectionState == ConnectionState.Disconnected || SocketConnectionState == ConnectionState.Disconnecting)
			{
				return;
			}

			//Iterate local client packets first.
			while (m_clientHostIncoming.Count > 0)
			{
				LocalPacket packet = m_clientHostIncoming.Dequeue();
				var segment = new ArraySegment<byte>(packet.Data, 0, packet.Length);
				Transport.RaiseDataReceived(new PurrNet.Transports.Connection(NetworkConstants.CLIENT_HOST_ID), new ByteData(segment), true);
				packet.Dispose();
			}

			m_socketManager.Receive(MAX_MESSAGES);
		}

		private void OnMessageReceived(Connection conn, IntPtr dataPtr, int size)
		{
			byte[] data = ProcessMessage(dataPtr, size);
			Transport.RaiseDataReceived(new PurrNet.Transports.Connection(m_steamConnections[conn]), new ByteData(new ArraySegment<byte>(data)), true);
		}
		
		internal void SendToClient(Channel channelId, ByteData data, int connectionId)
		{
			if (SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			//Check if sending local client first, send and exit if so.
			if (connectionId == NetworkConstants.CLIENT_HOST_ID)
			{
				if (m_clientHost != null)
				{
					LocalPacket packet = new(data.segment);

					m_clientHost.ReceivedFromLocalServer(packet);
				}

				return;
			}

			if (m_steamConnections.TryGetValue(connectionId, out Connection steamConn))
			{
				Result res = Send(steamConn, data, channelId);

				if (res == Result.NoConnection || res == Result.InvalidParam)
				{
					PurrLogger.Log($"Connection to {connectionId} was lost.");

					StopConnection(connectionId, steamConn);
				}
				else if (res != Result.OK)
				{
					PurrLogger.LogError($"Could not send: {res.ToString()}");
				}
			}
			else
			{
				PurrLogger.LogError($"ConnectionId {connectionId} does not exist, data will not be sent.");
			}
		}
		
		internal string GetConnectionAddress(int connectionId)
		{
			if (m_steamIds.TryGetValue(connectionId, out SteamId steamId))
			{
				return steamId.ToString();
			}

			PurrLogger.LogError($"ConnectionId {connectionId} is invalid; address cannot be returned.");

			return string.Empty;
		}

		#region ClientHost (local client).
		
		internal void SetClientHostSocket(ClientHostSocket socket)
		{
			m_clientHost = socket;
		}
		
		internal void OnClientHostState(bool started)
		{
			m_clientHostStarted = started;

			SteamId steamId = new()
			{
				Value = Transport.LocalUserSteamID
			};
			
			if (!started && m_clientHostStarted)
			{
				ClearQueue(m_clientHostIncoming);
				Transport.RemoveConnectionFromList(NetworkConstants.CLIENT_HOST_ID);
				Transport.HandleRemotePlayerDisconnect(NetworkConstants.CLIENT_HOST_ID, false, DisconnectReason.ClientRequest);
				m_steamIds.Remove(steamId);
			}
			else if (started)
			{
				m_steamIds[steamId] = NetworkConstants.CLIENT_HOST_ID;
				Transport.AddConnectionToList(NetworkConstants.CLIENT_HOST_ID);
				Transport.HandleRemotePlayerConnect(NetworkConstants.CLIENT_HOST_ID, false);
			}

			m_clientHostStarted = started;
		}
		
		internal void ReceivedFromClientHost(LocalPacket packet)
		{
			if (!m_clientHostStarted)
			{
				packet.Dispose();
				return;
			}

			m_clientHostIncoming.Enqueue(packet);
		}

		#endregion
	}

}