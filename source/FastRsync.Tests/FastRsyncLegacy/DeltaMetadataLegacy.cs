using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastRsync.Tests.FastRsyncLegacy
{
    internal class DeltaMetadataLegacy
    {
        public string HashAlgorithm { get; set; }
        public string ExpectedFileHashAlgorithm { get; set; }
        public string ExpectedFileHash { get; set; }
    }
}
