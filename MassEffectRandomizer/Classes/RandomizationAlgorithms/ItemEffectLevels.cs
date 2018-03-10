using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes.RandomizationAlgorithms
{
    public class ItemEffectLevels
    {
        public static void RandomizeGE_Weap_HeatLossRate(Bio2DA table2da, int row, Random random)
        {
            float basevalue = random.NextFloat(0.2, 0.25);
            float scalingvalue = random.NextFloat(0.008, 0.015);

            for (int i = 4; i <= /*itemsitems2da.columnNames.Count()*/ 13; i++)
            {
                int sizebefore = table2da[row, i].Data.Count();
                Console.WriteLine("[" + row + "][" + i + "]  (" + table2da.columnNames[i] + ") value is " + table2da[row, i].GetDisplayableValue());
                float randvalue = basevalue + (scalingvalue * (i - 4));
                Console.WriteLine("Items - Weapon Stats Randomizer HEATLOSSRATE[" + row + "][" + i + "] (" + table2da.columnNames[i] + ") value is now " + randvalue);
                table2da[row, i].Data = BitConverter.GetBytes(randvalue);
                table2da[row, i].Type = Bio2DACell.TYPE_FLOAT;
                if (table2da[row, i].Data.Count() != sizebefore)
                {
                    Debugger.Break();
                }
            }
        }

        public static void Randomize_GE_Weap_HeatPerShot(Bio2DA table2da, int row, Random random)
        {
            int[] lowheatindexes = { 61 };
            int[] midheatindexes = { 107, 385, 1148, 2506 };
            int[] highheatindexes = { 417, 525, 1086 };

            float basevalue = random.NextFloat(0.08, 0.285);
            if (lowheatindexes.Contains(row))
            {
                basevalue *= 0.53f;
            }
            if (midheatindexes.Contains(row))
            {
                basevalue *= 2.5f;
            }
            if (highheatindexes.Contains(row))
            {
                basevalue *= 3.5f;
            }
            float scalingvalue = random.NextFloat(0.01, 0.04);

            for (int i = 4; i <= /*itemsitems2da.columnNames.Count()*/ 13; i++)
            {
                int sizebefore = table2da[row, i].Data.Count();
                Console.WriteLine("[" + row + "][" + i + "]  (" + table2da.columnNames[i] + ") value is " + table2da[row, i].GetDisplayableValue());
                float randvalue = basevalue - (scalingvalue * (i - 4));
                Console.WriteLine("Items - Weapon Stats Randomizer HEATPERSHOT [" + row + "][" + i + "] (" + table2da.columnNames[i] + ") value is now " + randvalue);
                table2da[row, i].Data = BitConverter.GetBytes(randvalue);
                table2da[row, i].Type = Bio2DACell.TYPE_FLOAT;
                if (table2da[row, i].Data.Count() != sizebefore)
                {
                    Debugger.Break();
                }
            }
        }
    }
}
