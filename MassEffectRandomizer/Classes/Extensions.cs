using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MassEffectRandomizer.Classes
{
    public static class EnumerableExtensions
    {
        public static T[] TypedClone<T>(this T[] src)
        {
            return (T[])src.Clone();
        }
    }
}
