using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace H.OpenVpn
{
    public struct InOutBytes
    {
        public InOutBytes(long bytesIn, long bytesOut)
        {
            BytesIn = bytesIn;
            BytesOut = bytesOut;
        }

        public long BytesIn { get; }

        public long BytesOut { get; }

        public static InOutBytes Zero { get; } = new InOutBytes(0, 0);

        public static InOutBytes operator -(InOutBytes op1, InOutBytes op2)
        {
            return new InOutBytes(op1.BytesIn - op2.BytesIn, op1.BytesOut - op2.BytesOut);
        }
    }
}
