using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace QRCodeMaker.Core
{
	/// <summary>
	/// An appendable sequence of bits (0s and 1s). Mainly used by QrSegment.
	/// </summary>
	public class BitBuffer : List<bool>
	{
		/*---- Constructor ----*/

		// Creates an empty bit buffer (length 0).
		public BitBuffer()
		{
		}

		public BitBuffer(BitBuffer source) : base(source)
		{
		}


		/*---- Method ----*/

		// Appends the given number of low-order bits of the given value
		// to this buffer. Requires 0 <= len <= 31 and val < 2^len.
		public void AppendBits(uint val, int len)
		{
			if (len < 0 || len > 31 || val >> len != 0)
			{
				throw new Exception("Value out of range");
				//throw std::domain_error("Value out of range");
			}
			for (int i = len - 1; i >= 0; i--)  // Append bit by bit
			{
				this.Add(((val >> i) & 1) != 0);
			}
		}
		public void AppendBits(int val, int len)
		{
			AppendBits((uint)val, len);
		}

		public int BitLength
		{
			get
			{
				Debug.Assert(this.Count >= 0);
				return this.Count;
			}
		}

		public void AppendData(BitBuffer bb)
		{
			if (int.MaxValue - Count < bb.Count)
			{
				throw new Exception("Maximum length reached");
				//throw new IllegalStateException("Maximum length reached");
			}
			AddRange(bb);// Append bit by bit
		}

		public int GetBit(int index)
		{
			if (index < 0 || index >= Count)
			{
				throw new IndexOutOfRangeException();
				//throw new IndexOutOfBoundsException();
			}
			return this[index] ? 1 : 0;
		}
	}
}
