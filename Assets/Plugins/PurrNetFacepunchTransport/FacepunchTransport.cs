using System;
using System.Collections.Generic;
using Game.Scripts.Common.Network.FacepunchTransport.Core;
using PurrNet.Transports;
using Steamworks;
using UnityEngine;
using ConnectionState = PurrNet.Transports.ConnectionState;

namespace Game.Scripts.Common.Network.FacepunchTransport
{

	[DefaultExecutionOrder(-100)]
	public class FacepunchTransport : GenericTransport, ITransport
	{
		private readonly ClientSocket m_client = new();
		private readonly ClientHostSocket m_clientHost = new();

		private readonly List<Connection> m_connections = new();
		private readonly ServerSocket m_server = new();
		private string m_clientAddress = string.Empty;

		private ConnectionState m_clientState = ConnectionState.Disconnected;
		private ConnectionState m_listenerState = ConnectionState.Disconnected;
		private ushort m_port = 27015;
		private string m_serverBindAddress = string.Empty;

		public override bool isSupported => true;

		public override ITransport transport => this;

		public ulong LocalUserSteamID { get; private set; }

		public ConnectionState listenerState
		{
			get => m_listenerState;

			private set
			{
				if (m_listenerState == value)
				{
					return;
				}

				m_listenerState = value;
				onConnectionState?.Invoke(m_listenerState, true);
			}
		}

		public ConnectionState clientState
		{
			get => m_clientState;
			private set
			{
				if (m_clientState == value)
				{
					return;
				}

				m_clientState = value;
				onConnectionState?.Invoke(m_clientState, false);
			}
		}

		public IReadOnlyList<Connection> connections => m_connections;

		public event OnDataReceived onDataReceived;

		public event OnDataSent onDataSent;

		public event OnConnectionState onConnectionState;

		public event OnConnected onConnected;

		public event OnDisconnected onDisconnected;

		#region Init & DeInit & Unity

		public void Initialize(string clientAddress, ushort port, string serverBindAddress = null)
		{
			m_clientAddress = clientAddress;
			m_port = port;
			m_serverBindAddress = string.IsNullOrEmpty(serverBindAddress) ? NetworkConstants.LOCALHOST_IP : serverBindAddress;

#if UNITY_SERVER
			if (string.IsNullOrEmpty(serverBindAddress))
			{
				throw new ArgumentException("serverBindAddress is null or empty with UNITY_SERVER build");
			}
#endif

#if !UNITY_SERVER
			if (!SteamClient.IsValid)
			{
				throw new InvalidOperationException("Steam client is not initialized");
			}

			SteamNetworking.AllowP2PPacketRelay(true);
#endif

			m_clientHost.Initialize(this);
			m_client.Initialize(this);
			m_server.Initialize(this);
		}

		#endregion

		#region States & Actions

		public void HandleSelfConnectionState(ConnectionState connectionState, bool asServer)
		{
			if (asServer)
			{
				listenerState = connectionState;
			}
			else
			{
				clientState = connectionState;
			}
		}

		public void HandleRemotePlayerConnect(int connectionId, bool asServer)
		{
			Connection connection = new(connectionId);
			onConnected?.Invoke(connection, asServer);
		}

		public void AddConnectionToList(int connectionId)
		{
			Connection connection = new(connectionId);
			m_connections.Add(connection);
		}

		public void HandleRemotePlayerDisconnect(int connectionId, bool asServer, DisconnectReason reason)
		{
			Connection connection = new(connectionId);
			onDisconnected?.Invoke(connection, reason, asServer);
		}

		public void RemoveConnectionFromList(int connectionId)
		{
			Connection connection = new(connectionId);
			m_connections.Remove(connection);
		}

		#endregion

		#region Data

		public void SendToClient(Connection target, ByteData data, Channel method = Channel.ReliableOrdered)
		{
			if (!target.isValid)
			{
				return;
			}

			if (listenerState is not ConnectionState.Connected)
			{
				return;
			}

			m_server.SendToClient(method, data, target.connectionId);
			RaiseDataSent(target, data, true);
		}

		public void SendToServer(ByteData data, Channel method = Channel.ReliableOrdered)
		{
			m_client.SendToServer(method, data);
			m_clientHost.SendToServer(data.segment);
			RaiseDataSent(default, data, false);
		}

		public void RaiseDataReceived(Connection conn, ByteData data, bool asServer)
		{
			onDataReceived?.Invoke(conn, data, asServer);
		}

		public void RaiseDataSent(Connection conn, ByteData data, bool asServer)
		{
			onDataSent?.Invoke(conn, data, asServer);
		}

		public void ReceiveMessages(float delta)
		{
			m_server.IterateIncoming();
			m_client.IterateIncoming();
			m_clientHost.IterateIncoming();
		}

		public void SendMessages(float delta)
		{
			m_server.IterateOutgoing();
			m_client.IterateOutgoing();
		}

		#endregion

		#region Start & Stop

		// server

		protected override void StartServerInternal()
		{
			Listen(m_port);
		}

		public void Listen(ushort port)
		{
			bool clientRunning = false;
#if !UNITY_SERVER
			if (!SteamClient.IsValid)
			{
				throw new InvalidOperationException("Steam client is not initialized");
			}

			clientRunning = (m_client.SocketConnectionState != ConnectionState.Disconnected);

			if (clientRunning)
			{
				m_client.StopConnection();
			}
#endif

			m_server.ResetInvalidSocket();

			if (m_server.SocketConnectionState != ConnectionState.Disconnected)
			{
				throw new InvalidOperationException("Server is already running");
			}

			InitializeRelayNetworkAccess();

			bool result = m_server.StartConnection(m_serverBindAddress, m_port);

			if (result && clientRunning)
			{
				StartClient();
			}
		}

		public void StopListening()
		{
			m_server.StopConnection();
		}

		public void CloseConnection(Connection conn)
		{
			m_server.StopConnection(conn.connectionId);
		}

		// ---

		// client

		protected override void StartClientInternal()
		{
			Connect(m_clientAddress, m_port);
		}

		public void Connect(string ip, ushort port)
		{
			if (!SteamClient.IsValid)
			{
				throw new InvalidOperationException("Steam client is not initialized");
			}

			if (m_server.SocketConnectionState == ConnectionState.Disconnected)
			{
				if (m_client.SocketConnectionState != ConnectionState.Disconnected)
				{
					throw new InvalidOperationException("Client is already running");
				}

				if (m_clientHost.SocketConnectionState != ConnectionState.Disconnected)
				{
					m_clientHost.StopConnection();
				}

				InitializeRelayNetworkAccess();

				m_client.StartConnectionAsync(ip, port).Forget();
			}
			else
			{
				m_clientHost.StartConnection(m_server);
			}
		}

		public void Disconnect()
		{
			m_client.StopConnection();
			m_clientHost.StopConnection();
		}

		// ---

		#endregion

		#region Utils

		private void Shutdown()
		{
			StopServer();
			StopClient();
		}

		private void InitializeRelayNetworkAccess()
		{
#if !UNITY_SERVER
			SteamNetworkingUtils.InitRelayNetworkAccess();
			LocalUserSteamID = SteamClient.SteamId.Value;
#endif
		}

		#endregion
	}

}