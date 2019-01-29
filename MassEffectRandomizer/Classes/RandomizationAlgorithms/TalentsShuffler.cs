using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes.RandomizationAlgorithms
{
    static class TalentsShuffler
    {
        /// <summary>
        /// Shuffles a list of talent IDs into groups. Ensures no group has 2 of the same item
        /// </summary>
        /// <param name="allTalents"></param>
        /// <returns></returns>
        public static List<List<int>> TalentShuffle(List<int> allTalents, int numGroups, int numPerGroup, Random random)
        {

            if (allTalents.Count < numGroups * numPerGroup)
            {
                throw new Exception("Talent shuffler does not have enough talents to shuffle");
            }

            var lists = new List<List<int>>();
            allTalents.Shuffle(random);
            for (int i = 0; i < numGroups; i++)
            {
                var list = new List<int>();
                for (int k = 0; k < numPerGroup; k++)
                {
                    list.Add(allTalents[0]);
                    allTalents.RemoveAt(0);
                }
                lists.Add(list);
            }

            //Validate and reassign duplicate powers
            while (true)
            {
                bool allListsUnique = true;
                bool forceRunLoopAgain = false;
                for (int i = 0; i < lists.Count; i++)
                {
                    List<int> powerList = lists[i];
                    if (powerList.Count != powerList.Distinct().Count())
                    {
                        // Duplicates exist
                        allListsUnique = false;

                        //Get a duplicate power
                        List<int> duplicates = powerList.GroupBy(x => x)
                             .Where(g => g.Count() > 1)
                             .Select(g => g.Key)
                             .ToList();
                        int indexToRemove = powerList.IndexOf(duplicates[0]);
                        int powerToSwapToAnother = powerList[indexToRemove];
                        powerList.RemoveAt(indexToRemove);
                        Debug.WriteLine("Removed from powerList. Size now " + powerList.Count);

                        //Find another list that does not contain this power
                        foreach (var swapList in lists)
                        {
                            if (swapList == powerList)
                            {
                                continue; //don't swap with self
                            }
                            Debug.WriteLine("Find other list");
                            bool keepCheckingLists = true;
                            if (!swapList.Contains(powerToSwapToAnother))
                            {
                                //Find power in that list that does not exist in this list
                                for (int powerIndex = 0; powerIndex < swapList.Count; powerIndex++)
                                {
                                    Debug.WriteLine("Find power in list that doesn't exist in this one");
                                    int otherListTalent = swapList[powerIndex];
                                    if (!powerList.Contains(otherListTalent))
                                    {
                                        //We have the item to swap
                                        //Perform the swap
                                        powerList.Add(otherListTalent);
                                        Debug.WriteLine("Add to powerlist. Size now " + powerList.Count);
                                        var removed = swapList.Remove(otherListTalent);
                                        //Debug.WriteLine("Removed in swaplist: " + removed + ". Size now " + swapList.Count);
                                        swapList.Add(powerToSwapToAnother);
                                        keepCheckingLists = false;
                                        if (powerList.Count != swapList.Count)
                                        {
                                            Debugger.Break();
                                        }
                                        Log.Information(" >> Swapped powers between lists");
                                        break;
                                    }
                                }
                            }
                            if (!keepCheckingLists)
                            {
                                break;
                            }
                        }
                    }

                }

               
                if (allListsUnique)
                {
                    break;
                }
            }

            return lists;
        }

    }


}
