using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperSMBServer
{
    [Flags]
    enum SMBProtocol
    {
        SMB1 = 1,
        SMB2 = 2,
        SMB3 = 4
    }
}
