using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using PurrNet.Logging;
using PurrNet.Transports;
using Steamworks;
using Steamworks.Data;
using Channel = PurrNet.Transports.Channel;
using Connection = Steamworks.Data.Connection;
using ConnectionState = PurrNet.Transports.ConnectionState;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{

	public class ClientSocket : CommonSocket
	{
		private CancellationTokenSource m_cts;
		private FacepunchConnectionManager m_hostConnectionManager;
		private SteamId m_hostSteamID = 0;
		private UniTaskCompletionSource m_tcs;

		private Connection HostConnection => m_hostConnectionManager.Connection;

		internal async UniTaskVoid StartConnectionAsync(string address, ushort port)
		{
			m_cts?.Cancel();

			m_cts = new CancellationTokenSource();
			TimeSpan timeout = TimeSpan.FromSeconds(NetworkConstants.TRANSPORT_CONNECTION_TIMEOUT);

			using CancellationTokenSource timeoutCts = new(timeout);
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(m_cts.Token, timeoutCts.Token);

			SteamNetworkingSockets.OnConnectionStatusChanged += OnConnectionStatusChanged;

			SetLocalConnectionState(ConnectionState.Connecting, false);

			try
			{
				if (!SteamClient.IsValid)
				{
					PurrLogger.LogError("SteamWorks not initialized");

					SetLocalConnectionState(ConnectionState.Disconnected, false);

					return;
				}

				m_tcs = new();

				await using CancellationTokenRegistration registration = linkedCts.Token.Register(() => { m_tcs.TrySetCanceled(); });

				if (!IsValidAddress(address))
				{
					m_hostSteamID = UInt64.Parse(address);
					m_hostConnectionManager = SteamNetworkingSockets.ConnectRelay<FacepunchConnectionManager>(m_hostSteamID);
				}
				else
				{
					m_hostConnectionManager = SteamNetworkingSockets.ConnectNormal<FacepunchConnectionManager>(NetAddress.From(address, port));
				}

				m_hostConnectionManager.ForwardMessage = OnMessageReceived;

				await m_tcs.Task;
			}
			catch (OperationCanceledException)
			{
				PurrLogger.LogError(timeoutCts.IsCancellationRequested ? $"Connection to {address} timed out." : "The connection attempt was cancelled.");

				StopConnection();
			}
			catch (FormatException)
			{
				PurrLogger.LogError($"Connection string was not in the right format. Did you enter a SteamId?");

				SetLocalConnectionState(ConnectionState.Disconnected, false);
			}
			catch (Exception ex)
			{
				PurrLogger.LogError(ex.Message);

				SetLocalConnectionState(ConnectionState.Disconnected, false);
			}
			finally
			{
				if (SocketConnectionState != ConnectionState.Connected)
				{
					SetLocalConnectionState(ConnectionState.Disconnected, false);
				}
			}
		}

		private void OnConnectionStatusChanged(Connection conn, ConnectionInfo info)
		{
			switch (info.State)
			{
				case Steamworks.ConnectionState.Connected:
					PurrLogger.Log($"Connection was connected: {info.EndReason}");
					SetLocalConnectionState(ConnectionState.Connected, false);
					m_tcs?.TrySetResult();
					break;

				case Steamworks.ConnectionState.ClosedByPeer:
				case Steamworks.ConnectionState.ProblemDetectedLocally:
					PurrLogger.Log($"Connection was closed by peer, {info.EndReason}");
					m_tcs?.TrySetCanceled();
					StopConnection();
					break;

				default:
					PurrLogger.Log($"Connection state changed: {info.State} - {info.EndReason}");
					break;
			}
		}

		internal bool StopConnection()
		{
			if (SocketConnectionState == ConnectionState.Disconnected || SocketConnectionState == ConnectionState.Disconnecting)
			{
				return false;
			}

			SetLocalConnectionState(ConnectionState.Disconnecting, false);

			m_cts?.Cancel();

			SteamNetworkingSockets.OnConnectionStatusChanged -= OnConnectionStatusChanged;

			if (m_hostConnectionManager != null)
			{
				PurrLogger.Log("Sending Disconnect message");
				HostConnection.Close(false, 0, "Graceful disconnect");
				m_hostConnectionManager = null;
			}

			SetLocalConnectionState(ConnectionState.Disconnected, false);

			return true;
		}

		private void OnMessageReceived(IntPtr dataPtr, int size)
		{
			byte[] data = ProcessMessage(dataPtr, size);
			Transport.RaiseDataReceived(new PurrNet.Transports.Connection(-1), new ByteData(new ArraySegment<byte>(data)), false);
		}

		internal void SendToServer(Channel method, ByteData data)
		{
			if (SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			Result res = Send(HostConnection, data, method);

			if (res == Result.NoConnection || res == Result.InvalidParam)
			{
				PurrLogger.Log("Connection to server was lost.");
				StopConnection();
			}
			else if (res != Result.OK)
			{
				PurrLogger.LogError($"Could not send: {res.ToString()}");
			}
		}

		internal void IterateIncoming()
		{
			if (SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			m_hostConnectionManager.Receive(MAX_MESSAGES);
		}

		internal void IterateOutgoing()
		{
			if (SocketConnectionState != ConnectionState.Connected)
			{
				return;
			}

			HostConnection.Flush();
		}

		protected override void SetLocalConnectionState(ConnectionState connectionState, bool asServer)
		{
			base.SetLocalConnectionState(connectionState, asServer);
			
			if (connectionState == ConnectionState.Connected)
			{
				Transport.HandleRemotePlayerConnect(0, false);
			}
			else if (connectionState == ConnectionState.Disconnected)
			{
				Transport.HandleRemotePlayerDisconnect(0, false, DisconnectReason.ClientRequest);
			}
		}
	}
}