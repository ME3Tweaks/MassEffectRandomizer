using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes.RandomizationAlgorithms
{
    static class TalentEffectLevels
    {
        public enum RandomizationDirection
        {
            UpDown = 0,
            DownOnly = 1,
            UpOnly = 2
        }

        /// <summary>
        /// Randomizes a row of level-based stats using a linear distribution with optional columns that can get boosted a random amount
        /// </summary>
        /// <param name="table2DA">Table to randomize</param>
        /// <param name="row">Row to randomize</param>
        /// <param name="startcol">Level 1 column index</param>
        /// <param name="numcols">Number of columns (maxlevel)</param>
        /// <param name="maxFudge">Maximum randomness scalar for fudging start, endpoints</param>
        /// <param name="boostCols">List of levels that will receive a stat boost and apply to all following levels in the column list. E.g. Advanced Throw  (8) adds more than a standard level</param>
        /// <param name="random">Random number generator</param>
        public static void RandomizeRow_FudgeEndpointsEvenDistribution(Bio2DA table2DA, int row, int startcol, int numcols, double maxFudge, List<int> boostedLevels, Random random, float minValue = float.MinValue, float maxValue = float.MaxValue, RandomizationDirection directionsAllowed = RandomizationDirection.UpDown)
        {

            float startValue = float.Parse(table2DA[row, startcol].GetDisplayableValue());
            float endValue = float.Parse(table2DA[row, startcol + (numcols - 1)].GetDisplayableValue());

            if ((endValue < startValue && directionsAllowed == RandomizationDirection.UpOnly) || (endValue > startValue && directionsAllowed == RandomizationDirection.DownOnly))
            {
                endValue = startValue;
            }

            startValue = startValue *= random.NextFloat(1 - maxFudge, 1 + maxFudge);

            float preValidationEndValue = endValue * random.NextFloat(1 - maxFudge, 1 + maxFudge);
            if (directionsAllowed != RandomizationDirection.UpDown)
            {
                if (directionsAllowed == RandomizationDirection.DownOnly)
                {
                    //endvalue must be less than start
                    while (preValidationEndValue > startValue)
                    {
                        preValidationEndValue = endValue * random.NextFloat(1 - maxFudge, 1 + maxFudge);
                    }
                }
                else
                {
                    //endvalue must be higher than start
                    while (preValidationEndValue < startValue)
                    {
                        preValidationEndValue = endValue * random.NextFloat(1 - maxFudge, 1 + maxFudge);
                    }
                }
            }
            endValue = preValidationEndValue;
            if (endValue < minValue)
            {
                endValue = minValue;
            }
            if (endValue > maxValue)
            {
                endValue = maxValue;
            }

            float pointsBetweenDistribution = (endValue - startValue) / numcols;
            float previousValue = startValue;
            for (int level = 1; level < numcols + 1; level++)
            {
                previousValue = previousValue + (pointsBetweenDistribution * (level - 1));
                if (boostedLevels != null && boostedLevels.Contains(level))
                {
                    //Level receives boost
                    previousValue += pointsBetweenDistribution * random.NextFloat(1 - maxFudge, 1 + maxFudge);
                }

                if (previousValue < minValue)
                {
                    previousValue = minValue;
                }
                if (previousValue > maxValue)
                {
                    previousValue = maxValue;
                }

                int column = startcol + (level - 1);
                if (Math.Abs(previousValue - Math.Floor(previousValue + 0.001)) < 0.001)
                {
                    //int
                    table2DA[row, column].Type = Bio2DACell.Bio2DADataType.TYPE_INT;
                    int value = (int)previousValue;
                    Debug.WriteLine("TalentEffectLevels " + row + ", " + column + " = " + value);
                    table2DA[row, startcol + (level - 1)].Data = BitConverter.GetBytes(value);
                }
                else
                {
                    //float
                    table2DA[row, column].Type = Bio2DACell.Bio2DADataType.TYPE_FLOAT;
                    Debug.WriteLine("TalentEffectLevels " + row + ", " + column + " = " + previousValue);
                    table2DA[row, startcol + (level - 1)].Data = BitConverter.GetBytes(previousValue);
                }
            }
        }
    }
}
