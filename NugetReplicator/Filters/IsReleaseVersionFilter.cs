using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetReplicator.Filters
{
    public class IsReleaseVersionFilter
    {
        public static bool isOK(string data)
        {
            if (data.Contains("alpha") ||
                data.Contains("beta") ||
                data.Contains("rc"))
            {
                Console.WriteLine($"Release Err {data}");
                return false;
            }

            return true;
        }
    }
}
