using System;
using Steamworks;
using Steamworks.Data;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{
	public class FacepunchSocketManager : SocketManager
	{
		public Action<Connection, IntPtr, int> ForwardMessage;

		public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
		{
			ForwardMessage(connection, data, size);
		}
	}
}