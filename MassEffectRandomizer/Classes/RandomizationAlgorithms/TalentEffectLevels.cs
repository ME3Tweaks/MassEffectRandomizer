using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes.RandomizationAlgorithms
{
    static class TalentEffectLevels
    {
        /// <summary>
        /// Randomizes a row of level-based stats using a linear distribution with optional columns that can get boosted a random amount
        /// </summary>
        /// <param name="table2DA">TAble to randomize</param>
        /// <param name="row">Row to randomize</param>
        /// <param name="startcol">Level 1 column index</param>
        /// <param name="numcols">Number of columns (maxlevel)</param>
        /// <param name="maxFudge">Maximum randomness scalar for fudging start, endpoints</param>
        /// <param name="boostCols">List of levels that will receive a stat boost and apply to all following levels in the column list. E.g. Advanced Throw  (8) adds more than a standard level</param>
        /// <param name="random">Random number generator</param>
        public static void RandomizeRow_FudgeEndpointsEvenDistribution(Bio2DA table2DA, int row, int startcol, int numcols, double maxFudge, List<int> boostedLevels, Random random)
        {

            float startValue = float.Parse(table2DA[row, startcol].GetDisplayableValue());
            float endValue = float.Parse(table2DA[row, startcol + (numcols - 1)].GetDisplayableValue());

            startValue = startValue *= random.NextFloat(1 - maxFudge, 1 + maxFudge);
            endValue = endValue *= random.NextFloat(1 - maxFudge, 1 + maxFudge);

            float pointsBetweenDistribution = (endValue - startValue) / numcols;
            float previousValue = startValue;
            for (int level = 1; level < numcols; level++)
            {
                previousValue = previousValue + (pointsBetweenDistribution * (level - 1)); 
                if (boostedLevels != null && boostedLevels.Contains(level))
                {
                    //Level receives boost
                    previousValue += pointsBetweenDistribution * random.NextFloat(1 - maxFudge, 1 + maxFudge);
                }
                table2DA[row, startcol + (level - 1)].Type = Bio2DACell.TYPE_FLOAT;
                table2DA[row, startcol + (level - 1)].Data = BitConverter.GetBytes(previousValue);
            }

        }
    }
}
