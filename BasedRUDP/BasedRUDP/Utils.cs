using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BasedRUDP
{
    public static class Utils
    {
        public static Random Rnd = new Random();

        public static bool RandomBool(float probability)
        {
            bool should = Rnd.Next() > int.MaxValue * probability;
            return should;
        }

    }
}
