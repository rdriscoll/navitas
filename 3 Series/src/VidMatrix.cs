using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.DM;                // DM
using Crestron.SimplSharpPro.DM.Cards;
using Crestron.SimplSharpPro.DM.Endpoints;
using Crestron.SimplSharpPro.DM.Endpoints.Transmitters;

namespace Navitas
{
    public class VidMatrix
    {
        public DmMd8x8 dmMd8x8;
        public DmMd16x16 dmMd16x16;
        public Dictionary<ushort, DmTx200Base> wps;
    }
}