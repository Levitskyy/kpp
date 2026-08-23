using System;
using System.Collections.Generic;

namespace Game.Scripts.Common.Network.FacepunchTransport.Core
{
	public static class ByteArrayPool
	{
		private static readonly Queue<byte[]> m_byteArrays = new();
		
		public static byte[] Retrieve(int minimumLength)
		{
			byte[] result = null;

			if (m_byteArrays.Count > 0)
			{
				result = m_byteArrays.Dequeue();
			}

			int doubleMinimumLength = (minimumLength * 2);

			if (result == null)
			{
				result = new byte[doubleMinimumLength];
			}
			else if (result.Length < minimumLength)
			{
				Array.Resize(ref result, doubleMinimumLength);
			}

			return result;
		}
		
		public static void Store(byte[] buffer)
		{
			/* Holy cow that's a lot of buffered
			 * buffers. This wouldn't happen under normal
			 * circumstances but if the user is stress
			 * testing connections in one executable perhaps. */
			if (m_byteArrays.Count > 300)
			{
				return;
			}
			m_byteArrays.Enqueue(buffer);
		}
	}
}