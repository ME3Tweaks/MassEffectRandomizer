using System;
using System.Collections.Generic;

namespace MassEffectRandomizer.Classes
{
    public static class EnumerableExtensions
    {
        public static T[] TypedClone<T>(this T[] src)
        {
            return (T[])src.Clone();
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
    }
}
