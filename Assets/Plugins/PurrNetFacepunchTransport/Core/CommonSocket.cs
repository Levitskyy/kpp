using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using PurrNet.Logging;
using PurrNet.Transports;
using Steamworks;
using Steamworks.Data;
using Connection = Steamworks.Data.Connection;
using ConnectionState = PurrNet.Transports.ConnectionState;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{

	public abstract class CommonSocket
	{
		protected const int MAX_MESSAGES = 256;

		private ConnectionState m_socketConnectionState = ConnectionState.Disconnected;

		protected FacepunchTransport Transport = null;

		public ConnectionState SocketConnectionState => m_socketConnectionState;

		internal void Initialize(FacepunchTransport t)
		{
			Transport = t;
		}

		internal static void ClearQueue(Queue<LocalPacket> lpq)
		{
			while (lpq.Count > 0)
			{
				LocalPacket lp = lpq.Dequeue();
				lp.Dispose();
			}
		}

		protected static bool IsValidAddress(string address)
		{
			if (!string.IsNullOrEmpty(address))
			{
				if (!IPAddress.TryParse(address, out IPAddress result))
				{
					return false;
				}

				return true;
			}

			return false;
		}
		
		protected static Result Send(Connection conn, ByteData data, Channel channel)
		{
			var segment = new ArraySegment<byte>(data.data, data.offset, data.length + 1);

			GCHandle pinnedArray = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
			IntPtr pData = pinnedArray.AddrOfPinnedObject() + segment.Offset;

			SendType sendFlag = channel == Channel.Unreliable ? SendType.Unreliable : SendType.Reliable;
			Result result = conn.SendMessage(pData, segment.Count, sendFlag);

			if (result != Result.OK)
			{
				PurrLogger.LogWarning($"Send issue: {result}");
			}

			pinnedArray.Free();

			return result;
		}
		
		protected static byte[] ProcessMessage(IntPtr ptrs, int size)
		{
			byte[] managedArray = new byte[size];

			Marshal.Copy(ptrs, managedArray, 0, size);

			Array.Resize(ref managedArray, managedArray.Length - 1);

			return managedArray;
		}

		protected virtual void SetLocalConnectionState(ConnectionState connectionState, bool asServer)
		{
			if (connectionState == m_socketConnectionState)
			{
				return;
			}

			m_socketConnectionState = connectionState;

			Transport.HandleSelfConnectionState(connectionState, asServer);
		}
	}
}