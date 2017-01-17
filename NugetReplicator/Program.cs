﻿using NugetReplicator.Filters;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using log4net;
using log4net.Config;
using System.Threading;
using log4net.Appender;
using System.Diagnostics;

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
        //private const string FeedParameters = "?$filter=VersionDownloadCount gt 1000 and Published ge DateTime'1900-01-01T00:00:00' and Published le DateTime'1901-01-01T00:00:00' and IsPrerelease eq false&$orderby=Published";
        // fetch all nugets which curr ver downloaded more than X and published date >= start date and published date < end date and is a release ver
        private const string FeedParametersTmplate = "?$filter=VersionDownloadCount gt {0} and Published ge DateTime'{1}' and Published lt DateTime'{2}' and IsPrerelease eq false&$orderby=Published";
        private static string _dstPath;
        private static int _totalPackages = 0;
        private static readonly ILog _logFilesRep = LogManager.GetLogger("ReplicatorLogger");
        private static readonly ILog _log = LogManager.GetLogger("GeneralLogger");
        private const int MIN_VER_DOWNLOADED_COUNT = 1000;
        private static string FeedParameters;

        static void Main(string[] args)
        {
            try
            {
                XmlConfigurator.Configure();
                
                #if DEBUG
                    string[] testDates = new string[2]{ "28/12/2016", "29/12/2016" };
                    GetDatesRangeParams(testDates);
                #else
                    GetDatesRangeParams(args);
                #endif

                _log.Info($"Start replicator at {DateTime.Now}");

                bool isFirstRun = ReplicatorInitiailizer();
                ReplicateNugetRepository(isFirstRun);

                _log.Info($"Finish replicator at {DateTime.Now}");
                Console.WriteLine("Finish replicator process");
            }
            catch(Exception ex)
            {
                Console.WriteLine($"App Exception - for more details see log file");
                _log.Error($"Exception - {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }
            finally
            {
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
            }
        }

        private static void GetDatesRangeParams(string[] args)
        {
            DateTime fromDate = new DateTime();
            DateTime tillDate = new DateTime();

            if (args.Count() != 2)
            {
                _log.Error("Params count error");
                Console.WriteLine("Params count error");
                Console.ReadLine();
                Environment.Exit(0);
            }

            if (!DateTime.TryParse(args[0], out fromDate) ||
                !DateTime.TryParse(args[1], out tillDate))
            {
                _log.Error("Date params error");
                Console.WriteLine("Date params error");
                Console.ReadLine();
                Environment.Exit(0);
            }

            // get date params in format 2015-1-29
            // add 1 day so start date from 00:00 till end date at 00:00 (include all end date)
            tillDate = tillDate.AddDays(1);
            FeedParameters = string.Format(FeedParametersTmplate, MIN_VER_DOWNLOADED_COUNT,
                                           fromDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                                           tillDate.ToString("yyyy-MM-ddTHH:mm:ss"));
        }

        private static bool ReplicatorInitiailizer()
        {
            bool retval = false;

            _dstPath = Path.Combine(Environment.CurrentDirectory, "NugetRepo");
            if (!Directory.Exists(_dstPath))
            {
                Directory.CreateDirectory(_dstPath);
                retval = true;
            }

            _totalPackages = GetTotalPackageCount();
            _log.Info($"Replicate {_totalPackages} packages");
            Console.WriteLine($"Start replicate {_totalPackages} packages");
            Thread.Sleep(3000);

            return retval;
        }

        private static void ReplicateNugetRepository(bool isFirstRun)
        {
            Stopwatch timer= new Stopwatch();
            timer.Start();

            if(isFirstRun)
                _logFilesRep.Info(@"{""files"":[");

            string feedUrl = $"{ServiceUrlBase}{FeedParameters}";
            _log.Info($"download url {feedUrl}");
                      
            while (feedUrl != null)
            {
                feedUrl = DownloadPackagesEntries(feedUrl);
            }

            timer.Stop();
            Console.WriteLine($"download took: {timer.ElapsedMilliseconds} mills");
            _log.Info($"download took: {timer.ElapsedMilliseconds} mills");

            timer.Reset();
            timer.Start();
            DeleteLastCharOfFile();
            _logFilesRep.Info($"],\"general_data\":{{\"url\":\"{ServiceUrlBase}\",\"download_time\":\"{DateTime.Now}\"}}}}");
            timer.Stop();

            Console.WriteLine($"finialize took: {timer.ElapsedMilliseconds} mills");
            _log.Info($"finialize took: {timer.ElapsedMilliseconds} mills");
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
                string version = GetNodeValue(properties, "Version");

                if (!NugetNameFilter.isOK(id))
                {
                    _log.Error($"NugetNameFilter error {id}.{version}.nupkg");
                    return;
                }

                string srcUrl = GetAttributeValue(entry, "content", "src");
                string name = $"{id}.{ version}.nupkg";
                string trgFile = $"{_dstPath}/{name}";

                if (!File.Exists(trgFile))
                {
                    Console.WriteLine($"download {trgFile}");
                    client.DownloadFile(srcUrl, trgFile);

                    string packageHash = GetNodeValue(properties, "PackageHash");
                    _logFilesRep.Info($"{{\"name\": \"{name}\",\"extra_data\":{{\"hash\":\"{packageHash}\",\"hash_algorithm\":\"SHA512\", \"id\":\"{id}\",\"version\":\"{version}\"}}}},");
                }
            }
        }

        //  fix json - remove the last , from replicator_logs.metadata so it will be a correct json
        private static void DeleteLastCharOfFile()
        {
            var logfile = _logFilesRep.Logger.Repository.GetAppenders().OfType<RollingFileAppender>().LastOrDefault();
            string text = File.ReadAllText(logfile.File);
            text = text.Remove(text.LastIndexOf(","), 1);
            File.WriteAllText(logfile.File, text);
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
