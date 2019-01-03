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
        private static readonly string[] superslowgunsNoDamage = { "GethGun_Sniper_NODAMAGE", "NODAMAGE_Sniper_Rifle" }; //.0833
        private static readonly string[] slowgunsCannonSniper = { "Cannon", "Sniper" };
        private static readonly string[] kindaslowShotgun = { "Shotgun" };
        private static readonly string[] midspeedPistolGeth = { "Pistol", "GethGun", "Supergun" };
        private static readonly string[] fastguns = { "Assault_Rifle", "MachineGun", "AssaultDrone_Gun" };

        public static void Randomize_GE_Weap_HeatLossRate(Bio2DA table2da, int row, Random random)
        {
            //fast guns
            float minvalue = 0.13f;
            float maxvalue = 0.17f;
            float scalingmin = 0.002f;
            float scalingmax = 0.006f;

            string type = table2da[row, 14].GetDisplayableValue();

            if (midspeedPistolGeth.Any(type.Contains))
            {
                minvalue = 0.15f;
                maxvalue = 0.2f;
                scalingmin = 0.002f;
                scalingmax = 0.006f;
            }
            else
            if (kindaslowShotgun.Any(type.Contains))
            {
                minvalue = 0.13f;
                maxvalue = 0.18f;
                scalingmin = 0.003f;
                scalingmax = 0.007f;
            }
            else
            if (slowgunsCannonSniper.Any(type.Contains))
            {
                minvalue = 0.15f;
                maxvalue = 0.19f;
                scalingmin = 0.003f;
                scalingmax = 0.008f;
            }
            else
            if (superslowgunsNoDamage.Any(type.Contains))
            {
                minvalue = 0.16f;
                maxvalue = 0.20f;
                scalingmin = 0.004f;
                scalingmax = 0.009f;
            }
            float basevalue = random.NextFloat(minvalue, maxvalue);
            float scalingvalue = random.NextFloat(scalingmin, scalingmax);
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
            //fast guns
            float minvalue = 0.05f;
            float maxvalue = 0.11f;
            float scalingmin = 0.003f;
            float scalingmax = 0.01f;

            string type = table2da[row, 14].GetDisplayableValue();

            if (midspeedPistolGeth.Any(type.Contains))
            {
                minvalue = 0.08f;
                maxvalue = 0.18f;
                scalingmin = 0.01f;
                scalingmax = 0.021f;
            }
            else
            if (kindaslowShotgun.Any(type.Contains))
            {
                minvalue = 0.35f;
                maxvalue = 0.65f;
                scalingmin = 0.025f;
                scalingmax = 0.037f;
            }
            else
            if (slowgunsCannonSniper.Any(type.Contains))
            {
                minvalue = 0.55f;
                maxvalue = 0.85f;
                scalingmin = 0.025f;
                scalingmax = 0.034f;
            }
            else
            if (superslowgunsNoDamage.Any(type.Contains))
            {
                minvalue = 0.66f;
                maxvalue = 1.05f;
                scalingmin = 0.02f;
                scalingmax = 0.03f;
            }
            float basevalue = random.NextFloat(minvalue, maxvalue);
            float scalingvalue = random.NextFloat(scalingmin, scalingmax);

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

        public static void Randomize_GE_Weap_HeatLossRateOH(Bio2DA table2da, int row, Random random)
        {
            float basevalue = random.NextFloat(0.1, 0.19);
            float scalingvalue = random.NextFloat(0.006, 0.013);

            for (int i = 4; i <= /*itemsitems2da.columnNames.Count()*/ 13; i++)
            {
                int sizebefore = table2da[row, i].Data.Count();
                Console.WriteLine("[" + row + "][" + i + "]  (" + table2da.columnNames[i] + ") value is " + table2da[row, i].GetDisplayableValue());
                float randvalue = basevalue + (scalingvalue * (i - 4));
                Console.WriteLine("Items - Weapon Stats Randomizer HEATLOSSRATEOH[" + row + "][" + i + "] (" + table2da.columnNames[i] + ") value is now " + randvalue);
                table2da[row, i].Data = BitConverter.GetBytes(randvalue);
                table2da[row, i].Type = Bio2DACell.TYPE_FLOAT;
                if (table2da[row, i].Data.Count() != sizebefore)
                {
                    Debugger.Break();
                }
            }
        }

        internal static void Randomize_GE_Weap_RPS(Bio2DA table2da, int row, Random random)
        {
            //fastgun settings
            float minvalue = 7f;
            float maxvalue = 15f;
            float scalingmin = 0.2f;
            float scalingmax = 0.3f;

            string type = table2da[row, 14].GetDisplayableValue();
            if (superslowgunsNoDamage.Any(type.Contains))
            {
                minvalue = 0.0833f;
                maxvalue = 0.3f;
                scalingmin = 0.2f;
                scalingmax = 0.3f;
            }
            else if (slowgunsCannonSniper.Any(type.Contains))
            {
                minvalue = 0.25f;
                maxvalue = 0.65f;
                scalingmin = 0.030f;
                scalingmax = 0.059f;
            }
            else if (kindaslowShotgun.Any(type.Contains))
            {
                minvalue = 0.6f;
                maxvalue = 1.0f;
                scalingmin = 0.015f;
                scalingmax = 0.025f;
            }
            else if (midspeedPistolGeth.Any(type.Contains))
            {
                minvalue = 3.3f;
                maxvalue = 8f;
                scalingmin = 0.2f;
                scalingmax = 0.35f;
            }

            float basevalue = random.NextFloat(minvalue, maxvalue);
            float scalingvalue = random.NextFloat(scalingmin, scalingmax);

            for (int i = 4; i <= /*itemsitems2da.columnNames.Count()*/ 13; i++)
            {
                int sizebefore = table2da[row, i].Data.Count();
                Console.WriteLine("[" + row + "][" + i + "]  (" + table2da.columnNames[i] + ") value is " + table2da[row, i].GetDisplayableValue());
                float randvalue = basevalue + (scalingvalue * (i - 4));
                table2da[row, i].Data = BitConverter.GetBytes(randvalue);
                table2da[row, i].Type = Bio2DACell.TYPE_FLOAT;
                Console.WriteLine("Items - Weapon Stats Randomizer RPS [" + row + "][" + i + "] (" + table2da.columnNames[i] + ") value is now " + table2da[row, i].GetDisplayableValue());
                if (table2da[row, i].Data.Count() != sizebefore)
                {
                    Debugger.Break();
                }
            }
        }

        internal static void Randomize_GE_Weap_PhysicsForce(Bio2DA table2da, int row, Random random)
        {
            //fastgun settings
            int minvalue = 20;
            int maxvalue = 30;
            int scalingmin = 1;
            int scalingmax = 5;

            string type = table2da[row, 14].GetDisplayableValue();
            if (type.Contains("Cannon"))
            {
                minvalue = 1500;
                maxvalue = 4500;
                scalingmin = 100;
                scalingmax = 200;
            }
            else if (superslowgunsNoDamage.Any(type.Contains))
            {
                minvalue = 0;
                maxvalue = 20;
                scalingmin = 2;
                scalingmax = 3;
            }
            else if (slowgunsCannonSniper.Any(type.Contains))
            {
                minvalue = 230;
                maxvalue = 430;
                scalingmin = 7;
                scalingmax = 15;
            }
            else if (kindaslowShotgun.Any(type.Contains))
            {
                minvalue = 410;
                maxvalue = 460;
                scalingmin = 5;
                scalingmax = 10;
            }
            else if (midspeedPistolGeth.Any(type.Contains))
            {
                minvalue = 35;
                maxvalue = 55;
                scalingmin = 1;
                scalingmax = 2;
            }

            int basevalue = random.Next(minvalue, maxvalue);
            int scalingvalue = random.Next(scalingmin, scalingmax);

            for (int i = 4; i <= /*itemsitems2da.columnNames.Count()*/ 13; i++)
            {
                int sizebefore = table2da[row, i].Data.Count();
                Console.WriteLine("[" + row + "][" + i + "]  (" + table2da.columnNames[i] + ") value is " + table2da[row, i].GetDisplayableValue());
                int randvalue = basevalue + (scalingvalue * (i - 4));
                Console.WriteLine("Items - Weapon Stats Randomizer PhysicsForce [" + row + "][" + i + "] (" + table2da.columnNames[i] + ") value is now " + randvalue);
                table2da[row, i].Data = BitConverter.GetBytes(randvalue);
                table2da[row, i].Type = Bio2DACell.TYPE_INT;
                if (table2da[row, i].Data.Count() != sizebefore)
                {
                    Debugger.Break();
                }
            }
        }

        internal static void Randomize_GE_Weap_Damage(Bio2DA table2da, int row, Random random)
        {
            //fastgun settings
            int minvalue = 20;
            int maxvalue = 30;
            int scalingmin = 1;
            int scalingmax = 5;

            string type = table2da[row, 14].GetDisplayableValue();
            if (type.Contains("Cannon"))
            {
                minvalue = 900;
                maxvalue = 1200;
                scalingmin = 10;
                scalingmax = 30;
            }
            else if (superslowgunsNoDamage.Any(type.Contains))
            {
                minvalue = 0;
                maxvalue = 5;
                scalingmin = 2;
                scalingmax = 3;
            }
            else if (slowgunsCannonSniper.Any(type.Contains))
            {
                minvalue = 330;
                maxvalue = 430;
                scalingmin = 7;
                scalingmax = 15;
            }
            else if (kindaslowShotgun.Any(type.Contains))
            {
                minvalue = 275;
                maxvalue = 310;
                scalingmin = 3;
                scalingmax = 6;
            }
            else if (midspeedPistolGeth.Any(type.Contains))
            {
                minvalue = 39;
                maxvalue = 50;
                scalingmin = 1;
                scalingmax = 2;
            }

            if (type.Contains("MINION"))
            {
                //enemies will have a slightly weaker gun
                minvalue = (int) (minvalue * 0.77);
                maxvalue = (int)(maxvalue * 0.77);
            }

            int basevalue = random.Next(minvalue, maxvalue);
            int scalingvalue = random.Next(scalingmin, scalingmax);

            for (int i = 4; i <= /*itemsitems2da.columnNames.Count()*/ 13; i++)
            {
                int sizebefore = table2da[row, i].Data.Count();
                Console.WriteLine("[" + row + "][" + i + "]  (" + table2da.columnNames[i] + ") value is " + table2da[row, i].GetDisplayableValue());
                int randvalue = basevalue + (scalingvalue * (i - 4));
                table2da[row, i].Data = BitConverter.GetBytes(randvalue);
                table2da[row, i].Type = Bio2DACell.TYPE_INT;
                Console.WriteLine("Items - Weapon Stats Randomizer Damage [" + row + "][" + i + "] (" + table2da.columnNames[i] + ") value is now " + table2da[row, i].GetDisplayableValue());

                if (table2da[row, i].Data.Count() != sizebefore)
                {
                    Debugger.Break();
                }
            }
        }
    }
}
