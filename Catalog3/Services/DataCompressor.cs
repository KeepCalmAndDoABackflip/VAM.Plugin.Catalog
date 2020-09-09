using System;

namespace Dyme.Compression
{
	public class DymeCompression
	{

		public static string Compress(string inputString)
		{
			return Compress(inputString, false);
		}

		public static string Compress(string inputString, bool asTextSafe, char customFlagChar)
		{
			return Compress(inputString, asTextSafe, Convert.ToByte(customFlagChar));
		}

		public static string Compress(string inputString, bool asTextSafe, byte customFlagByte = 0)
		{
			// If the current string is 5 bytes or less then theres no point in compressing 
			// since a compression token is 4 bytes long + 1 flagByte = 5 bytes = zero benefit.
			if (inputString.Length < 6) return inputString;

			// Parse into byte array...
			byte[] inputBuffer = BufferFromString(inputString);

			// Compress...
			int newBufferLength = SequenceCompressAndReturnNewBufferLength(ref inputBuffer, inputBuffer.Length, asTextSafe, Convert.ToByte(customFlagByte));

			// Parse buffer into string...
			string newString = StringFromBuffer(inputBuffer, newBufferLength);

			return newString;
		}

		public static string Decompress(string inputString)
		{
			// If the string is less than 5 bytes then there cannot be any compression applied
			// since a compression token is at least 4 bytes long + 1 flagByte = 5 bytes.
			if (inputString.Length < 5) return inputString;

			// Parse string into buffer...
			byte[] inputBuffer = BufferFromString(inputString);

			// Decompress...
			byte[] newBuffer = SequenceDecompressBytes(inputBuffer, inputBuffer.Length);

			// Parse buffer into string...
			string newString = StringFromBuffer(newBuffer, newBuffer.Length);

			return newString;
		}

		public static byte[] Compress(byte[] input)
		{
			// If its 4 bytes or less then theres no point in compressing 
			// since a compression token is 4 bytes long.
			if (input.Length < 6) return input;

			// Compress...
			int newBufferLength = SequenceCompressAndReturnNewBufferLength(ref input, input.Length);

			byte[] finalBuffer = ResizeBuffer(input, newBufferLength);

			return finalBuffer;
		}

		public static byte[] Decompress(byte[] input)
		{
			// If the string is less than 4 bytes then there cannot be any compression applied
			// since a compression token is at least 4 bytes long.
			if (input.Length < 4) return input;

			// Decompress...
			byte[] newBuffer = SequenceDecompressBytes(input, input.Length);

			return newBuffer;
		}

		private static byte[] ResizeBuffer(byte[] input, int newBufferLength)
		{
			byte[] newBuffer = new byte[newBufferLength];
			for (var i = 0; i < newBufferLength; i++)
			{
				newBuffer[i] = input[i];
			}
			return newBuffer;
		}

		public static byte[] SequenceDecompressBytes(byte[] inputBuffer, int inputBufferLength)
		{
			bool isTextSafe = Convert.ToChar(inputBuffer[--inputBufferLength]) == '1' ? true : false;
			byte flagByte = inputBuffer[--inputBufferLength];

			// Handle the no-compression case...
			if (flagByte == 0)
			{
				var buffer = new byte[inputBufferLength];
				CopyInto(inputBuffer, buffer);
				return buffer;
			}

			int newBufferLength = 0;

			for (var i = 0; i < inputBufferLength; i++)
			{
				if (inputBuffer[i] == flagByte)
				{
					i++; //...skip original character.
					ushort repetitionLength = !isTextSafe ? GetRepetitionLength(inputBuffer, ref i) : GetRepetitionLengthTextSafe(inputBuffer, ref i);
					newBufferLength += repetitionLength;
				}
				else
				{
					newBufferLength += 1;
				}
			}

			var newBuffer = new byte[newBufferLength];
			int newBufferCounter = 0;

			for (var i = 0; i < inputBufferLength; i++)
			{
				if (inputBuffer[i] == flagByte)
				{
					// If this one is a flag byte, 
					//   - then the next byte is the repeated character
					//   - and the next 2 bytes(UInt16) are the length of the repetition
					var originalCharacter = inputBuffer[++i];
					ushort repetitionLength = !isTextSafe ? GetRepetitionLength(inputBuffer, ref i) : GetRepetitionLengthTextSafe(inputBuffer, ref i);
					for (var x = 0; x < repetitionLength; x++)
					{
						newBuffer[newBufferCounter++] = originalCharacter;
					}
				}
				else
				{
					newBuffer[newBufferCounter++] = inputBuffer[i];
				}
			}
			return newBuffer;
		}

		private static ushort GetRepetitionLength(byte[] inputBuffer, ref int currentIndex)
		{
			byte[] repetitionLengthByteArray = new byte[] { inputBuffer[++currentIndex], inputBuffer[++currentIndex] };
			var repetitionLength = BitConverter.ToUInt16(repetitionLengthByteArray, 0);
			return repetitionLength;
		}

		private static ushort GetRepetitionLengthTextSafe(byte[] inputBuffer, ref int currentIndex)
		{
			byte repetitionCountNumberLengthAsByte = inputBuffer[++currentIndex];
			string repetitionCountNumberLengthAsChar = new string(new char[] { Convert.ToChar(repetitionCountNumberLengthAsByte) });
			int repetitionCountNumberLength = int.Parse(repetitionCountNumberLengthAsChar);
			char[] repetitionCountAsCharacters = new char[repetitionCountNumberLength];
			for (var i = 0; i < repetitionCountNumberLength; i++)
			{
				repetitionCountAsCharacters[i] = Convert.ToChar(inputBuffer[++currentIndex]);
			}
			string repetitionCountAsString = new string(repetitionCountAsCharacters);
			ushort repetitionCount = ushort.Parse(repetitionCountAsString);
			return repetitionCount;
		}

		public static int SequenceCompressAndReturnNewBufferLength(ref byte[] inputBuffer, int inputBufferLength, bool asTextSafe = false, byte customFlagByte = 0)
		{
			const int MAX_LENGTH_OF_UINT16 = 65535;
			byte rolledUpByte = inputBuffer[0];
			var newBuffer = new byte[inputBufferLength + 2]; // + 2 in case of extra length due to info bytes and no compression.
			var newBufferLength = 0;
			ushort rollupCount = 1;
			byte isTextSafeByte = Convert.ToByte(asTextSafe ? '1' : '0');

			// Pre-process sequence to get flag byte...
			byte flagByte = GetFirstAvailableFlagByte(inputBuffer, inputBufferLength, asTextSafe, customFlagByte);
			// In the rare occasion that there are no bytes to compress with...
			if (flagByte == 0)
			{ // ...Set the flagByte to null (0)
				// We now have to create a string 2 characters longer than the original string.
				// Why? ... because The decompression algorithm will assume that the last byte in the file is the flagByte. 
				// So we have to add the flag byte and the textSafe boolean byte, otherwise the decompression will use whatever byte happens to be at the end.
				// Since there's no compression, these extra bytes are now adding to the length of the original string.
				var largerBuffer = new byte[inputBufferLength + 2]; // +1 flag-byte + 1 isTextSafe-byte
				CopyInto(inputBuffer, largerBuffer);
				inputBuffer = largerBuffer;
				inputBuffer[inputBufferLength++] = flagByte;
				inputBuffer[inputBufferLength++] = isTextSafeByte;
				return inputBufferLength; // ...No available bytes to compress with. Exit early with no compression.
			}

			for (var i = 1; i < inputBufferLength; i++)
			{
				if (inputBuffer[i] == inputBuffer[i - 1] && rollupCount < MAX_LENGTH_OF_UINT16)
				{
					rollupCount++;
				}
				else
				{
					rolledUpByte = inputBuffer[i - 1];
					ProcessSequence(inputBuffer, i, rolledUpByte, ref newBuffer, ref newBufferLength, rollupCount, flagByte, asTextSafe);
					rollupCount = 1;
				}
			}

			// Perform the final iteration here rather than checking in the main loop.
			// This avoids repeating a check that'll only happen once and saves us O^1 unneccesary checks
			rolledUpByte = inputBuffer[inputBufferLength - 1];
			ProcessSequence(inputBuffer, inputBufferLength, rolledUpByte, ref newBuffer, ref newBufferLength, rollupCount, flagByte, asTextSafe);

			newBuffer[newBufferLength++] = flagByte;
			newBuffer[newBufferLength++] = isTextSafeByte;

			inputBuffer = newBuffer;
			return newBufferLength;
		}

		private static byte CreateOptionsByte(bool b1, bool b2, bool b3, bool b4, bool b5, bool b6, bool b7, bool b8)
		{
			var decimalRepresentation = (b1 ? 0 : 2E0) + (b2 ? 0 : 2E1) + (b3 ? 0 : 2E2) + (b4 ? 0 : 2E3) + (b5 ? 0 : 2E4) + (b6 ? 0 : 2E5) + (b7 ? 0 : 2E6) + (b8 ? 0 : 2E7);
			return Convert.ToByte(decimalRepresentation);
		}

		// Will truncate any additional bytes that do not fit into the outputBuffer
		private static void CopyInto(byte[] inputBuffer, byte[] outputBuffer)
		{
			var minLen = inputBuffer.Length < outputBuffer.Length ? inputBuffer.Length : outputBuffer.Length;
			for (var i = 0; i < minLen; i++)
			{
				outputBuffer[i] = inputBuffer[i];
			}
		}

		private static byte GetFirstAvailableFlagByte(byte[] inputBuffer, int inputBufferLength, bool asTextSafe = false, byte customFlagByte = 0)
		{
			const int FLAG_BYTE_RANGE_START = 1;
			const int FLAG_BYTE_RANGE_END = 255;
			int[] bytesUsedCounts = new int[256];
			for (var i = 0; i < inputBufferLength; i++)
			{
				bytesUsedCounts[inputBuffer[i]]++;
			}

			if (customFlagByte != 0)
			{
				if (bytesUsedCounts[customFlagByte] == 0) return customFlagByte;
				throw new Exception($"Cannot use '{customFlagByte}' as a flag byte");
			}

			if (asTextSafe)
			{
				for (var i = 33; i < 127; i++)
				{
					if (bytesUsedCounts[i] == 0) return Convert.ToByte(i);
				}
				throw new Exception($"No text-safe characters available");
			}

			for (var i = FLAG_BYTE_RANGE_START; i <= FLAG_BYTE_RANGE_END; i++)
			{
				if (bytesUsedCounts[i] == 0) return Convert.ToByte(i);
			}

			return 0;
		}

		private static void ProcessSequence(byte[] inputBuffer, int inputBufferCursor, byte rolledUpByte, ref byte[] newBuffer, ref int newBufferLength, ushort rollupCount, byte flagByte, bool asTextSafe = false)
		{
			// Add the set of collected bytes...
			bool wasAbleToCompress = !asTextSafe
						? RollUp(flagByte, rolledUpByte, rollupCount, ref newBuffer, ref newBufferLength)
						: RollUpAsTextSafe(flagByte, rolledUpByte, rollupCount, ref newBuffer, ref newBufferLength);
			if (!wasAbleToCompress) // ...not long enough to be compressed
			{
				// Add any bytes that were collected as they were originally...
				for (var x = inputBufferCursor - rollupCount; x < inputBufferCursor; x++)
				{
					newBuffer[newBufferLength++] = inputBuffer[x];
				}
			}
		}

		private static bool RollUp(byte flagByte, byte rolledUpByte, ushort rollupCount, ref byte[] newBuffer, ref int newBufferLength)
		{
			const ushort COMPRESSION_THRESHOLD = 4; // ...the minimum length that the compression must be in order to make it worth compressing.
			if (rollupCount <= COMPRESSION_THRESHOLD) return false;
			byte[] rollupCountBytes = BitConverter.GetBytes(rollupCount);
			newBuffer[newBufferLength++] = flagByte;
			newBuffer[newBufferLength++] = rolledUpByte;
			newBuffer[newBufferLength++] = rollupCountBytes[0];
			newBuffer[newBufferLength++] = rollupCountBytes[1];
			return true;
		}

		private static bool RollUpAsTextSafe(byte flagByte, byte rolledUpByte, ushort rollupCount, ref byte[] newBuffer, ref int newBufferLength)
		{
			const ushort COMPRESSION_BASE_THRESHOLD = 3; // 1 flag-byte + 1 character-byte + 1 rollup-count-length-byte

			string rollupCountNumber = rollupCount.ToString();
			byte[] rollupCountAsCharBytes = BufferFromString(rollupCountNumber);
			int lengthOfRollupCountNumber = rollupCountNumber.Length;
			if (rollupCount <= COMPRESSION_BASE_THRESHOLD + lengthOfRollupCountNumber) return false; // not worth compressing
			string lengthOfRollupCountNumberAsString = lengthOfRollupCountNumber.ToString(); //... can only be 1 ASCII digit, "0" - "9" (which is fine because there is already a Uint16 limit on the sequence of length "5" digits at most)
			if (lengthOfRollupCountNumberAsString.Length > 1) throw new Exception("The string length of the rollup-count cannot be longer than 1 digit (the rollup-count must be between 1 and 9 digits long)");
			byte lengthOfRollupCountNumberAsByte = Convert.ToByte(lengthOfRollupCountNumberAsString[0]);
			newBuffer[newBufferLength++] = flagByte;
			newBuffer[newBufferLength++] = rolledUpByte;
			newBuffer[newBufferLength++] = lengthOfRollupCountNumberAsByte;
			for (var i = 0; i < lengthOfRollupCountNumber; i++)
			{
				newBuffer[newBufferLength++] = rollupCountAsCharBytes[i];
			}
			return true;
		}

		private static byte[] BufferFromString(string inputString)
		{
			var inputBuffer = new byte[inputString.Length];
			for (var i = 0; i < inputString.Length; i++)
			{
				inputBuffer[i] = Convert.ToByte(inputString[i]);
			}

			return inputBuffer;
		}

		private static string StringFromBuffer(byte[] inputBuffer, int newBufferLength)
		{
			// Parse into character array...
			char[] finalCharArray = new char[newBufferLength];
			for (int i = 0; i < newBufferLength; i++)
			{
				finalCharArray[i] = Convert.ToChar(inputBuffer[i]);
			}
			var newString = new string(finalCharArray);
			return newString;
		}

	}
}
