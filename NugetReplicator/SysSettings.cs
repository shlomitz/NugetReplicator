using SharpConfig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NugetReplicator
{
    // fetch all nugets which curr ver downloaded more than X and published date >= start date and published date < end date and is a release ver
    // result 0
    //private const string FeedParameters = "?$filter=VersionDownloadCount gt 1000 and Published ge DateTime'2015-12-29T09:13:28' and Published le DateTime'2015-12-29T09:13:28' and IsPrerelease eq false&$orderby=Published";
    // result 1
    //private const string FeedParameters = "?$filter=VersionDownloadCount gt 1000 and Published ge DateTime'2015-12-29T09:13:28' and Published le DateTime'2015-12-29T10:13:28' and IsPrerelease eq false&$orderby=Published";
    //private const string FeedParameters = "?$filter=VersionDownloadCount gt 1000 and Published ge DateTime'1900-01-01T00:00:00' and Published le DateTime'1901-01-01T00:00:00' and IsPrerelease eq false&$orderby=Published";
    public class SysSettings
    {
        private const string FeedParametersTmplate = "?$filter=VersionDownloadCount gt {0} and Published ge DateTime'{1}' and Published lt DateTime'{2}' and IsPrerelease eq false&$orderby=Published";
        string _feedUrl = String.Empty;

        #region ctor

        public SysSettings(Section section)
        {
            DownloadDirectory = section["DownloadDirectory"].StringValue;
            DownloadID = section["DownloadID"].IntValue;
            Hash = section["Hash"].BoolValue;

            GenerateFeed(section);
        }

        #endregion

        #region private methods

        private void GenerateFeed(Section section)
        {
            DateTime fromDate = new DateTime();
            DateTime tillDate = new DateTime();

            if (!DateTime.TryParse(section["FromDate"].StringValue, out fromDate) ||
                !DateTime.TryParse(section["TillDate"].StringValue, out tillDate))
            {
                throw new ArgumentException("Date params exception - can't convert date params to DateTime");
            }

            // get date params in format 29/01/2015
            // add 1 day so query include all start date and till date
            // query start from "FromDate" at 00:00 till "TillDate" at 00:00 (include all TillDate)
            tillDate = tillDate.AddDays(1);
            _feedUrl = string.Format(FeedParametersTmplate,
                                     section["MinVerDownloadCount"].StringValue,
                                     fromDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                                     tillDate.ToString("yyyy-MM-ddTHH:mm:ss"));
        }

        #endregion

        #region Properties

        public string DownloadDirectory { get; set; }
        public int DownloadID { get; set; }
        public bool Hash { get; set; }
        public String FeedUrl
        {
            get
            {
                return _feedUrl;
            }
        }

        #endregion
    }
}