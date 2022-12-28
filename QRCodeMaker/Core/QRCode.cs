using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using Ksnm.ExtensionMethods.System.Enum;

namespace QRCodeMaker.Core
{

	/**
	 * A QR Code symbol, which is a type of two-dimension barcode.
	 * Invented by Denso Wave and described in the ISO/IEC 18004 standard.
	 * <p>Instances of this class represent an immutable square grid of black and white cells.
	 * The class provides static factory functions to create a QR Code from text or binary data.
	 * The class covers the QR Code Model 2 specification, supporting all versions (sizes)
	 * from 1 to 40, all 4 error correction levels, and 4 character encoding modes.</p>
	 * <p>Ways to create a QR Code object:</p>
	 * <ul>
	 *   <li><p>High level: Take the payload data and call {@link QrCode#encodeText(String,Ecc)}
	 *     or {@link QrCode#encodeBinary(byte[],Ecc)}.</p></li>
	 *   <li><p>Mid level: Custom-make the list of {@link QrSegment segments}
	 *     and call {@link QrCode#encodeSegments(List,Ecc)} or
	 *     {@link QrCode#encodeSegments(List,Ecc,int,int,int,boolean)}</p></li>
	 *   <li><p>Low level: Custom-make the array of data codeword bytes (including segment headers and
	 *     final padding, excluding error correction codewords), supply the appropriate version number,
	 *     and call the {@link QrCode#QrCode(int,Ecc,byte[],int) constructor}.</p></li>
	 * </ul>
	 * <p>(Note that all ways require supplying the desired error correction level.)</p>
	 * @see QrSegment
	 */
	public class QRCode
	{

		#region Static factory functions (high level)

		/**
		 * Returns a QR Code representing the specified Unicode text string at the specified error correction level.
		 * As a conservative upper bound, this function is guaranteed to succeed for strings that have 738 or fewer
		 * Unicode code points (not UTF-16 code units) if the low error correction level is used. The smallest possible
		 * QR Code version is automatically chosen for the output. The ECC level of the result may be higher than the
		 * ecl argument if it can be done without increasing the version.
		 * @param text the text to be encoded (not {@code null}), which can be any Unicode string
		 * @param ecl the error correction level to use (not {@code null}) (boostable)
		 * @return a QR Code (not {@code null}) representing the text
		 * @throws NullPointerException if the text or error correction level is {@code null}
		 * @throws DataTooLongException if the text fails to fit in the
		 * largest version QR Code at the ECL, which means it is too long
		 */
		public static QRCode EncodeText(String text, ErrorCorrectionLevel ecl)
		{
			var segs = QRSegment.MakeSegments(text);
			return EncodeSegments(segs, ecl);
		}


		/**
		 * Returns a QR Code representing the specified binary data at the specified error correction level.
		 * This function always encodes using the binary segment mode, not any text mode. The maximum number of
		 * bytes allowed is 2953. The smallest possible QR Code version is automatically chosen for the output.
		 * The ECC level of the result may be higher than the ecl argument if it can be done without increasing the version.
		 * @param data the binary data to encode (not {@code null})
		 * @param ecl the error correction level to use (not {@code null}) (boostable)
		 * @return a QR Code (not {@code null}) representing the data
		 * @throws NullPointerException if the data or error correction level is {@code null}
		 * @throws DataTooLongException if the data fails to fit in the
		 * largest version QR Code at the ECL, which means it is too long
		 */
		public static QRCode EncodeBinary(IReadOnlyList<byte> data, ErrorCorrectionLevel ecl, int minVersion, int maxVersion, int mask, bool boostEcl)
		{
			var seg = QRSegment.MakeBytes(data);
			return EncodeSegments(new QRSegment[] { seg }, ecl, minVersion, maxVersion, mask, boostEcl);
		}


		/*---- Static factory functions (mid level) ----*/

		/**
		 * Returns a QR Code representing the specified segments at the specified error correction
		 * level. The smallest possible QR Code version is automatically chosen for the output. The ECC level
		 * of the result may be higher than the ecl argument if it can be done without increasing the version.
		 * <p>This function allows the user to create a custom sequence of segments that switches
		 * between modes (such as alphanumeric and byte) to encode text in less space.
		 * This is a mid-level API; the high-level API is {@link #encodeText(String,Ecc)}
		 * and {@link #encodeBinary(byte[],Ecc)}.</p>
		 * @param segs the segments to encode
		 * @param ecl the error correction level to use (not {@code null}) (boostable)
		 * @return a QR Code (not {@code null}) representing the segments
		 * @throws NullPointerException if the list of segments, any segment, or the error correction level is {@code null}
		 * @throws DataTooLongException if the segments fail to fit in the
		 * largest version QR Code at the ECL, which means they are too long
		 */
		public static QRCode EncodeSegments(IReadOnlyList<QRSegment> segs, ErrorCorrectionLevel ecl)
		{
			return EncodeSegments(segs, ecl, MinVersion, MaxVersion, AutoMask, true);
		}


		/**
		 * Returns a QR Code representing the specified segments with the specified encoding parameters.
		 * The smallest possible QR Code version within the specified range is automatically
		 * chosen for the output. Iff boostEcl is {@code true}, then the ECC level of the
		 * result may be higher than the ecl argument if it can be done without increasing
		 * the version. The mask number is either between 0 to 7 (inclusive) to force that
		 * mask, or &#x2212;1 to automatically choose an appropriate mask (which may be slow).
		 * <p>This function allows the user to create a custom sequence of segments that switches
		 * between modes (such as alphanumeric and byte) to encode text in less space.
		 * This is a mid-level API; the high-level API is {@link #encodeText(String,Ecc)}
		 * and {@link #encodeBinary(byte[],Ecc)}.</p>
		 * @param segs the segments to encode
		 * @param ecl the error correction level to use (not {@code null}) (boostable)
		 * @param minVersion the minimum allowed version of the QR Code (at least 1)
		 * @param maxVersion the maximum allowed version of the QR Code (at most 40)
		 * @param mask the mask number to use (between 0 and 7 (inclusive)), or &#x2212;1 for automatic mask
		 * @param boostEcl increases the ECC level as long as it doesn't increase the version number
		 * @return a QR Code (not {@code null}) representing the segments
		 * @throws NullPointerException if the list of segments, any segment, or the error correction level is {@code null}
		 * @throws IllegalArgumentException if 1 &#x2264; minVersion &#x2264; maxVersion &#x2264; 40
		 * or &#x2212;1 &#x2264; mask &#x2264; 7 is violated
		 * @throws DataTooLongException if the segments fail to fit in
		 * the maxVersion QR Code at the ECL, which means they are too long
		 */
		public static QRCode EncodeSegments(IReadOnlyList<QRSegment> segs, ErrorCorrectionLevel ecl, int minVersion, int maxVersion, int mask, bool boostEcl)
		{
			//Objects.requireNonNull(segs);
			//Objects.requireNonNull(ecl);
			if (!(MinVersion <= minVersion && minVersion <= maxVersion && maxVersion <= MaxVersion) || mask < -1 || mask > 7)
			{
				throw new Exception("Invalid value");
				//throw new IllegalArgumentException("Invalid value");
			}

			// Find the minimal version number to use
			// 使用する最小バージョンの数字を探す
			int version, dataUsedBits;
			for (version = minVersion; ; version++)
			{
				int dataCapacityBits = GetNumDataCodewords(version, ecl) * 8;// Number of data bits available
				dataUsedBits = QRSegment.GetTotalBits(segs, version);
				if (dataUsedBits != -1 && dataUsedBits <= dataCapacityBits)
				{
					break;  // This version number is found to be suitable
				}
				if (version >= maxVersion)
				{
					// この範囲のすべてのバージョンは、与えられたデータに適合しない。
					// All versions in the range could not fit the given data
					string msg = "Segment too long";
					if (dataUsedBits != -1)
					{
						msg = string.Format("Data length = {0} bits, Max capacity = {1} bits", dataUsedBits, dataCapacityBits);
					}
					throw new DataTooLongException(msg);
				}
			}
			Debug.Assert(dataUsedBits != -1);

			// Increase the error correction level while the data still fits in the current version number
			// データが現在のバージョン番号に収まっている間に、エラー補正レベルを上げる
			foreach (var newEcl in (ErrorCorrectionLevel[])System.Enum.GetValues(typeof(ErrorCorrectionLevel)))
			{
				// From low to high
				// ローからハイへ
				if (boostEcl && dataUsedBits <= GetNumDataCodewords(version, newEcl) * 8)
				{
					ecl = newEcl;
				}
			}

			// Concatenate all segments to create the data bit string
			// すべてのセグメントを連結してデータビット文字列を作成します。
			var bitBuffer = new BitBuffer();
			foreach (var seg in segs)
			{
				bitBuffer.AppendBits(seg.mode.ModeBits, 4);
				bitBuffer.AppendBits(seg.numChars, seg.mode.NumCharCountBits(version));
				bitBuffer.AppendData(seg.GetData());
			}
			Debug.Assert(bitBuffer.BitLength == dataUsedBits);

			// Add terminator and pad up to a byte if applicable
			// ターミネータを追加し、該当する場合はバイトまでパッドアップします。
			{
				int dataCapacityBits = GetNumDataCodewords(version, ecl) * 8;
				Debug.Assert(bitBuffer.BitLength <= dataCapacityBits);
				bitBuffer.AppendBits(0, Math.Min(4, dataCapacityBits - bitBuffer.BitLength));
				bitBuffer.AppendBits(0, (8 - bitBuffer.BitLength % 8) % 8);
				Debug.Assert(bitBuffer.BitLength % 8 == 0);

				// Pad with alternating bytes until data capacity is reached
				// データ容量に達するまでバイトを交互に配置したパッド
				for (int padByte = 0xEC; bitBuffer.BitLength < dataCapacityBits; padByte ^= 0xEC ^ 0x11)
				{
					bitBuffer.AppendBits(padByte, 8);
				}

				// Pack bits into bytes in big endian
				// ビッグエンディアンでビットをバイトに詰め込む
				byte[] dataCodewords = new byte[bitBuffer.BitLength / 8];
				for (int i = 0; i < bitBuffer.BitLength; i++)
				{
					dataCodewords[(uint)i >> 3] |= (byte)(bitBuffer.GetBit(i) << (7 - (i & 7)));
				}

				// Create the QR Code object
				return new QRCode(version, ecl, dataCodewords, mask);
			}
		}

		#endregion Static factory functions (high level)

		#region Instance fields

		// Public immutable scalar parameters:

		/** The version number of this QR Code, which is between 1 and 40 (inclusive).
		 * This determines the size of this barcode. */
		public int Version;

		/** The width and height of this QR Code, measured in modules, between
		 * 21 and 177 (inclusive). This is equal to version &#xD7; 4 + 17. */
		public int Size;

		/** The error correction level used in this QR Code, which is not {@code null}. */
		public ErrorCorrectionLevel errorCorrectionLevel;

		/** The index of the mask pattern used in this QR Code, which is between 0 and 7 (inclusive).
		 * <p>Even if a QR Code is created with automatic masking requested (mask =
		 * &#x2212;1), the resulting object still has a mask value between 0 and 7. */
		public int Mask;

		public byte[] DataCodewords;

		// Private grids of modules/pixels, with dimensions of size*size:

		/// <summary>
		/// このQRコードのモジュール（false=白、true=黒）。
		/// コンストラクタ終了後は不変。GetModule()でアクセスできます。
		/// The modules of this QR Code (false = white, true = black).
		/// Immutable after constructor finishes. Accessed through getModule().
		/// </summary>
		private bool[,] modules;

		/// <summary>
		/// モジュールの種類
		/// </summary>
		private ModuleType[,] moduleTypes;

		/// <summary>
		/// マスキングの対象とならない関数モジュールを示します。コンストラクタが終了すると破棄されます。
		/// Indicates function modules that are not subjected to masking. Discarded when constructor finishes.
		/// </summary>
		private bool[,] isFunction;

		#endregion Instance fields

		/*---- Constructor (low level) ----*/

		/**
		 * Constructs a QR Code with the specified version number,
		 * error correction level, data codeword bytes, and mask number.
		 * <p>This is a low-level API that most users should not use directly. A mid-level
		 * API is the {@link #encodeSegments(List,Ecc,int,int,int,boolean)} function.</p>
		 * @param ver the version number to use, which must be in the range 1 to 40 (inclusive)
		 * @param ecl the error correction level to use
		 * @param dataCodewords the bytes representing segments to encode (without ECC)
		 * @param msk the mask pattern to use, which is either &#x2212;1 for automatic choice or from 0 to 7 for fixed choice
		 * @throws NullPointerException if the byte array or error correction level is {@code null}
		 * @throws IllegalArgumentException if the version or mask value is out of range,
		 * or if the data is the wrong length for the specified version and error correction level
		 */
		public QRCode(int version, ErrorCorrectionLevel ecl, byte[] dataCodewords, int mask)
		{
			// Check arguments and initialize fields
			if (version < MinVersion || version > MaxVersion)
			{
				throw new ArgumentOutOfRangeException($"Version value out of range. version={version}");
			}
			if (mask < -1 || mask > 7)
			{
				throw new ArgumentOutOfRangeException($"Mask value out of range. mask={mask}");
			}
			this.Version = version;
			Size = version * 4 + 17;
			errorCorrectionLevel = ecl;
			this.DataCodewords = dataCodewords;

			modules = new bool[Size, Size];  // Initially all white
			moduleTypes = new ModuleType[Size, Size];
			isFunction = new bool[Size, Size];

			// Compute ECC, draw modules, do masking
			DrawFunctionPatterns();
			var allCodewords = AddEccAndInterleave(dataCodewords);
			DrawCodewords(allCodewords);
			this.Mask = HandleConstructorMasking(mask);
			//isFunction = null;// 消さずに残す
		}

		/*---- Public instance methods ----*/

		/**
		 * Returns the color of the module (pixel) at the specified coordinates, which is {@code false}
		 * for white or {@code true} for black. The top left corner has the coordinates (x=0, y=0).
		 * If the specified coordinates are out of bounds, then {@code false} (white) is returned.
		 * @param x the x coordinate, where 0 is the left edge and size&#x2212;1 is the right edge
		 * @param y the y coordinate, where 0 is the top edge and size&#x2212;1 is the bottom edge
		 * @return {@code true} if the coordinates are in bounds and the module
		 * at that location is black, or {@code false} (white) otherwise
		 */
		public bool GetModule(int x, int y)
		{
			if (0 <= x && x < Size && 0 <= y && y < Size)
			{
				return modules[y, x];
			}
			throw new ArgumentOutOfRangeException($"x={x} y={y}");
		}

		public ModuleType GetModuleType(int x, int y)
		{
			if (0 <= x && x < Size && 0 <= y && y < Size)
			{
				return moduleTypes[y, x];
			}
			throw new ArgumentOutOfRangeException($"x={x} y={y}");
		}

		public bool IsFunction(int x, int y)
		{
			if (0 <= x && x < Size && 0 <= y && y < Size)
			{
				return isFunction[y, x];
			}
			throw new ArgumentOutOfRangeException($"x={x} y={y}");
		}

		/**
		 * Returns a raster image depicting this QR Code, with the specified module scale and border modules.
		 * <p>For example, toImage(scale=10, border=4) means to pad the QR Code with 4 white
		 * border modules on all four sides, and use 10&#xD7;10 pixels to represent each module.
		 * The resulting image only contains the hex colors 000000 and FFFFFF.
		 * @param scale the side length (measured in pixels, must be positive) of each module
		 * @param border the number of border modules to add, which must be non-negative
		 * @return a new image representing this QR Code, with padding and scaling
		 * @throws IllegalArgumentException if the scale or border is out of range, or if
		 * {scale, border, size} cause the image dimensions to exceed Integer.MAX_VALUE
		 */
		public Bitmap ToImage(int scale, int border)
		{
			if (scale <= 0 || border < 0)
			{
				throw new ArgumentException("Value out of range");
			}
			if (border > int.MaxValue / 2 || Size + border * 2L > int.MaxValue / scale)
			{
				throw new ArgumentException("Scale or border too large");
			}

			//描画先とするImageオブジェクトを作成する
			var result = new Bitmap((Size + border * 2) * scale, (Size + border * 2) * scale);
			var graphics = Graphics.FromImage(result);

			for (int y = 0; y < result.Height; y++)
			{
				for (int x = 0; x < result.Width; x++)
				{
					bool color = GetModule(x / scale - border, y / scale - border);
					result.SetPixel(x, y, color ? Color.Black : Color.White);
				}
			}
			return result;
		}
		/// <summary>
		/// モジュールの形状
		/// </summary>
		public enum ModuleShape
		{
			/// <summary>
			/// 四角
			/// </summary>
			Square,
			/// <summary>
			/// 丸
			/// </summary>
			Circle,
		}
		/// <summary>
		/// 丸を敷き詰めたQRコード画像を生成
		/// </summary>
		/// <param name="moduleSize">丸の間隔[直径/ピクセル]</param>
		/// <param name="circleScale">丸のスケール[比率]</param>
		/// <param name="border">画像の外側の余白[moduleSize]</param>
		/// <returns></returns>
		public Bitmap ToImage(ModuleShape moduleShape, int moduleSize, float circleScale, int border,Color color)
		{
			if (moduleSize <= 0 || border < 0)
			{
				throw new ArgumentException("Value out of range");
			}
			if (border > int.MaxValue / 2 || Size + border * 2L > int.MaxValue / moduleSize)
			{
				throw new ArgumentException("Scale or border too large");
			}

			// 描画先とするImageオブジェクトを作成する
			var result = new Bitmap((Size + border * 2) * moduleSize, (Size + border * 2) * moduleSize);
			var graphics = Graphics.FromImage(result);
			var scaledSize = moduleSize * circleScale;

			// 周囲を描画
			{
				Brush brush = Brushes.White;
				var borderSize = moduleSize * border;
				graphics.FillRectangle(brush, 0, 0, result.Width, borderSize);
				graphics.FillRectangle(brush, 0, 0, borderSize, result.Height);
				graphics.FillRectangle(brush, 0, result.Height - borderSize, result.Width, borderSize);
				graphics.FillRectangle(brush, result.Width - borderSize, 0, borderSize, result.Height);
			}

			// 中央に調整するためのオフセット
			var offset = (moduleSize - scaledSize) / 2;

			// QRコードを描画
			for (int yI = 0; yI < Size; yI++)
			{
				for (int xI = 0; xI < Size; xI++)
				{
					// 位置
					var x = ((border + xI) * moduleSize);
					var y = ((border + yI) * moduleSize);
					// 色
					bool module = GetModule(xI, yI);
					Brush brush = Brushes.Black;
					if (module)
					{
						brush = new SolidBrush(color);
					}
					else
					{
						brush = Brushes.White;
					}
					// 機能ごとに形状を変更
					//bool isFunction = IsFunction(xI, yI);
					var moduleType = GetModuleType(xI, yI);
					var isFunction = moduleType.Any(ModuleType.FinderPattern,ModuleType.AlignmentPattern);
					if (isFunction)
					{
						graphics.FillRectangle(brush, x, y, moduleSize, moduleSize);
					}
					else
					{
						if(moduleShape == ModuleShape.Square)
						{
							graphics.FillRectangle(brush, x + offset, y + offset, scaledSize, scaledSize);
						}
						else if (moduleShape == ModuleShape.Circle)
						{
							graphics.FillEllipse(brush, x + offset, y + offset, scaledSize, scaledSize);
						}
					}
				}
			}
			return result;
		}


		/**
		 * Returns a string of SVG code for an image depicting this QR Code, with the specified number
		 * of border modules. The string always uses Unix newlines (\n), regardless of the platform.
		 * @param border the number of border modules to add, which must be non-negative
		 * @return a string representing this QR Code as an SVG XML document
		 * @throws IllegalArgumentException if the border is negative
		 */
		public string ToSvgString(int border)
		{
			if (border < 0)
			{
				throw new Exception("Border must be non-negative");
				//throw new IllegalArgumentException("Border must be non-negative");
			}
			long brd = border;
			var stringBuilder = new StringBuilder();
			stringBuilder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n")
			.Append("<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.1//EN\" \"http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd\">\n")
			.Append(String.Format("<svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" viewBox=\"0 0 {0} {0}\" stroke=\"none\">\n", Size + brd * 2))
			.Append("\t<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>\n")
			.Append("\t<path d=\"");
			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					if (GetModule(x, y))
					{
						if (x != 0 || y != 0)
						{
							stringBuilder.Append(" ");
						}
						stringBuilder.Append(string.Format("M{0},{1}h1v1h-1z", x + brd, y + brd));
					}
				}
			}
			return stringBuilder
				.Append("\" fill=\"#000000\"/>\n")
				.Append("</svg>\n")
				.ToString();
		}



		/*---- Private helper methods for constructor: Drawing function modules ----*/

		// Reads this object's version field, and draws and marks all function modules.
		private void DrawFunctionPatterns()
		{
			// 水平・垂直方向のタイミングパターンを描く
			// Draw horizontal and vertical timing patterns
			for (int i = 0; i < Size; i++)
			{
				SetFunctionModule(6, i, i % 2 == 0, ModuleType.VerticalTimingPattern);
				SetFunctionModule(i, 6, i % 2 == 0, ModuleType.HorizonTimingPattern);
			}

			// Draw 3 finder patterns (all corners except bottom right; overwrites some timing modules)
			DrawFinderPattern(3, 3);
			DrawFinderPattern(Size - 4, 3);
			DrawFinderPattern(3, Size - 4);

			// Draw numerous alignment patterns
			int[] alignPatPos = GetAlignmentPatternPositions();
			int numAlign = alignPatPos.Length;
			for (int i = 0; i < numAlign; i++)
			{
				for (int j = 0; j < numAlign; j++)
				{
					// Don't draw on the three finder corners
					if (!(i == 0 && j == 0 || i == 0 && j == numAlign - 1 || i == numAlign - 1 && j == 0))
						DrawAlignmentPattern(alignPatPos[i], alignPatPos[j]);
				}
			}

			// Draw configuration data
			DrawFormatBits(0);  // Dummy mask value; overwritten later in the constructor
			DrawVersion();
		}


		// Draws two copies of the format bits (with its own error correction code)
		// based on the given mask and this object's error correction level field.
		private void DrawFormatBits(int mask)
		{
			// Calculate error correction code and pack bits
			int data = GetFormatBits(errorCorrectionLevel) << 3 | mask;  // errCorrLvl is uint2, mask is uint3
			int rem = data;
			for (int i = 0; i < 10; i++)
			{
				rem = (int)((rem << 1) ^ (((uint)rem >> 9) * 0x537));
			}
			int bits = (data << 10 | rem) ^ 0x5412;  // uint15
			Debug.Assert((uint)bits >> 15 == 0);

			// Draw first copy
			for (int i = 0; i <= 5; i++)
			{
				SetFunctionModule(8, i, GetBit(bits, i), ModuleType.Format);
			}
			SetFunctionModule(8, 7, GetBit(bits, 6), ModuleType.Format);
			SetFunctionModule(8, 8, GetBit(bits, 7), ModuleType.Format);
			SetFunctionModule(7, 8, GetBit(bits, 8), ModuleType.Format);
			for (int i = 9; i < 15; i++)
			{
				SetFunctionModule(14 - i, 8, GetBit(bits, i), ModuleType.Format);
			}

			// Draw second copy
			for (int i = 0; i < 8; i++)
			{
				SetFunctionModule(Size - 1 - i, 8, GetBit(bits, i), ModuleType.Format);
			}
			for (int i = 8; i < 15; i++)
			{
				SetFunctionModule(8, Size - 15 + i, GetBit(bits, i), ModuleType.Format);
			}
			SetFunctionModule(8, Size - 8, true, ModuleType.Format);  // Always black
		}


		// Draws two copies of the version bits (with its own error correction code),
		// based on this object's version field, iff 7 <= version <= 40.
		private void DrawVersion()
		{
			if (Version < 7)
			{
				return;
			}

			// Calculate error correction code and pack bits
			int rem = Version;  // version is uint6, in the range [7, 40]
			for (int i = 0; i < 12; i++)
			{
				rem = (int)((rem << 1) ^ (((uint)rem >> 11) * 0x1F25));
			}
			int bits = Version << 12 | rem;  // uint18
			Debug.Assert((uint)bits >> 18 == 0);

			// Draw two copies
			for (int i = 0; i < 18; i++)
			{
				bool bit = GetBit(bits, i);
				int a = Size - 11 + i % 3;
				int b = i / 3;
				SetFunctionModule(a, b, bit, ModuleType.Version);
				SetFunctionModule(b, a, bit, ModuleType.Version);
			}
		}


		// Draws a 9*9 finder pattern including the border separator,
		// with the center module at (x, y). Modules can be out of bounds.
		private void DrawFinderPattern(int x, int y)
		{
			for (int dy = -4; dy <= 4; dy++)
			{
				for (int dx = -4; dx <= 4; dx++)
				{
					int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));  // Chebyshev/infinity norm
					int xx = x + dx, yy = y + dy;
					if (0 <= xx && xx < Size && 0 <= yy && yy < Size)
					{
						SetFunctionModule(xx, yy, dist != 2 && dist != 4, ModuleType.FinderPattern);
					}
				}
			}
		}

		/// <summary>
		/// 中央のモジュールを(x, y)とした、5*5の位置合わせパターンを描画します。
		/// すべてのモジュールは境界内になければならない。
		/// Draws a 5*5 alignment pattern, with the center module
		/// at (x, y). All modules must be in bounds.</summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		private void DrawAlignmentPattern(int x, int y)
		{
			for (int dy = -2; dy <= 2; dy++)
			{
				for (int dx = -2; dx <= 2; dx++)
				{
					SetFunctionModule(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1, ModuleType.AlignmentPattern);
				}
			}
		}

		/// <summary>
		/// Sets the color of a module and marks it as a function module.
		/// Only used by the constructor. Coordinates must be in bounds.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="isBlack"></param>
		private void SetFunctionModule(int x, int y, bool isBlack, ModuleType moduleType)
		{
			modules[y, x] = isBlack;
			moduleTypes[y, x] = moduleType;
			isFunction[y, x] = true;
		}


		/*---- Private helper methods for constructor: Codewords and masking ----*/

		// Returns a new byte string representing the given data with the appropriate error correction
		// codewords appended to it, based on this object's version and error correction level.
		/// <summary>
		/// 与えられたデータを表す新しいバイト文字列を、適切なエラー訂正とともに返します。
		/// このオブジェクトのバージョンとエラー訂正レベルに基づいて、それに追加されたコードワード。
		/// </summary>
		/// <param name="data"></param>
		/// <returns></returns>
		private byte[] AddEccAndInterleave(byte[] data)
		{
			if (data.Length != GetNumDataCodewords(Version, errorCorrectionLevel))
			{
				throw new Exception();
				//throw new IllegalArgumentException();
			}

			// Calculate parameter numbers
			int numBlocks = NumErrorCorrectionBlocks[(int)errorCorrectionLevel][Version];
			int blockEccLen = EccCodeWordsPerBlock[(int)errorCorrectionLevel][Version];
			int rawCodewords = GetNumRawDataModules(Version) / 8;
			int numShortBlocks = numBlocks - rawCodewords % numBlocks;
			int shortBlockLen = rawCodewords / numBlocks;

			// Split data into blocks and append ECC to each block
			var blocks = new byte[numBlocks][];
			var rsDiv = ReedSolomonComputeDivisor(blockEccLen);
			for (int i = 0, k = 0; i < numBlocks; i++)
			{
				var dat = new byte[shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1)];
				Array.Copy(data, k, dat, 0, dat.Length);
				k += dat.Length;

				var block = new byte[shortBlockLen + 1];
				Array.Copy(dat, block, dat.Length);

				byte[] ecc = ReedSolomonComputeRemainder(dat, rsDiv);
				Array.Copy(ecc, 0, block, block.Length - blockEccLen, ecc.Length);
				blocks[i] = block;
			}

			// Interleave (not concatenate) the bytes from every block into a single sequence
			var result = new byte[rawCodewords];
			for (int i = 0, k = 0; i < blocks[0].Length; i++)
			{
				for (int j = 0; j < blocks.Length; j++)
				{
					// Skip the padding byte in short blocks
					if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
					{
						result[k] = blocks[j][i];
						k++;
					}
				}
			}
			return result;
		}


		// Draws the given sequence of 8-bit codewords (data and error correction) onto the entire
		// data area of this QR Code. Function modules need to be marked off before this is called.
		private void DrawCodewords(byte[] data)
		{
			if (data.Length != GetNumRawDataModules(Version) / 8)
			{
				throw new Exception();
				//throw new IllegalArgumentException();
			}

			int i = 0;  // Bit index into the data
						// Do the funny zigzag scan
			for (int right = Size - 1; right >= 1; right -= 2)
			{
				// Index of right column in each column pair
				if (right == 6)
				{
					right = 5;
				}
				for (int vert = 0; vert < Size; vert++)
				{
					// Vertical counter
					for (int j = 0; j < 2; j++)
					{
						int x = right - j;  // Actual x coordinate
						bool upward = ((right + 1) & 2) == 0;
						int y = upward ? Size - 1 - vert : vert;  // Actual y coordinate
						if (!isFunction[y, x] && i < data.Length * 8)
						{
							modules[y, x] = GetBit(data[i >> 3], 7 - (i & 7));
							i++;
						}
						// If this QR Code has any remainder bits (0 to 7), they were assigned as
						// 0/false/white by the constructor and are left unchanged by this method
					}
				}
			}
			Debug.Assert(i == data.Length * 8);
		}


		// XORs the codeword modules in this QR Code with the given mask pattern.
		// The function modules must be marked and the codeword bits must be drawn
		// before masking. Due to the arithmetic of XOR, calling applyMask() with
		// the same mask value a second time will undo the mask. A final well-formed
		// QR Code needs exactly one (not zero, two, etc.) mask applied.
		private void ApplyMask(int mask)
		{
			if (mask < 0 || mask > 7)
			{
				throw new ArgumentOutOfRangeException("Mask value out of range");
			}
			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					bool invert;
					switch (mask)
					{
						case 0: invert = (x + y) % 2 == 0; break;
						case 1: invert = y % 2 == 0; break;
						case 2: invert = x % 3 == 0; break;
						case 3: invert = (x + y) % 3 == 0; break;
						case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
						case 5: invert = x * y % 2 + x * y % 3 == 0; break;
						case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
						case 7: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
						default:
							throw new Exception();
							//throw new AssertionError();
					}
					modules[y, x] ^= invert & !isFunction[y, x];
				}
			}
		}


		// A messy helper function for the constructor. This QR Code must be in an unmasked state when this
		// method is called. The given argument is the requested mask, which is -1 for auto or 0 to 7 for fixed.
		// This method applies and returns the actual mask chosen, from 0 to 7.
		/// <summary>
		/// このメソッドを呼び出す際には、このQRコードはマスクされていない状態でなければなりません。
		/// </summary>
		/// <param name="mask">0～7、-1なら自動</param>
		/// <returns></returns>
		public int HandleConstructorMasking(int mask)
		{
			if (mask == AutoMask)
			{
				// Automatically choose best mask
				int minPenalty = int.MaxValue;
				for (int i = 0; i < 8; i++)
				{
					ApplyMask(i);
					DrawFormatBits(i);
					int penalty = GetPenaltyScore();
					if (penalty < minPenalty)
					{
						mask = i;
						minPenalty = penalty;
					}
					ApplyMask(i);  // Undoes the mask due to XOR
				}
			}
			Debug.Assert(0 <= mask && mask <= 7);
			ApplyMask(mask);  // Apply the final choice of mask
			DrawFormatBits(mask);  // Overwrite old format bits
			return mask;  // The caller shall assign this value to the final-declared field
		}

		/// <summary>
		/// このQRコードの現在のモジュールの状態から、ペナルティスコアを計算して返します。
		/// これは、自動マスク選択アルゴリズムが、最も低いスコアを得るマスクパターンを見つけるために使用されます。
		/// Calculates and returns the penalty score based on state of this QR Code's current modules.
		/// This is used by the automatic mask choice algorithm to find the mask pattern that yields the lowest score.
		/// </summary>
		/// <returns></returns>
		private int GetPenaltyScore()
		{
			int result = 0;

			// Adjacent modules in row having same color, and finder-like patterns
			for (int y = 0; y < Size; y++)
			{
				bool runColor = false;
				int runX = 0;
				int[] runHistory = new int[7];
				for (int x = 0; x < Size; x++)
				{
					if (modules[y, x] == runColor)
					{
						runX++;
						if (runX == 5)
						{
							result += PenaltyN1;
						}
						else if (runX > 5)
						{
							result++;
						}
					}
					else
					{
						FinderPenaltyAddHistory(runX, runHistory);
						if (!runColor)
						{
							result += FinderPenaltyCountPatterns(runHistory) * PenaltyN3;
						}
						runColor = modules[y, x];
						runX = 1;
					}
				}
				result += FinderPenaltyTerminateAndCount(runColor, runX, runHistory) * PenaltyN3;
			}
			// Adjacent modules in column having same color, and finder-like patterns
			for (int x = 0; x < Size; x++)
			{
				bool runColor = false;
				int runY = 0;
				int[] runHistory = new int[7];
				for (int y = 0; y < Size; y++)
				{
					if (modules[y, x] == runColor)
					{
						runY++;
						if (runY == 5)
							result += PenaltyN1;
						else if (runY > 5)
							result++;
					}
					else
					{
						FinderPenaltyAddHistory(runY, runHistory);
						if (!runColor)
							result += FinderPenaltyCountPatterns(runHistory) * PenaltyN3;
						runColor = modules[y, x];
						runY = 1;
					}
				}
				result += FinderPenaltyTerminateAndCount(runColor, runY, runHistory) * PenaltyN3;
			}

			// 2*2 blocks of modules having same color
			for (int y = 0; y < Size - 1; y++)
			{
				for (int x = 0; x < Size - 1; x++)
				{
					bool color = modules[y, x];
					if (color == modules[y, x + 1] &&
						  color == modules[y + 1, x] &&
						  color == modules[y + 1, x + 1])
						result += PenaltyN2;
				}
			}

			// Balance of black and white modules
			int black = 0;
			foreach (var color in modules)
			{
				if (color)
				{
					black++;
				}
			}
			int total = Size * Size;  // Note that size is odd, so black/total != 1/2
									  // Compute the smallest integer k >= 0 such that (45-5k)% <= black/total <= (55+5k)%
			int k = (Math.Abs(black * 20 - total * 10) + total - 1) / total - 1;
			result += k * PenaltyN4;
			return result;
		}

		/// <summary>
		/// ビットマップを設定する
		/// 不透明度が0なら何もしません
		/// 赤色が128以上なら白(false)、128未満なら黒(true)を設定します。
		/// </summary>
		/// <param name="bitmap"></param>
		public void SetBitmap(Bitmap bitmap)
		{
			var width = Math.Min(bitmap.Width, Size);
			var height = Math.Min(bitmap.Height, Size);
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					var color = bitmap.GetPixel(x, y);
					if (color.A > 0)
					{
						modules[y, x] = color.R < 128;
					}
				}
			}
		}



		#region Private helper functions

		// Returns an ascending list of positions of alignment patterns for this version number.
		// Each position is in the range [0,177), and are used on both the x and y axes.
		// This could be implemented as lookup table of 40 variable-length lists of unsigned bytes.
		private int[] GetAlignmentPatternPositions()
		{
			if (Version == 1)
			{
				return new int[] { };
			}
			else
			{
				int numAlign = Version / 7 + 2;
				int step;
				if (Version == 32)  // Special snowflake
				{
					step = 26;
				}
				else  // step = ceil[(size - 13) / (numAlign*2 - 2)] * 2
				{
					step = (Version * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2;
				}
				int[] result = new int[numAlign];
				result[0] = 6;
				for (int i = result.Length - 1, pos = Size - 7; i >= 1; i--, pos -= step)
				{
					result[i] = pos;
				}
				return result;
			}
		}

		// Returns the number of data bits that can be stored in a QR Code of the given version number, after
		// all function modules are excluded. This includes remainder bits, so it might not be a multiple of 8.
		// The result is in the range [208, 29648]. This could be implemented as a 40-entry lookup table.
		static int GetNumRawDataModules(int ver)
		{
			if (ver < MinVersion || ver > MaxVersion)
			{
				throw new ArgumentOutOfRangeException("Version number out of range");
			}

			int size = ver * 4 + 17;
			int result = size * size;   // Number of modules in the whole QR Code square
			result -= 8 * 8 * 3;        // Subtract the three finders with separators
			result -= 15 * 2 + 1;       // Subtract the format information and black module
			result -= (size - 16) * 2;  // Subtract the timing patterns (excluding finders)
										// The five lines above are equivalent to: int result = (16 * ver + 128) * ver + 64;
			if (ver >= 2)
			{
				int numAlign = ver / 7 + 2;
				result -= (numAlign - 1) * (numAlign - 1) * 25;  // Subtract alignment patterns not overlapping with timing patterns
				result -= (numAlign - 2) * 2 * 20;  // Subtract alignment patterns that overlap with timing patterns
													// The two lines above are equivalent to: result -= (25 * numAlign - 10) * numAlign - 55;
				if (ver >= 7)
				{
					result -= 6 * 3 * 2;  // Subtract version information
				}
			}
			Debug.Assert(208 <= result && result <= 29648);
			return result;
		}



		/// <summary>
		/// Returns a Reed-Solomon ECC generator polynomial for the given degree. This could be
		/// implemented as a lookup table over all possible parameter values, instead of as an algorithm.
		/// 指定された次数のリード・ソロモンECC生成多項式を返します。これは
		/// アルゴリズムとしてではなく、すべての可能なパラメータ値のルックアップテーブルとして実装されています。
		/// </summary>
		/// <param name="degree">度合</param>
		/// <returns></returns>
		static byte[] ReedSolomonComputeDivisor(int degree)
		{
			if (degree < 1 || degree > 255)
			{
				throw new ArgumentOutOfRangeException("Degree out of range");
			}
			// Polynomial coefficients are stored from highest to lowest power, excluding the leading term which is always 1.
			// For example the polynomial x^3 + 255x^2 + 8x + 93 is stored as the uint8 array {255, 8, 93}.
			var result = new byte[degree];
			result[degree - 1] = 1;  // Start off with the monomial x^0

			// Compute the product polynomial (x - r^0) * (x - r^1) * (x - r^2) * ... * (x - r^{degree-1}),
			// and drop the highest monomial term which is always 1x^degree.
			// Note that r = 0x02, which is a generator element of this field GF(2^8/0x11D).
			int root = 1;
			for (int i = 0; i < degree; i++)
			{
				// Multiply the current product by (x - r^i)
				for (int j = 0; j < result.Length; j++)
				{
					result[j] = (byte)ReedSolomonMultiply(result[j] & 0xFF, root);
					if (j + 1 < result.Length)
					{
						result[j] ^= result[j + 1];
					}
				}
				root = ReedSolomonMultiply(root, 0x02);
			}
			return result;
		}

		/// <summary>
		/// Returns the Reed-Solomon error correction codeword for the given data and divisor polynomials.
		/// 与えられたデータと除数多項式に対するリード・ソロモンエラー訂正コードワードを返します。
		/// </summary>
		/// <param name="data">データ</param>
		/// <param name="divisor">除数多項式</param>
		/// <returns></returns>
		public static byte[] ReedSolomonComputeRemainder(IReadOnlyList<byte> data, IReadOnlyList<byte> divisor)
		{
			var result = new byte[divisor.Count];
			foreach (var b in data)
			{
				// Polynomial division
				int factor = (b ^ result[0]) & 0xFF;
				Array.Copy(result, 1, result, 0, result.Length - 1);
				result[result.Length - 1] = 0;
				for (int i = 0; i < result.Length; i++)
				{
					result[i] ^= (byte)ReedSolomonMultiply(divisor[i] & 0xFF, factor);
				}
			}
			return result;
		}


		// Returns the product of the two given field elements modulo GF(2^8/0x11D). The arguments and result
		// are unsigned 8-bit integers. This could be implemented as a lookup table of 256*256 entries of uint8.
		private static int ReedSolomonMultiply(int x, int y)
		{
			Debug.Assert(x >> 8 == 0 && y >> 8 == 0);
			// Russian peasant multiplication
			int z = 0;
			for (int i = 7; i >= 0; i--)
			{
				z = (int)((z << 1) ^ (((uint)z >> 7) * 0x11D));
				z ^= (int)((((uint)y >> i) & 1) * x);
			}
			Debug.Assert((uint)z >> 8 == 0);
			return z;
		}


		// Returns the number of 8-bit data (i.e. not error correction) codewords contained in any
		// QR Code of the given version number and error correction level, with remainder bits discarded.
		// This stateless pure function could be implemented as a (40*4)-cell lookup table.
		public static int GetNumDataCodewords(int ver, ErrorCorrectionLevel ecl)
		{
			return GetNumRawDataModules(ver) / 8
				- EccCodeWordsPerBlock[(int)ecl][ver]
				* NumErrorCorrectionBlocks[(int)ecl][ver];
		}


		// Can only be called immediately after a white run is added, and
		// returns either 0, 1, or 2. A helper function for getPenaltyScore().
		private int FinderPenaltyCountPatterns(int[] runHistory)
		{
			int n = runHistory[1];
			Debug.Assert(n <= Size * 3);
			bool core = n > 0 && runHistory[2] == n && runHistory[3] == n * 3 && runHistory[4] == n && runHistory[5] == n;
			return (core && runHistory[0] >= n * 4 && runHistory[6] >= n ? 1 : 0)
				 + (core && runHistory[6] >= n * 4 && runHistory[0] >= n ? 1 : 0);
		}


		// Must be called at the end of a line (row or column) of modules. A helper function for getPenaltyScore().
		private int FinderPenaltyTerminateAndCount(bool currentRunColor, int currentRunLength, int[] runHistory)
		{
			if (currentRunColor)
			{
				// Terminate black run
				FinderPenaltyAddHistory(currentRunLength, runHistory);
				currentRunLength = 0;
			}
			currentRunLength += Size;  // Add white border to final run
			FinderPenaltyAddHistory(currentRunLength, runHistory);
			return FinderPenaltyCountPatterns(runHistory);
		}


		// Pushes the given value to the front and drops the last value. A helper function for getPenaltyScore().
		private void FinderPenaltyAddHistory(int currentRunLength, int[] runHistory)
		{
			if (runHistory[0] == 0)
			{
				currentRunLength += Size;  // Add white border to initial run
			}
			Array.Copy(runHistory, 0, runHistory, 1, runHistory.Length - 1);
			runHistory[0] = currentRunLength;
		}

		#endregion Private helper functions

		/// <summary>
		/// x の i 番目のビットが 1 に設定されている場合に true を返します。
		/// </summary>
		public static bool GetBit(int x, int i)
		{
			return (((uint)x >> i) & 1) != 0;
		}

		#region Constants and tables

		/** The minimum version number  (1) supported in the QR Code Model 2 standard. */
		public const int MinVersion = 1;

		/** The maximum version number (40) supported in the QR Code Model 2 standard. */
		public const int MaxVersion = 40;


		/// <summary>
		/// この値を指定するとマスクを自動で決定する。
		/// </summary>
		public const int AutoMask = -1;
		/// <summary>
		/// マスクの最小値
		/// </summary>
		public const int MinMask = 1;
		/// <summary>
		/// マスクの最大値
		/// </summary>
		public const int MaxMask = 7;

		// For use in getPenaltyScore(), when evaluating which mask is best.
		private const int PenaltyN1 = 3;
		private const int PenaltyN2 = 3;
		private const int PenaltyN3 = 40;
		private const int PenaltyN4 = 10;


		private static readonly sbyte[][] EccCodeWordsPerBlock = new sbyte[][]
		{
			// Version: (note that index 0 is for padding, and is set to an illegal value)
			//            0,  1,  2,  3,  4,  5,  6,  7,  8,  9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40    Error correction level
			new sbyte[]{ -1,  7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30},  // Low
			new sbyte[]{ -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28},  // Medium
			new sbyte[]{ -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30},  // Quartile
			new sbyte[]{ -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30},  // High
		};

		/// <summary>
		/// Version: (note that index 0 is for padding, and is set to an illegal value)
		/// Version: インデックス 0 はパディング用であり、不正な値に設定されていることに注意してください。
		/// </summary>
		private static readonly sbyte[][] NumErrorCorrectionBlocks = new sbyte[][]
		{
			//           0, 1, 2, 3, 4, 5, 6, 7, 8, 9,10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40    Error correction level
			new sbyte[]{-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4,  4,  4,  4,  4,  6,  6,  6,  6,  7,  8,  8,  9,  9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25},  // Low
			new sbyte[]{-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5,  5,  8,  9,  9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49},  // Medium
			new sbyte[]{-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8,  8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68},  // Quartile
			new sbyte[]{-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81},  // High
		};

		#endregion Constants and tables

		/// <summary>
		/// The error correction level in a QR Code symbol.
		/// QRコード記号の誤り訂正レベル。
		/// </summary>
		public enum ErrorCorrectionLevel
		{
			/// <summary>
			/// QRコードは約7％の誤りを許容します。
			/// The QR Code can tolerate about  7% erroneous codewords
			/// </summary>
			Low = 0,
			/// <summary>
			/// QRコードは約15％の誤りを許容します。
			/// The QR Code can tolerate about 15% erroneous codewords
			/// </summary>
			Medium,
			/// <summary>
			/// QRコードは約25％の誤りを許容します。
			/// The QR Code can tolerate about 25% erroneous codewords
			/// </summary>
			Quartile,
			/// <summary>
			/// QRコードは約30％の誤りを許容します。
			/// The QR Code can tolerate about 30% erroneous codewords
			/// </summary>
			High,
		}

		/// <summary>
		/// module(pixel)の種類
		/// </summary>
		[Flags]
		public enum ModuleType
		{
			/// <summary>
			/// データ
			/// </summary>
			Data = 0,
			/// <summary>
			/// 
			/// </summary>
			Function = 1,
			/// <summary>
			/// 位置合わせパターン
			/// </summary>
			AlignmentPattern = Function | (0b10),
			/// <summary>
			/// バージョン
			/// </summary>
			Version = Function | (0b100),
			/// <summary>
			/// フォーマット
			/// </summary>
			Format = Function | (0b1000),
			/// <summary>
			/// 位置検出パターン
			/// </summary>
			FinderPattern = Function | (0b1_0000),
			/// <summary>
			/// 水平タイミングパターン
			/// </summary>
			HorizonTimingPattern = Function | (0b10_0000),
			/// <summary>
			/// 垂直タイミングパターン
			/// </summary>
			VerticalTimingPattern = Function | (0b100_0000),
		}

		/// <summary>
		/// Returns a value in the range 0 to 3 (unsigned 2-bit integer).
		/// 0 から 3 の範囲の値（符号なし 2 ビット整数）を返します。
		/// </summary>
		/// <param name="ecl">誤り訂正レベル</param>
		/// <returns></returns>
		private static int GetFormatBits(ErrorCorrectionLevel ecl)
		{
			switch (ecl)
			{
				case ErrorCorrectionLevel.Low: return 1;
				case ErrorCorrectionLevel.Medium: return 0;
				case ErrorCorrectionLevel.Quartile: return 3;
				case ErrorCorrectionLevel.High: return 2;
				default:
					throw new Exception("Assertion error");//throw std::logic_error("Assertion error");
			}
		}
	}
}