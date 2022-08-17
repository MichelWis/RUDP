using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace BasedRUDP
{
    public class RemoteNetworkInstance
    {
        public IPEndPoint EndPoint { get; set; }
        public UInt64 LastComTimepoint { get; set; }

        public RemoteNetworkInstance(IPEndPoint _ep, UInt64 _tickCount)
        {
            EndPoint = _ep;
            LastComTimepoint = _tickCount;
        }
    }
}
