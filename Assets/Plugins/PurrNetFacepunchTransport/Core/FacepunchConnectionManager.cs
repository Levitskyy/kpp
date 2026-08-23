using System;
using Steamworks;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{
	public class FacepunchConnectionManager : ConnectionManager
	{
		public Action<IntPtr, int> ForwardMessage;

		public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
		{
			ForwardMessage(data, size);
		}
	}
}