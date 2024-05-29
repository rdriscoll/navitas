using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Http;

namespace Navitas
{
    public class PanasonicCam
    {
        private ushort panSpeed = 25;//49;
        private ushort tiltSpeed = 25;//49;
        private ushort panPad = 49;
        private ushort tiltPad = 49;
        private string url = "192.168.0.10"; // default when they don't get a DHCP lease
        private CTimer panTiltTimer;
        private long panTiltTimerInterval = 100;
        private bool _currentPower = false;
        private string name = "Panasonic Camera";
        private string user = String.Empty;//"admin"; // todo: set way to change from default "admin"
        private string password = String.Empty;// = "12345"; // todo: set way to change from default "12345"

        // Public delegates
        public delegate void errorHandler(SimplSharpString errMsg);
        public errorHandler OnError { get; set; }

        public delegate void OnResponse(SimplSharpString strResponse);
        public OnResponse ResponseFn { get; set; }

        public event EventHandler PowerFb;
        //public event EventHandler PowerOnFb;
        //public event EventHandler PowerOffFb;

        protected CTimer pollTimer;
        public int pollTimerRepeat = 5000;
        private bool deviceBusy;

        protected void pollTimerExpired(object obj)
        {
            //            CrestronConsole.PrintLine("PollCam");
            //            PanLeft();
        }

        public PanasonicCam(string url)
        {
            this.url = url;
            deviceBusy = false;
            pollTimer = new CTimer(pollTimerExpired, this, 1, pollTimerRepeat);
        }

        public void Dispose()
        {
            //CrestronConsole.PrintLine("{0} {1} Dispose", name, url);
            if (pollTimer != null)
            {
                pollTimer.Stop();
                pollTimer.Dispose();
            }
            //CrestronConsole.PrintLine("{0} {1} Dispose done", name, url);
        }

        // Private methods
        private void SetPtz(String strCommand)
        {
            HttpClient httpClient = new HttpClient();
            HttpClientRequest httpRequest = new HttpClientRequest();
            String strHttpString;

            if (deviceBusy) return;
            deviceBusy = true;

            if (url.Length < 1)
                return;
            try
            {
                httpClient.KeepAlive = false;
                httpClient.Timeout = 1;
                httpClient.TimeoutEnabled = true;
                if (user != null && user.Length > 0)
                    httpClient.UserName = user;
                if (password != null && password.Length > 0)
                    httpClient.Password = password;
                strHttpString = String.Format("http://" + url + "/cgi-bin/aw_ptz?cmd=%23" + strCommand + "&res=1");
                Debug("httpRequest: " + strHttpString);
                httpRequest.Header.SetHeaderValue("User-Agent", "Mozilla/5.0");
                httpRequest.Header.SetHeaderValue("Upgrade-Insecure-Requests", "1");
                httpRequest.Header.SetHeaderValue("Cookie", "Session=0");
                httpRequest.Header.ContentType = "text/xml";
                httpRequest.Url.Parse(strHttpString);
                httpClient.DispatchAsync(httpRequest, httpCallBackTransferResponse);
            }
            catch (Exception e)
            {
                Debug("httpRequest Failed");
                deviceBusy = false;
                CrestronConsole.PrintLine("{0} {1}\n{2}", name, e.ToString(), e.StackTrace);
                if (OnError != null)
                    OnError(new SimplSharpString(e.ToString() + "\n\r" + e.StackTrace));
            }
        }
/*
GET http://10.4.136.161/cgi-bin/aw_ptz?cmd=%23PTS5050&res=1 HTTP/1.1
Host: 10.4.136.161
Connection: keep-alive
Cache-Control: max-age=0
Upgrade-Insecure-Requests: 1
User-Agent: Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/53.0.2785.113 Safari/537.36
Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,* /*;q=0.8
Accept-Encoding: gzip, deflate, sdch
Accept-Language: en-US,en;q=0.8
Cookie: Session=0
*/
        void httpCallBackTransferResponse(HttpClientResponse httpResponse, HTTP_CALLBACK_ERROR error)
        {
            if (httpResponse != null)
            {
                if (httpResponse.ContentString.Length < 60)
                    Debug("httpResponse: " + httpResponse.ContentString);
                else
                    Debug(String.Format("httpResponse, len:{0}", name, httpResponse.ContentString.Length));
                switch (httpResponse.ContentString)
                {
                    case "p1":
                    case "p0":
                        _currentPower = httpResponse.ContentString.Contains("1");
                        if (PowerFb != null)
                            PowerFb(_currentPower, new EventArgs());
                        break;
                }
            }
            Debug("httpRequest Ended");
            deviceBusy = false;
            return;
        }

        private void Debug(String strArg)
        {
            CrestronConsole.PrintLine("{0} {1}", name, strArg);
            if (ResponseFn != null)
                ResponseFn(strArg);
        }

        private int ConvertRange(int nArg, int nInMin, int nInMax, int nOutMin, int nOutMax)
        {
            int nInRange_ = nInMax - nInMin;
            int nOutRange_ = nOutMax - nOutMin;
            int nReturn_ = (int)((nArg - nInMin) * nOutRange_) / nInRange_ + nOutMin;
            return nReturn_;
        }

        // Public Methods

        public void SetUrl(String strArg)
        {
            url = strArg;
        }

        public void SetPanSpeed(ushort arg)
        {
            panSpeed = (ushort)ConvertRange((int)arg, 0, 65535, 1, 49);
        }

        public void SetTiltSpeed(ushort arg)
        {
            tiltSpeed = (ushort)ConvertRange((int)arg, 0, 65535, 1, 49);
        }

        // PT
        public void PanLeft()
        {
            SetPtz("PTS" + (50 - panSpeed).ToString("00") + "50");
        }
        public void PanRight()
        {
            SetPtz("PTS" + (50 + panSpeed).ToString("00") + "50");
        }
        public void TiltUp()
        {
            SetPtz("PTS50" + (50 + tiltSpeed).ToString("00"));
        }
        public void TiltDown()
        {
            SetPtz("PTS50" + (50 - tiltSpeed).ToString("00"));
        }
        public void PanTiltStop()
        {
            if (panTiltTimer != null)
                panTiltTimer.Stop();
            SetPtz("PTS5050");
        }

        public void PanPad(ushort arg)
        {
            panPad = (ushort)ConvertRange((int)arg, 0, 65535, 1, 99);
        }
        public void TiltPad(ushort arg)
        {
            tiltPad = (ushort)ConvertRange((int)arg, 0, 65535, 1, 99);
        }
        public void PanTiltPad()
        {
            SetPtz("PTS" + panPad.ToString("00") + tiltPad.ToString("00"));
            if (panTiltTimer == null)
                panTiltTimer = new CTimer(timerExpired, this, 1, panTiltTimerInterval);
            else
                panTiltTimer.Reset(1, panTiltTimerInterval);
        }
        private void timerExpired(object obj)
        {
            if (panTiltTimer != null)
                SetPtz("PTS" + panPad.ToString("00") + tiltPad.ToString("00"));
            Debug("timerExpired: PTS" + panPad.ToString("00") + tiltPad.ToString("00"));
        }

        // Zoom
        public void ZoomIn()
        {
            SetPtz("Z80");
        }
        public void ZoomOut()
        {
            SetPtz("Z20");
        }
        public void ZoomStop()
        {
            SetPtz("Z50");
        }
        public void ZoomHome()
        {
            SetPtz("AXZ555");
        }

        // power
        public void PowerOn()
        {
            SetPtz("On");
        }
        public void PowerOff()
        {
            SetPtz("Of");
        }
        public void PowerToggle()
        {
            SetPtz(_currentPower?"Of":"On");
        }

        // Focus
        public void FocusNear()
        {
            SetPtz("F3");
        }
        public void FocusFar()
        {
            SetPtz("F70");
        }
        public void FocusStop()
        {
            SetPtz("F50");
        }

        // Preset
        public void PresetRecall(ushort arg)
        {
            SetPtz("R" + arg.ToString("00"));
        }
        public void PresetStore(ushort arg)
        {
            SetPtz("M" + arg.ToString("00"));
        }
        public void PresetHome()
        {
            SetPtz("APC80008000");
        }
    }
}