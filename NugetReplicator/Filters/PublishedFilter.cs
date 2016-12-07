using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetReplicator.Filters
{
    public class PublishedFilter
    {
        public static bool isOK(string data, DateTime tillDate)
        {
            DateTime Published = DateTime.Parse(data);
            int diff = Published.CompareTo(tillDate);

            if (diff <= 0)
                return true;

            Console.WriteLine($"Published Err {data}");
            return false;
        }
    }
}
