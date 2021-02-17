using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperSMBServer
{
    public class AggregatedShareSettings : ShareSettings
    {
        public List<string> SharePaths { get; set; }

        //public AggregatedShareSettings(string shareName, string sharePath, List<string> readAccess, List<string> writeAccess) :
        //    this(shareName, new List<string>() { sharePath }, readAccess, writeAccess) { }

        public AggregatedShareSettings(string shareName, List<string> sharePaths, List<string> readAccess, List<string> writeAccess) :
            base(shareName, sharePaths[0], readAccess, writeAccess) {
            SharePaths = sharePaths;
            ReadAccess = readAccess;
            WriteAccess = writeAccess;
        }
    }
}
