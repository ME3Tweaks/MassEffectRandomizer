using Gibbed.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MassEffectRandomizer.Classes
{
    public static class EnumerableExtensions
    {
        public static T[] TypedClone<T>(this T[] src)
        {
            return (T[])src.Clone();
        }

        /// <summary>
        /// Overwrites a portion of an array starting at offset with the contents of another array.
        /// Accepts negative indexes
        /// </summary>
        /// <typeparam name="T">Content of array.</typeparam>
        /// <param name="dest">Array to write to</param>
        /// <param name="offset">Start index in dest. Can be negative (eg. last element is -1)</param>
        /// <param name="source">data to write to dest</param>
        public static void OverwriteRange<T>(this IList<T> dest, int offset, IList<T> source)
        {
            if (offset < 0)
            {
                offset = dest.Count + offset;
                if (offset < 0)
                {
                    throw new IndexOutOfRangeException("Attempt to write before the beginning of the array.");
                }
            }
            if (offset + source.Count > dest.Count)
            {
                throw new IndexOutOfRangeException("Attempt to write past the end of the array.");
            }
            for (int i = 0; i < source.Count; i++)
            {
                dest[offset + i] = source[i];
            }
        }
    }

    public static class RandomExtensions
    {
        public static float NextFloat(
            this Random random,
            double minValue,
            double maxValue)
        {
            return (float)(random.NextDouble() * (maxValue - minValue) + minValue);
        }
    }

    public static class ListExtensions
    {
        private static Random rng = new Random();

        public static void Shuffle<T>(this IList<T> list, Random random = null)
        {
            if (random == null && rng == null) rng = new Random();
            Random randomToUse = random ?? rng;
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = randomToUse.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }

    public static class IOExtensions
    {
        public static void WriteStringASCII(this Stream stream, string value)
        {
            stream.WriteValueS32(value.Length + 1);
            stream.WriteStringZ(value, Encoding.ASCII);
        }

        public static void WriteStringUnicode(this Stream stream, string value)
        {
            if (value.Length > 0)
            {
                stream.WriteValueS32(-(value.Length + 1));
                stream.WriteStringZ(value, Encoding.Unicode);
            }
            else
            {
                stream.WriteValueS32(0);
            }
        }

        public static void WriteStream(this Stream stream, MemoryStream value)
        {
            value.WriteTo(stream);
        }

        /// <summary>
        /// Copies the inputstream to the outputstream, for the specified amount of bytes
        /// </summary>
        /// <param name="input">Stream to copy from</param>
        /// <param name="output">Stream to copy to</param>
        /// <param name="bytes">The number of bytes to copy</param>
        public static void CopyToEx(this Stream input, Stream output, int bytes)
        {
            var buffer = new byte[32768];
            int read;
            while (bytes > 0 &&
                   (read = input.Read(buffer, 0, Math.Min(buffer.Length, bytes))) > 0)
            {
                output.Write(buffer, 0, read);
                bytes -= read;
            }
        }

        public static NameReference ReadNameReference(this Stream stream, ME1Package pcc)
        {
            return new NameReference(pcc.getNameEntry(stream.ReadValueS32()), stream.ReadValueS32());
        }
    }

    public static class StringExtensions
    {
        /// <summary>
        /// Truncates string so that it is no longer than the specified number of characters.
        /// </summary>
        /// <param name="str">String to truncate.</param>
        /// <param name="length">Maximum string length.</param>
        /// <returns>Original string or a truncated one if the original was too long.</returns>
        public static string Truncate(this string str, int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", "Length must be >= 0");
            }

            if (str == null)
            {
                return null;
            }

            int maxLength = Math.Min(str.Length, length);
            return str.Substring(0, maxLength);
        }

        public static bool ContainsWord(this string s, string word)
        {
            string[] ar = s.Split(' ', '.'); //Split on space and periods

            foreach (string str in ar)
            {
                if (str.ToLower() == word.ToLower())
                    return true;
            }
            return false;
        }
    }
}
