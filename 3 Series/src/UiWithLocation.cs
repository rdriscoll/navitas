using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro.UI;
using Crestron.SimplSharpPro.DeviceSupport;

namespace Navitas
{
    public class UiWithLocation
    {
        public Tsw750 device;
        private ControlSystem cs;

        private byte id;
        private byte ipid;
        private byte currentPage;
        //private ushort micIndex;

        public CTimer volTimer;
        public Direction volDirection;
        public long volTimerInterval = 200;
        public long volTimerDueTime = 500;

        public CTimer passTimer;
        public long passTimerInterval = 5000;

        public ushort CurrentDeviceControl { get; set; }
        public bool AvSrcOffSelected { get; set; }
        public byte SelectedAvSrc { get; set; }
        public List<bool> AvDestSelected { get; set; }

        // delegates
        public BasicTriListWithSmartObject Device { get { return this.device; } }

        public byte Id { get { return this.id; } }
        public byte Ipid { get { return this.ipid; } }
        public Location location { get; set; }
        //public bool MicSub;
        //public bool LightsSub;
        //public bool AdvancedMode;

        public UiWithLocation(byte id, byte ipid, Location location, ControlSystem cs)
        {
            this.cs = cs;
            device = new Tsw750(ipid, cs);
            device.Description = "TSW-750";
            //device.Name = "Touch panel";
            this.id = id;
            this.ipid = ipid;
            AvDestSelected = new List<bool>();
            this.location = location;
        }

        public void Dispose()
        {
            if (volTimer != null)
            {
                volTimer.Stop();
                volTimer.Dispose();
            }
            if (passTimer != null)
            {
                passTimer.Stop();
                passTimer.Dispose();
            }
            device.Dispose();
        }

        public void SetCurrentPage(byte page)
        {
            currentPage = page;
        }

        public void StartPassTimer()
        {
            if (passTimer == null)
            {
                CrestronConsole.PrintLine("     Creating StartPassTimer");
                passTimer = new CTimer(passTimerExpired, this, passTimerInterval);
            }
            else
            {
                CrestronConsole.PrintLine("     Resetting StartPassTimer");
                passTimer.Reset(passTimerInterval);
            }
        }

        public void DoVol(Direction dir)
        {
            volDirection = dir;
            if (dir == Direction.STOP)
            {
                if (volTimer != null)
                    volTimer.Stop();
            }
            else
            {
                cs.NudgeVol(location, volDirection);
                if (volTimer == null)
                {
                    CrestronConsole.PrintLine(" creating volTimer");
                    volTimer = new CTimer(volTimerExpired, this, volTimerDueTime, volTimerInterval);
                }
                else
                {
                    CrestronConsole.PrintLine(" resetting volTimer");
                    volTimer.Reset(1, volTimerInterval);
                }
            }
        }

        private void volTimerExpired(object obj)
        {
            try
            {
                if (volTimer != null)
                {
                    CrestronConsole.PrintLine(" volTimerExpired != null {0}", id);
                    cs.RampVol(location, volDirection);
                }
                else
                    CrestronConsole.PrintLine(" volTimerExpired == null {0}", id);
            }
            catch (Exception e)
            {
                    CrestronConsole.PrintLine(" volTimerExpired {0} {1}", id, e.Message);
            }
        }

        private void passTimerExpired(object obj)
        {
            try
            {
                CrestronConsole.PrintLine("     passTimerExpired");
                passTimer.Stop();
                CrestronConsole.PrintLine("     Stopped StartPassTimer");
                cs.ClearPassText(location);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("passTimerExpired exception: {0}", e.ToString());
            }
        }

    }
}