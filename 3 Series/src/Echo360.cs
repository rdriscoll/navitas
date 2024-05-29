using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using Newtonsoft.Json;
using Crestron.SimplSharp;
using Crestron.SimplSharp.Net.Http;
using Crestron.SimplSharp.Net.Https;
using Crestron.SimplSharp.CrestronXmlLinq;

namespace Navitas
{
    public delegate void StringEventHandler(object sender, StringEventArgs e);
    public delegate void EchoHardwareDetailsEventHandler(object sender, EchoHardwareDetailsEventArgs e);
    public delegate void EchoMonitoringEventHandler(object sender, EchoMonitoringEventArgs e);
    public delegate void EchoStatusEventHandler(object sender, EchoStatusEventArgs e);

    public class Echo360
    {
        // Public delegates
        public event EchoStatusEventHandler StatusFb;
        public event EchoHardwareDetailsEventHandler HardwareFb;
        public event EchoMonitoringEventHandler MonitoringFb;
        public StringEventHandler ResponseFn { get; set; }

        // Private variables
        public CTimer pollTimer;
        public long pollTimerInterval = 1000; // don't change from 1 second for feedback and countdown reasons
        private byte _currentPoll = 0;
        private byte _lastPoll = 0;
        private String _currentCommand = String.Empty;
        private String _queuedCommand = String.Empty;
        private String _queuedPath = String.Empty;
        public uint remainingSeconds = 0;
        DateTime dtStart;
        DateTime dtClock;
        private const byte POLL_MONITORING = 1;
        private const byte POLL_CURRENT_CAPTURE = 5;
        private const byte POLL_SYSTEM = 9;
        private const byte POLL_RESET = 11;

        public String IPAddress;
        public String Username;
        public String Password;
        public ushort IPPort;

        public EchoStatus status = new EchoStatus();
        public EchoHardwareDetails hardwareDetails = new EchoHardwareDetails();
        public EchoMonitoring monitoring = new EchoMonitoring();

        private string url;
        private string name = "Echo 360";
        private int deviceBusy = 0;
        private ushort retries = 0;

        HttpClient httpClient;        
        
        public Echo360(string url)
        {
            if (IPPort == 0)
            this.url = url;
            this.IPAddress = url;
            deviceBusy = 0;
            if (pollTimer == null)
                pollTimer = new CTimer(pollTimerExpired, this, pollTimerInterval, pollTimerInterval);
            else
                pollTimer.Reset(pollTimerInterval, pollTimerInterval);
        }
        public void Dispose()
        {
            if (pollTimer != null)
            {
                pollTimer.Stop();
                pollTimer.Dispose();
            }
            if (httpClient != null)
                httpClient.Abort();
        }
        private void pollTimerExpired(object obj)
        {
            try
            {
                //CrestronConsole.PrintLine("{0} pollTimerExpired, busy: {1}, currentPoll: {2}", name, deviceBusy, _currentPoll);
                dtClock = dtClock.AddSeconds(1);
                if (pollTimer != null && deviceBusy < 1)
                {
                    _currentPoll++;
                    //CrestronConsole.PrintLine("{0} _currentPoll : {1}", name, _currentPoll);
                    switch (_currentPoll)
                    {
                        case POLL_CURRENT_CAPTURE: GetCurrent(); break;
                        case POLL_SYSTEM         : GetSystemStatus(); break;
                        case POLL_RESET          : _currentPoll = 0; break;
                        default :
                            if (_currentPoll % 2 == 0)
                                GetMonitoringStatus(); 
                            break;
                    }
                }
                //CrestronConsole.PrintLine("{0} pollTimerExpired: MonitoringFb: {1}, _lastPoll: {2}, monitoring.state: {3}", name, MonitoringFb, _lastPoll, monitoring.state);
                if (MonitoringFb != null && _lastPoll != POLL_MONITORING && monitoring.state == "paused")
                    MonitoringFb(this, new EchoMonitoringEventArgs(monitoring));
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} pollTimerExpired: {1}", name, e.ToString());
            }
        }
        public void SendHttpCommand(String RequestTypeIn, String Path, String Command)
        {
            httpClient = new HttpClient();
            HttpClientRequest httpRequest = new HttpClientRequest();
            String strHttpString;

            if (deviceBusy > 0)
            {
                if (RequestTypeIn == "POST")
                {
                    _queuedCommand = Command;
                    _queuedPath = Path;
                }
                if (retries < 5)
                {
                    retries++;
                    return;
                }
            }
            retries = 0;
            deviceBusy++;

            try
            {
                if (url != null)
                {
                    httpClient.KeepAlive = false;
                    httpClient.Timeout = 3;
                    httpClient.TimeoutEnabled = true;
                    if (Username != null && Username.Length > 0)
                        httpClient.UserName = Username;
                    if (Password != null && Password.Length > 0)
                        httpClient.Password = Password;

                    if (IPPort == 80)
                        strHttpString = String.Format("http://{0}{1}", url, Path);
                    else
                        strHttpString = String.Format("http://{0}:{1}{2}", url, IPPort, Path);
                    HttpHeaders headers = new HttpHeaders();
                    headers.SetHeaderValue("Authorization", String.Format("Basic " + System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(Username + ":" + Password))));
                    httpRequest.Header = headers;
                    httpRequest.RequestType = Crestron.SimplSharp.Net.Http.RequestType.Post;
                    httpRequest.Url.Parse(strHttpString);
                    httpRequest.ContentString = Uri.EscapeDataString(Command);
                    _currentCommand = String.Format("{0} {1}, {2}", RequestTypeIn, strHttpString, Command);
                    CrestronConsole.PrintLine("{0} DispatchAsync: {1}", name, _currentCommand);
                    httpClient.DispatchAsync(httpRequest, httpCallBackTransferResponse);
                    _queuedCommand = String.Empty;
                    _queuedPath = String.Empty;
                }
            }
            catch (Exception e)
            {
                //Debug("httpRequest Failed");
                deviceBusy--;
                CrestronConsole.PrintLine("{0} {1}\n{2}", name, e.Message, e.StackTrace);
            }
        }
        void httpCallBackTransferResponse(HttpClientResponse httpResponse, HTTP_CALLBACK_ERROR error)
        {
            try
            {
                if (httpResponse != null)
                    ParseResponse(httpResponse);
                //Debug("httpRequest Ended");
                deviceBusy--;
                if (deviceBusy < 1 && !String.IsNullOrEmpty(_queuedCommand))
                    SendHttpCommand("POST", _queuedCommand, _queuedPath);
                return;
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} httpCallBackTransferResponse: {1}", name, e.ToString());
            }
        }
        private void Debug(String strArg)
        {
            CrestronConsole.PrintLine("{0} {1}", name, strArg);
            if (ResponseFn != null)
                ResponseFn(this, new StringEventArgs(strArg));
        }
        #region response
        public void ParseProtocolExample(XDocument doc) // the example is innacurate
        {
            string text = String.Empty;
            IEnumerable<XElement> xe;
            //text += GetDocumentString(doc, "title"); // status.current.schedule.parameters.title
            //text += PrintXElementStrings(doc.Elements("status").Elements("current").Elements("schedule").Elements("parameters")
            //                                .Elements("title"));
            xe = doc.Root.Descendants("parameters");  // status.current.schedule.parameters
            status.title = PrintXElementStrings(xe.Elements("title"));
            status.presenter = PrintXElementStrings(xe.Elements("presenters").Elements("presenter"));

            xe = doc.Root.Descendants("current");  // status.current
            status.duration = PrintXElementStrings(xe.Elements("duration"));
            status.state = PrintXElementStrings(xe.Elements("state"));
            status.outputType = PrintXElementStrings(xe.Elements("output-type"));

            status.error = PrintXElementStrings(xe.Elements("Error"));
            status.rpm = PrintXElementStrings(xe.Elements("rpm"));

            //Current schedule
            EchoSchedule sched = new EchoSchedule();
            xe = doc.Root.Descendants("capture-profile"); // status.current.schedule.parameters.capture-profile
            sched.name = PrintXElementStrings(xe.Elements("name"));
            sched.outputType = PrintXElementStrings(xe.Elements("output-type"));
            xe = doc.Root.Descendants("schedule"); // status.current.schedule
            sched.duration = PrintXElementStrings(xe.Elements("duration"));
            status.schedule = sched;

            //Sources
            List<EchoSource> sources = new List<EchoSource>();
            xe = doc.Root.Descendants("sources").Elements("source"); // status.current.sources.source
            // avoiding: status.current.schedule.parameters.capture-profile.prodicts.product.source name=
            foreach (XElement src in xe)
            {
                EchoSource es = new EchoSource();
                es.name = PrintXElementStrings(xe.Elements("duration"));
                es.className = PrintXElementStrings(xe.Elements("class"));
                IEnumerable<XElement> chs = src.Elements("channels").Elements("channel"); // status.current.sources.source.channels.channel
                foreach (XElement ch in chs)
                {
                    EchoChannel ec = new EchoChannel();
                    ec.position = PrintXElementStrings(xe.Elements("position"));
                    ec.peak = PrintXElementStrings(xe.Elements("peak"));
                    es.chans.Add(ec);
                }
                sources.Add(es);
            }
            status.sources = sources;
            if(StatusFb != null)
                StatusFb(this, new EchoStatusEventArgs(status));
        }
        public void ParseSystem(XDocument doc)
        {
            string text = String.Empty;
            IEnumerable<XElement> xe;
            // get elements no matter where in the DOM. 
            hardwareDetails.wallClockTime = GetDocumentString(doc, "wall-clock-time");
            dtClock = DateTime.Parse(hardwareDetails.wallClockTime);

            hardwareDetails.maxCaptureMinutes = GetDocumentString(doc, "max-capture-duration-minutes");
            hardwareDetails.maxCaptureExtendMinutes = GetDocumentString(doc, "max-live-capture-extend-duration-minutes");
            hardwareDetails.hostAddress = GetDocumentString(doc, "host-address");
            hardwareDetails.serialNumber = GetDocumentString(doc, "serial-number");
            hardwareDetails.systemVersion = GetDocumentString(doc, "system-version");
            hardwareDetails.upSince = GetDocumentString(doc, "up-since");
            hardwareDetails.lastSync = GetDocumentString(doc, "last-sync");
            hardwareDetails.utcOffset = GetDocumentString(doc, "utc-offset");
            hardwareDetails.location = GetDocumentString(doc, "location");
            // get elements in specific locations
            xe = doc.Root.Descendants("api-versions"); // status.api-versions.api-version
            hardwareDetails.apiVersion = PrintXElementStrings(xe.Elements("api-version"));

            xe = doc.Root.Descendants("server-properties");
            hardwareDetails.serverType = PrintXElementStrings(xe.Elements("server-type"));

            xe = doc.Root.Descendants("capture-profiles");
            hardwareDetails.captureProfile = PrintXElementStrings(xe.Elements("capture-profile")); // multiples

            xe = doc.Root.Descendants("monitor-profiles");
            hardwareDetails.monitorProfile = PrintXElementStrings(xe.Elements("monitor-profile")); // multiples

            xe = doc.Root.Descendants("log");
            hardwareDetails.logState = PrintXElementStrings(xe.Elements("state")); // multiples

            dtStart = DateTime.Parse(hardwareDetails.wallClockTime);

            if (HardwareFb != null)
                HardwareFb(this, new EchoHardwareDetailsEventArgs(hardwareDetails));
        }
        public void ParseMonitoring(XDocument doc)
        {
            //CrestronConsole.PrintLine("{0} ParseMonitoring", name);
            string state = monitoring.state;
            //IEnumerable<XElement> xe;
            // get elements no matter where in the DOM. 
            monitoring.state      = GetDocumentString(doc, "state");
            monitoring.startTime  = GetDocumentString(doc, "start-time"); // 2016-06-20T03:06:37.508Z
            string dur = GetDocumentString(doc, "duration");
            int duration = Utils.atoi(dur);
            TimeSpan tsDuration = new TimeSpan((int)(duration / 3600), (int)(duration % 3600 / 60), (int)(duration % 60));
            monitoring.duration = MakeTimeString(tsDuration, false, true, true);
            monitoring.outputType = GetDocumentString(doc, "output-type");
            monitoring.originalDuration = GetDocumentString(doc, "original-duration");
            monitoring.confidenceMonitoring = GetDocumentString(doc, "confidence-monitoring");
            //public List<EchoSource> sources { get; set; }
           //if (state != monitoring.state && MonitoringFb != null)
            GetRemaining(tsDuration);
            if (MonitoringFb != null)
                    MonitoringFb(this, new EchoMonitoringEventArgs(monitoring));
        }
        public void ParseCurrentCapture(XDocument doc)
        {
            string text = String.Empty;
            IEnumerable<XElement> xe;
            // get elements no matter where in the DOM. 
            hardwareDetails.wallClockTime = GetDocumentString(doc, "wall-clock-time");
            dtClock = DateTime.Parse(hardwareDetails.wallClockTime);

            hardwareDetails.maxCaptureMinutes = GetDocumentString(doc, "max-capture-duration-minutes");
            hardwareDetails.maxCaptureExtendMinutes = GetDocumentString(doc, "max-live-capture-extend-duration-minutes");
            // get elements in specific locations
            xe = doc.Root.Descendants("api-versions"); // status.api-versions.api-version
            hardwareDetails.apiVersion = PrintXElementStrings(xe.Elements("api-version"));

            xe = doc.Root.Descendants("server-properties");
            hardwareDetails.serverType = PrintXElementStrings(xe.Elements("server-type"));

            xe = doc.Root.Descendants("capture-profiles");
            hardwareDetails.captureProfile = PrintXElementStrings(xe.Elements("capture-profile")); // multiples

            xe = doc.Root.Descendants("monitor-profiles");
            hardwareDetails.monitorProfile = PrintXElementStrings(xe.Elements("monitor-profile")); // multiples

            xe = doc.Root.Descendants("current");
            //hardwareDetails.state = PrintXElementStrings(xe.Elements("state")); // multiples

            xe = doc.Root.Descendants("parameters");  // status.current.schedule.parameters
            status.title = PrintXElementStrings(xe.Elements("title"));
            status.presenter = PrintXElementStrings(xe.Elements("presenters").Elements("presenter"));
            //if (StatusFb != null)
            if (!String.IsNullOrEmpty(status.title) && StatusFb != null)
                    StatusFb(this, new EchoStatusEventArgs(status));

            if (HardwareFb != null)
                HardwareFb(this, new EchoHardwareDetailsEventArgs(hardwareDetails));
        }
        public void ParseResponse(HttpClientResponse r)
        {
            try
            {
                //if (r.ContentString.Length < 60)
                //    CrestronConsole.PrintLine("{0} httpResponse to command: {1} \n{2}", name, _currentCommand, r.ContentString);
                //else
                //    CrestronConsole.PrintLine("{0} httpResponse (len:{1}) to command: {2}", name, r.ContentString.Length, _currentCommand);
                if (ResponseFn != null)
                {
                    if (r.Code == 401 || r.Code == 403)
                        ResponseFn(this, new StringEventArgs(String.Format("{0}: Unauthorized - need new token", r.Code)));
                    else if (r.Code != 200)
                        ResponseFn(this, new StringEventArgs(String.Format("{0}: Response from web service isn't OK", r.Code)));
                }
                XDocument doc = XDocument.Parse(r.ContentString);
                //CrestronConsole.PrintLine("{0} doc.Root.Name: {1}", name, doc.Root.Name.ToString());
                if (doc.Root.Name.ToString().Equals("status")) // system.xml response from "/status/system"
                {
                    //ParseProtocolExample(doc);
                    switch (_lastPoll)
                    {
                        case POLL_MONITORING     : 
                            ParseMonitoring(doc);
                            break;
                        case POLL_CURRENT_CAPTURE: 
                            ParseCurrentCapture(doc);
                            break;
                        case POLL_SYSTEM         :
                            ParseSystem(doc);
                            break;
                        default: 
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} ParseResponse: {1}", name, e.ToString());
            }
        }
        private string PrintXElementStrings(IEnumerable<XElement> xes)
        {
            string text = String.Empty;
            foreach (XElement xe in xes)
            {
                //text += xe.Name + ": " + xe.Value + "\n";
                if(text.Length > 0)
                    text += "; ";
                text += xe.Value;
            }
            return text;
        }
        private string GetDocumentString(XDocument doc, string ele)
        {
            string text = String.Empty;
            IEnumerable<XElement> xes = doc.Root.Descendants(ele);
            foreach (XElement xe in xes)
                if (xes.Count() > 1)
                    text += "; " + xe.Parent.Name + "." + xe.Name + ": " + xe.Value;
                else
                    text += xe.Value;
                    //text += xe.Name + ": " + xe.Value + "\n";
            return text;
        }
        public void SetUrl(String str)
        {
            url = str;
        }
        public string MakeTimeString(TimeSpan ts, bool showHours, bool showMinutes, bool showSeconds)
        {
            StringBuilder sb = new StringBuilder();
            if (ts.Hours > 0 || showHours)
                sb = sb.Append(string.Format(CultureInfo.CurrentCulture, "{0}", ts.Hours));
            if (showMinutes || ts.Minutes > 0 || sb.Length > 0)
            {
                if (sb.Length > 0)
                {
                    sb = sb.Append(":");
                    sb = sb.Append(string.Format(CultureInfo.CurrentCulture, "{0:00}", ts.Minutes));
                }
                else
                    sb = sb.Append(string.Format(CultureInfo.CurrentCulture, "{0}", ts.Minutes));
            }
            if (showSeconds)
            {
                if (sb.Length > 0)
                    sb = sb.Append(".");
                sb = sb.Append(string.Format(CultureInfo.CurrentCulture, "{0:00}", ts.Seconds));
            }
            return sb.ToString();
        }       
        public void GetRemaining(TimeSpan tsDuration)
        {
            try
            {
                double runtime;
                TimeSpan tsRunTime;
                TimeSpan tsRemain;
 
                switch (monitoring.state)
                {
                    case "active":
                    case "waiting":
                    case "paused":
                        dtStart = DateTime.Parse(monitoring.startTime);
                        CrestronConsole.PrintLine("Clock: {0}", dtClock.ToLongTimeString());
                        //CrestronConsole.PrintLine("Start: {0}", dtStart.ToLongTimeString());
                        tsRunTime = dtClock.Subtract(dtStart);
                        //CrestronConsole.PrintLine("Duration: {0}", MakeTimeString(tsDuration, false, true, true));
                        //CrestronConsole.PrintLine("RunTime: {0}", MakeTimeString(tsRunTime, false, true, true));
                        runtime = tsRunTime.TotalMinutes;
                        tsRemain = tsDuration - tsRunTime;
                        monitoring.remaining = MakeTimeString(tsRemain, false, true, true);
                        //CrestronConsole.PrintLine("remaining: {0}", monitoring.remaining);
                        break;
                    case "complete": break;
                    case "inactive":
                        monitoring.remaining = "0:00";
                        break;
                }
            }
            catch(Exception e)
            {
                CrestronConsole.PrintLine("{0} GetRemaining: {1}", name, e.ToString());
            }
        }

        #endregion
        #region GetCommands
        public void GetSystemStatus()
        {
            _lastPoll = POLL_SYSTEM;
            SendHttpCommand("GET", "/status/system", "");
        }
        public void GetMonitoringStatus()
        {
            _lastPoll = POLL_MONITORING;
            SendHttpCommand("GET", "/status/monitoring", "");
        }
        public void GetCurrent()
        {
            _lastPoll = POLL_CURRENT_CAPTURE;
            SendHttpCommand("GET", "/status/current_capture", "");
        }
        public void Pause()
        {
            CrestronConsole.PrintLine("{0} Pause()", name);
            if (monitoring.state == "paused")
                Resume();
            else
               SendHttpCommand("POST", "/capture/pause", "");
        }
        public void Start()
        {
            SendHttpCommand("POST", "/capture/record", "");
        }
        public void Resume()
        {
           Start();
        }
        public void Stop()
        {
            SendHttpCommand("POST", "/capture/stop", "");
        }
        public void Reboot()
        {
            SendHttpCommand("POST", "/diagnostics/reboot", "");
        }
        public void Extend(ushort duration)
        {
            SendHttpCommand("POST", "/capture/extend", String.Format("duration={0}&extend=Submit+Query", duration)); // duration in seconds

        }
        public void AdHoc(string description, string profileName, ushort duration)
        {
            string description_ = description.Length > 0 ? description : "Crestron Ad-Hoc";
            string profileName_ = profileName.Length > 0 ? profileName : "Audio and Video";
            ushort duration_ = (ushort)(duration > 0 ? duration : 300);
            SendHttpCommand("POST", "/capture/new_capture", String.Format("description={0}&capture_profile_name={1}&duration={2}", description_, profileName_, duration_)); // duration in seconds
        }
        public void MonitorDevice(string profileName, ushort duration)
        {
            SendHttpCommand("POST", "/capture/confidence_monitor", String.Format("description=Confidence Monitoring&capture_profile_name={0}&duration={1}", profileName, duration)); // duration in seconds
        }
        #endregion
    }

    public struct EchoChannel
    {
        public string position { get; set; }
        public string peak { get; set; }
    }
    public class EchoSource
    {
        public string name { get; set; }
        public string className { get; set; }
        public List<EchoChannel> chans { get; set; }

        public EchoSource()
        {
            chans = new List<EchoChannel>();
        }
    }
    public class EchoSchedule
    {
        public string name { get; set; }
        public string outputType { get; set; }
        public string duration { get; set; }
    }
    public class EchoMonitoring
    {
        public string state { get; set; }
        public string startTime { get; set; }
        public string duration { get; set; }
        public string remaining { get; set; }
        public string originalDuration { get; set; }
        public string outputType { get; set; }
        public List<EchoSource> sources { get; set; }
        public string confidenceMonitoring { get; set; }

        public EchoMonitoring()
        {
            sources = new List<EchoSource>();
        }
    }
    public class EchoStatus
    {
        public string title { get; set; }
        public string presenter { get; set; }
        public string duration { get; set; }
        public string state { get; set; }
        public string outputType { get; set; }
        public string error { get; set; }
        public string rpm { get; set; }
        public EchoSchedule schedule { get; set; }
        public List<EchoSource> sources { get; set; }

        public EchoStatus()
        {
            sources = new List<EchoSource>();
        }
    }
    public class EchoHardwareDetails
    {
        public string wallClockTime { get; set; }
        public string apiVersion { get; set; }
        public string serverType { get; set; }
        public string hostAddress { get; set; }
        public string serialNumber { get; set; }
        public string systemVersion { get; set; }
        public string upSince { get; set; }
        public string lastSync { get; set; }
        public string utcOffset { get; set; }
        public string maxCaptureMinutes { get; set; }
        public string maxCaptureExtendMinutes { get; set; }
        public string location { get; set; }
        public string captureProfile { get; set; }
        public string monitorProfile { get; set; }
        public string logState { get; set; }

        public EchoHardwareDetails()
        {
        }
    }
    public class EchoHardwareDetailsEventArgs : EventArgs
    {
        public EchoHardwareDetails val { get; set; }
        public EchoHardwareDetailsEventArgs() { }
        public EchoHardwareDetailsEventArgs(EchoHardwareDetails val)
        {
            this.val = val;
        }
    }
    public class EchoStatusEventArgs : EventArgs
    {
        public EchoStatus val { get; set; }
        public EchoStatusEventArgs() { }
        public EchoStatusEventArgs(EchoStatus val)
        {
            this.val = val;
        }
    }
    public class EchoMonitoringEventArgs : EventArgs
    {
        public EchoMonitoring val { get; set; }
        public EchoMonitoringEventArgs() { }
        public EchoMonitoringEventArgs(EchoMonitoring val)
        {
            this.val = val;
        }
    }
}


/* GetSystemStatus response
SendHttpCommand to 137.154.42.34:8080, Auth: Basic dXdzOmxlY3RvcGlAIQ==httpResponse: 
<status>
    <wall-clock-time>2016-06-20T03:06:37.508Z</wall-clock-time>
    <api-versions>
        <api-version>3.0</api-version>
    </api-versions>
    <max-capture-duration-minutes>240</max-capture-duration-minutes>
    <max-live-capture-extend-duration-minutes>240</max-live-capture-extend-duration-minutes>
    <server-properties>
        <server-type>ess</server-type>
    </server-properties>
    <capture-profiles>
        <capture-profile>UWS Audio Only</capture-profile>
        <capture-profile>UWS Display and Audio</capture-profile>
        <capture-profile>UWS Display and Audio HD</capture-profile>
    </capture-profiles>
    <monitor-profiles>
        <monitor-profile>UWS Display and Audio HD</monitor-profile>
    </monitor-profiles>
    <host-address>echo0019e5</host-address>
    <serial-number>00-1c-08-00-19-e5</serial-number>
    <system-version>5.5.559796374</system-version>
    <up-since>2016-06-18T15:33:14.824Z</up-since>
    <last-sync>2016-06-20T03:06:26.094Z</last-sync>
    <content>
        <state>idle</state>
        <archive-space-usage />
        <uploaded>0</uploaded>
        <uploads-pending>0</uploads-pending>
        <bytes-pending>0</bytes-pending>
        <uploading>false</uploading>
        <upload>    
            <bytes-per-second>0</bytes-per-second>
            <filename />
            <start-time>1970-01-01T00:00:00.000Z</start-time>
            <file-size>0</file-size>
            <bytes-sent>0</bytes-sent>
        </upload>
    </content>
    <log>
        <state>idle</state>
        <archive-space-usage>22.0</archive-space-usage>
        <uploaded>425</uploaded> 
        <uploads-pending>0</uploads-pending>
        <bytes-pending>0</bytes-pending>
        <uploading>false</uploading>
    </log>
    <location>Hawkesbury: G6, ETS-TEST-2</location>
    <utc-offset>600</utc-offset>
</status>
*/
/* GetMonitoringStatus response
<status>
    <state>inactive</state>
</status>
*/
/* GetCurrent response
<status>
    <wall-clock-time>2016-06-20T03:52:44.059Z</wall-clock-time>
    <api-versions>
        <api-version>3.0</api-version>
    </api-versions>
    <max-capture-duration-minutes>240</max-capture-duration-minutes>
    <max-live-capture-extend-duration-minutes>240</max-live-capture-extend-duration-minutes>
    <server-properties>
        <server-type>ess</server-type>
    </server-properties>
    <capture-profiles>
        <capture-profile>UWS Audio Only</capture-profile>
        <capture-profile>UWS Display and Audio</capture-profile>
        <capture-profile>UWS Display and Audio HD</capture-profile>
    </capture-profiles>
    <monitor-profiles>
        <monitor-profile>UWS Display and Audio HD</monitor-profile>
    </monitor-profiles>
    <current> 
        <schedule></schedule>
    </current>
</status>
*/