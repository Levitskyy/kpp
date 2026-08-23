using System;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{
	internal struct LocalPacket
	{
		public byte[] Data;
		public int Length;

		public LocalPacket(ArraySegment<byte> data)
		{
			Data = ByteArrayPool.Retrieve(data.Count);
			Length = data.Count;
			Buffer.BlockCopy(data.Array, data.Offset, Data, 0, Length);
		}

		public void Dispose()
		{
			if (Data != null)
			{
				ByteArrayPool.Store(Data);
			}
		}
	}

}