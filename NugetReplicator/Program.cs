using NugetReplicator.Filters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NugetReplicator
{
    class Program
    {
        //private const string FeedUrlBase = "https://www.myget.org/F/dotnet-core/api/v2";
        //IsLatestVersion - without beta
        //IsAbsoluteLatestVersion - include beta
        private const string ServiceUrlBase = "https://www.nuget.org/api/v2/Packages";
        // result 0
        //private const string FeedParameters = "?$filter=VersionDownloadCount gt 1000 and Published ge DateTime'2015-12-29T09:13:28' and Published le DateTime'2015-12-29T09:13:28' and IsPrerelease eq false&$orderby=Published";
        // result 1
        //private const string FeedParameters = "?$filter=VersionDownloadCount gt 1000 and Published ge DateTime'2015-12-29T09:13:28' and Published le DateTime'2015-12-29T10:13:28' and IsPrerelease eq false&$orderby=Published";
        private const string FeedParameters = "?$filter=VersionDownloadCount gt 1000 and Published ge DateTime'1900-01-01T00:00:00' and Published le DateTime'1901-01-01T00:00:00' and IsPrerelease eq false&$orderby=Published";
        private static string _dstPath;
        private static int _count = 100; //each "page" in the NuGet feed is max 100 entries
        private static int _totalPackages = 0;

        static void Main(string[] args)
        {
            ReplicatorInitiailizer();
            ReplicateNugetRepository();

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void ReplicatorInitiailizer()
        {
            _dstPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "NugetReplicator");

            if (!Directory.Exists(_dstPath))
                Directory.CreateDirectory(_dstPath);

            _totalPackages = GetTotalPackageCount();
        }

        private static void ReplicateNugetRepository()
        {
            string feedUrl = $"{ServiceUrlBase}{FeedParameters}";            
            while (feedUrl != null)
            {
                feedUrl = DownloadPackagesEntries(feedUrl);
            }
        }

        private static int GetTotalPackageCount()
        {
            var url = $"{ServiceUrlBase}/$count{FeedParameters}";
            using (var client = new WebClient())
            {
                var total = client.DownloadString(url);
                return int.Parse(total);
            }
        }

        private static string DownloadPackagesEntries(string feedUrl)
        {
            using (var client = new WebClient())
            {
                string resp = client.DownloadString(feedUrl);
                XElement feed = XDocument.Parse(resp).Root;

                var entries = from e in feed.Elements()
                              where e.Name.LocalName == "entry"
                              select e;

                Parallel.ForEach(entries, new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4
                }, DownloadPackage);

                return GetNextPageLink(feed);
            }
        }

        private static string GetNextPageLink(XElement feed)
        {
            //if there are more OData "pages" of packages to download, there will be a <link rel="next" ... 
            //with the source
            var nextlink = feed.Elements().SingleOrDefault(elm => elm.Name.LocalName == "link" &&
                                                                  elm.Attributes().Any(attr => attr.Value == "next"));
            return nextlink == null ? null : GetAttributeValue(nextlink, "href");
        }
        
        private static void DownloadPackage(XElement entry)
        {
            using (var client = new WebClient())
            {
                XElement properties = GetNode(entry, "properties");
                string id = GetNodeValue(properties, "Id");
                if (!NugetNameFilter.isOK(id))
                    return;

                string version = GetNodeValue(properties, "Version");
                string srcUrl = GetAttributeValue(entry, "content", "src");            
                string trgFile = $"{_dstPath}/{id}.{version}.nupkg";

                if (!File.Exists(trgFile))
                {
                    Console.WriteLine($"download {trgFile}");
                    client.DownloadFile(srcUrl, trgFile);
                }
            }
        }

        #region xml page XDocument parsers

        private static string GetAttributeValue(XElement element, string nodeName, string attributeName)
        {
            var node = GetNode(element, nodeName);

            var hit = from e in node.Attributes()
                      where e.Name.LocalName == attributeName
                      select e;

            return hit.SingleOrDefault()?.Value;
        }

        private static string GetAttributeValue(XElement element, string name)
        {
            var hit = from e in element.Attributes()
                      where e.Name.LocalName == name
                      select e;
            return hit.SingleOrDefault()?.Value;
        }

        private static XElement GetNode(XContainer element, string nodeName)
        {
            var hit = from e in element.Elements()
                      where e.Name.LocalName == nodeName
                      select e;
            return hit.SingleOrDefault();
        }

        private static string GetNodeValue(XElement element, string nodeName)
        {
            var hit = from e in element.Elements()
                      where e.Name.LocalName == nodeName
                      select e;
            return hit.SingleOrDefault()?.Value;
        }

        #endregion
    }
}
