using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetReplicator.Filters
{
    public class NugetNameFilter
    {
        public static bool isOK(string data)
        {
            if (data.StartsWith("7zip"))
                return true;

            bool retval = (data.IndexOf("Persian", StringComparison.OrdinalIgnoreCase) > -1) ||
                          (data.IndexOf("Nuclear", StringComparison.OrdinalIgnoreCase) > -1) || 
                          (data.IndexOf("Apitron", StringComparison.OrdinalIgnoreCase) > -1) ||
                          (Char.IsDigit(data[0]) || data.StartsWith("_"));

            if (retval)
                Console.WriteLine($"NugetNameFilter error {data}");

            return !retval;
        }
    }
}
