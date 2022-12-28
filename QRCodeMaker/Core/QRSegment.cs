using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace QRCodeMaker.Core
{
	/**
	 * A segment of character/binary/control data in a QR Code symbol.
	 * Instances of this class are immutable.
	 * <p>The mid-level way to create a segment is to take the payload data and call a
	 * static factory function such as {@link QrSegment#makeNumeric(String)}. The low-level
	 * way to create a segment is to custom-make the bit buffer and call the {@link
	 * QrSegment#QrSegment(Mode,int,BitBuffer) constructor} with appropriate values.</p>
	 * <p>This segment class imposes no length restrictions, but QR Codes have restrictions.
	 * Even in the most favorable conditions, a QR Code can only hold 7089 characters of data.
	 * Any segment longer than this is meaningless for the purpose of generating QR Codes.
	 * This class can represent kanji mode segments, but provides no help in encoding them
	 * - see {@link QrSegmentAdvanced} for full kanji support.</p>
	 */
	public class QRSegment
	{

		/*---- Static factory functions (mid level) ----*/

		/**
		 * Returns a segment representing the specified binary data
		 * encoded in byte mode. All input byte arrays are acceptable.
		 * <p>Any text string can be converted to UTF-8 bytes ({@code
		 * s.getBytes(StandardCharsets.UTF_8)}) and encoded as a byte mode segment.</p>
		 * @param data the binary data (not {@code null})
		 * @return a segment (not {@code null}) containing the data
		 * @throws NullPointerException if the array is {@code null}
		 */
		public static QRSegment MakeBytes(IReadOnlyList<byte> data)
		{
			var bitBuffer = new BitBuffer();
			foreach (byte datum in data)
			{
				bitBuffer.AppendBits(datum, 8);
			}
			return new QRSegment(Mode.Byte, data.Count, bitBuffer);
		}


		/**
		 * Returns a segment representing the specified string of decimal digits encoded in numeric mode.
		 * @param digits the text (not {@code null}), with only digits from 0 to 9 allowed
		 * @return a segment (not {@code null}) containing the text
		 * @throws NullPointerException if the string is {@code null}
		 * @throws IllegalArgumentException if the string contains non-digit characters
		 */
		public static QRSegment MakeNumeric(string digits)
		{
			if (NumericRegex.Match(digits).Length != digits.Length)
			{
				throw new Exception("String contains non-numeric characters");
				//throw new IllegalArgumentException("String contains non-numeric characters");
			}

			var bitBuffer = new BitBuffer();
			for (int i = 0; i < digits.Length;)
			{
				// Consume up to 3 digits per iteration
				int n = Math.Min(digits.Length - i, 3);
				bitBuffer.AppendBits(int.Parse(digits.Substring(i, i + n)), n * 3 + 1);
				i += n;
			}
			return new QRSegment(Mode.Numeric, digits.Length, bitBuffer);
		}


		/**
		 * Returns a segment representing the specified text string encoded in alphanumeric mode.
		 * The characters allowed are: 0 to 9, A to Z (uppercase only), space,
		 * dollar, percent, asterisk, plus, hyphen, period, slash, colon.
		 * @param text the text (not {@code null}), with only certain characters allowed
		 * @return a segment (not {@code null}) containing the text
		 * @throws NullPointerException if the string is {@code null}
		 * @throws IllegalArgumentException if the string contains non-encodable characters
		 */
		public static QRSegment MakeAlphanumeric(string text)
		{
			if (AlphaNumericRegex.Match(text).Length != text.Length)
			{
				throw new Exception("String contains unencodable characters in alphanumeric mode");
				//throw new IllegalArgumentException("String contains unencodable characters in alphanumeric mode");
			}

			var bitBuffer = new BitBuffer();
			int i;
			for (i = 0; i <= text.Length - 2; i += 2)
			{
				// Process groups of 2
				int temp = AlphaNumericCharSet.IndexOf(text.ElementAt(i)) * 45;
				temp += AlphaNumericCharSet.IndexOf(text.ElementAt(i + 1));
				bitBuffer.AppendBits(temp, 11);
			}
			if (i < text.Length)  // 1 character remaining
			{
				bitBuffer.AppendBits(AlphaNumericCharSet.IndexOf(text.ElementAt(i)), 6);
			}
			return new QRSegment(Mode.AlphaNumeric, text.Length, bitBuffer);
		}


		/**
		 * Returns a list of zero or more segments to represent the specified Unicode text string.
		 * The result may use various segment modes and switch modes to optimize the length of the bit stream.
		 * @param text the text to be encoded, which can be any Unicode string
		 * @return a new mutable list (not {@code null}) of segments (not {@code null}) containing the text
		 * @throws NullPointerException if the text is {@code null}
		 */
		public static List<QRSegment> MakeSegments(string text)
		{
			// Select the most efficient segment encoding automatically
			var result = new List<QRSegment>();
			if (text.Equals(""))
			{
				// Leave result empty
			}
			else if (NumericRegex.Match(text).Length == text.Length)
			{
				result.Add(MakeNumeric(text));
			}
			else if (AlphaNumericRegex.Match(text).Length == text.Length)
			{
				result.Add(MakeAlphanumeric(text));
			}
			else
			{
				var bytes = Encoding.UTF8.GetBytes(text);
				result.Add(MakeBytes(bytes));
			}
			return result;
		}


		/**
		 * Returns a segment representing an Extended Channel Interpretation
		 * (ECI) designator with the specified assignment value.
		 * @param assignVal the ECI assignment number (see the AIM ECI specification)
		 * @return a segment (not {@code null}) containing the data
		 * @throws IllegalArgumentException if the value is outside the range [0, 10<sup>6</sup>)
		 */
		public static QRSegment MakeEci(int assignVal)
		{
			var bitBuffer = new BitBuffer();
			if (assignVal < 0)
			{
				throw new ArgumentOutOfRangeException("ECI assignment value out of range");
			}
			else if (assignVal < (1 << 7))
			{
				bitBuffer.AppendBits(assignVal, 8);
			}
			else if (assignVal < (1 << 14))
			{
				bitBuffer.AppendBits(2, 2);
				bitBuffer.AppendBits(assignVal, 14);
			}
			else if (assignVal < 1_000_000)
			{
				bitBuffer.AppendBits(6, 3);
				bitBuffer.AppendBits(assignVal, 21);
			}
			else
			{
				throw new ArgumentOutOfRangeException("ECI assignment value out of range");
			}
			return new QRSegment(Mode.ECI, 0, bitBuffer);
		}



		/*---- Instance fields ----*/

		/** The mode indicator of this segment. Not {@code null}. */
		public Mode mode;

		/** The length of this segment's unencoded data. Measured in characters for
		 * numeric/alphanumeric/kanji mode, bytes for byte mode, and 0 for ECI mode.
		 * Always zero or positive. Not the same as the data's bit length. */
		public int numChars;

		// The data bits of this segment. Not null. Accessed through getData().
		BitBuffer data;


		/*---- Constructor (low level) ----*/

		/**
		 * Constructs a QR Code segment with the specified attributes and data.
		 * The character count (numCh) must agree with the mode and the bit buffer length,
		 * but the constraint isn't checked. The specified bit buffer is cloned and stored.
		 * @param md the mode (not {@code null})
		 * @param numCh the data length in characters or bytes, which is non-negative
		 * @param data the data bits (not {@code null})
		 * @throws NullPointerException if the mode or data is {@code null}
		 * @throws IllegalArgumentException if the character count is negative
		 */
		public QRSegment(Mode mode, int numChars, BitBuffer data)
		{
			this.mode = mode;
			if (numChars < 0)
			{
				throw new ArgumentException("Invalid value");
			}
			this.numChars = numChars;
			this.data = new BitBuffer(data);  // Make defensive copy
		}


		/*---- Methods ----*/

		/**
		 * Returns the data bits of this segment.
		 * @return a new copy of the data bits (not {@code null})
		 */
		public BitBuffer GetData()
		{
			return data;  // Make defensive copy
		}


		// Calculates the number of bits needed to encode the given segments at the given version.
		// Returns a non-negative number if successful. Otherwise returns -1 if a segment has too
		// many characters to fit its length field, or the total bits exceeds Integer.MAX_VALUE.
		public static int GetTotalBits(IReadOnlyList<QRSegment> segments, int version)
		{
			long result = 0;
			foreach (var segment in segments)
			{
				int ccbits = segment.mode.NumCharCountBits(version);
				if (segment.numChars >= (1 << ccbits))
				{
					return -1;  // The segment's length doesn't fit the field's bit width
				}
				result += 4L + ccbits + segment.data.BitLength;
				if (result > int.MaxValue)
				{
					return -1;  // The sum will overflow an int type
				}
			}
			return (int)result;
		}


		/*---- Constants ----*/

		/** Describes precisely all strings that are encodable in numeric mode. To test whether a
		 * string {@code s} is encodable: {@code boolean ok = NUMERIC_REGEX.matcher(s).matches();}.
		 * A string is encodable iff each character is in the range 0 to 9.
		 * @see #makeNumeric(String) */
		public static readonly Regex NumericRegex = new Regex("[0-9]*");

		/** Describes precisely all strings that are encodable in alphanumeric mode. To test whether a
		 * string {@code s} is encodable: {@code boolean ok = ALPHANUMERIC_REGEX.matcher(s).matches();}.
		 * A string is encodable iff each character is in the following set: 0 to 9, A to Z
		 * (uppercase only), space, dollar, percent, asterisk, plus, hyphen, period, slash, colon.
		 * @see #makeAlphanumeric(String) */
		public static readonly Regex AlphaNumericRegex = new Regex("[A-Z0-9 $%*+./:-]*");

		// The set of all legal characters in alphanumeric mode, where
		// each character value maps to the index in the string.
		static readonly string AlphaNumericCharSet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";



		/*---- Public helper enumeration ----*/

		/**
		 * Describes how a segment's data bits are interpreted.
		 */
		public class Mode
		{

			/*-- Constants --*/

			public static readonly Mode Numeric = new Mode(0x1, 10, 12, 14);
			public static readonly Mode AlphaNumeric = new Mode(0x2, 9, 11, 13);
			public static readonly Mode Byte = new Mode(0x4, 8, 16, 16);
			public static readonly Mode Kanji = new Mode(0x8, 8, 10, 12);
			public static readonly Mode ECI = new Mode(0x7, 0, 0, 0);


			/*-- Fields --*/

			// The mode indicator bits, which is a uint4 value (range 0 to 15).
			public int ModeBits { get; private set; }

			// Number of character count bits for three different version ranges.
			private int[] numBitsCharCount = new int[3];


			/*-- Constructor --*/

			private Mode(int mode, int ccbits0, int ccbits1, int ccbits2)
			{
				ModeBits = mode;
				numBitsCharCount[0] = ccbits0;
				numBitsCharCount[1] = ccbits1;
				numBitsCharCount[2] = ccbits2;
			}


			/*-- Method --*/

			// Returns the bit width of the character count field for a segment in this mode
			// in a QR Code at the given version number. The result is in the range [0, 16].
			public int NumCharCountBits(int ver)
			{
				Debug.Assert(QRCode.MinVersion <= ver && ver <= QRCode.MaxVersion);
				return numBitsCharCount[(ver + 7) / 17];
			}

		}

	}
}
