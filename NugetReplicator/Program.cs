using NugetReplicator.Filters;
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
using SharpConfig;
using System.Security.Cryptography;

//private const string FeedUrlBase = "https://www.myget.org/F/dotnet-core/api/v2";
//IsLatestVersion - without beta
//IsAbsoluteLatestVersion - include beta
namespace NugetReplicator
{
    class Program
    {
        private const string ServiceUrlBase = "https://www.nuget.org/api/v2/Packages";
        private static string _dstPath;
        private static bool _isNewDownload = false;
        private static readonly ILog _logFilesRep = LogManager.GetLogger("ReplicatorLogger");
        private static readonly ILog _log = LogManager.GetLogger("GeneralLogger");

        static void Main(string[] args)
        {
            try
            {
                Configuration conf = Configuration.LoadFromFile("ReplicatorSettings.cfg");
                SysSettings sysSettings = new SysSettings(conf["Sys"]);
                string basePath = InitializeDownloadDir(sysSettings);
                string filename = sysSettings.Hash ? "Hash" : "Replicator";
                log4net.GlobalContext.Properties["MetaDataLogFileName"] = $"{basePath}\\{filename}"; //log file path
                log4net.GlobalContext.Properties["GeneralLogFileName"] = $"{basePath}\\log"; //log file path
                XmlConfigurator.Configure();

                _log.Info($"Start {filename} at {DateTime.Now}");

                ReplicatorInitiailizer(sysSettings);
                ReplicateNugetRepository(sysSettings);

                _log.Info($"Finish replicator at {DateTime.Now}");
                Console.WriteLine("Finish replicator process");

                if (sysSettings.Hash)
                {
                    _log.Info($"Increase DownloadID param by 1 to  {++sysSettings.DownloadID}");
                    conf["Sys"]["DownloadID"].IntValue = sysSettings.DownloadID;
                    conf.SaveToFile("ReplicatorSettings.cfg");
                    _log.Info($"Remove hash dir {_dstPath}");
                    Directory.Delete(_dstPath, true);
                }
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

        private static string InitializeDownloadDir(SysSettings sysSettings)
        {
            string basePath = Path.Combine(sysSettings.DownloadDirectory, sysSettings.DownloadID.ToString());
            _dstPath = Path.Combine(basePath, sysSettings.Hash?"Hash":"Nugets");
            if (!Directory.Exists(_dstPath))
            {
                Directory.CreateDirectory(_dstPath);
                _isNewDownload = true;
            }

            return basePath;
        }

        private static void ReplicatorInitiailizer(SysSettings sysSettings)
        {
            if(_isNewDownload)
                _logFilesRep.Info(@"{""files"":[");

            int totalPackages = GetTotalPackageCount(sysSettings);
            _log.Info($"Replicate {totalPackages} packages");
            Console.WriteLine($"Start replicate {totalPackages} packages");
            Thread.Sleep(2000);
        }

        private static void ReplicateNugetRepository(SysSettings sysSettings)
        {
            Stopwatch timer= new Stopwatch();
            timer.Start();

            string feedUrl = $"{ServiceUrlBase}{sysSettings.FeedUrl}";
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

        private static int GetTotalPackageCount(SysSettings sysSettings)
        {
            var url = $"{ServiceUrlBase}/$count{sysSettings.FeedUrl}";
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

                    string packageHash = CalcMD5ForFile(trgFile);
                    _logFilesRep.Info($"{{\"name\": \"{name}\",\"extra_data\":{{\"hash\":\"{packageHash}\",\"hash_algorithm\":\"MD5\", \"id\":\"{id}\",\"version\":\"{version}\"}}}},");
                }
            }
        }

        private static string CalcMD5ForFile(string filePath)
        {           
            using (var md5 = MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(filePath))).Replace("-", string.Empty);
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

        private static string GetNextPageLink(XElement feed)
        {
            //if there are more OData "pages" of packages to download, there will be a <link rel="next" ... 
            //with the source
            var nextlink = feed.Elements().SingleOrDefault(elm => elm.Name.LocalName == "link" &&
                                                                  elm.Attributes().Any(attr => attr.Value == "next"));
            return nextlink == null ? null : GetAttributeValue(nextlink, "href");
        }

        #endregion
    }
}
