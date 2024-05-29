using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;    // For Threading
// have to keep-alive over ethernet or the socket drops and won't pass data on reconnect.

namespace Navitas
{
    public class PanasonicProj : Display
    {
        private string delimiter = "\x0D";
        private string hash = String.Empty;
        private string user = "admin1"; // todo: set way to change from default "admin1"
        private string password = "panasonic"; // todo: set way to change from default "panasonic"

        public PanasonicProj(string name)
             : base(name)
        {
            DEFAULT_IP_PORT = 1024;
            WarmMsecs = 40000;
            CoolMsecs = 40000;
            VolLevelMax = 100;
            VolLevelMin = 0;
            VolStep = 1;
        }

        protected override void Initialize()
        {
            base.Initialize();
        }

        public override void SetComms(IPClient comms)
        {
            base.SetComms(comms);
            comms.SetDelim(delimiter);
            comms.IpPort = DEFAULT_IP_PORT;
            comms.ParseRxData += new IPClient.StringEventHandler(ParseRx);
            //comms.SetDebug(0);
        }

        public override void SetComms(SerialPort comms)
        {
            base.SetComms(comms);
            comms.SetDelim(delimiter);
            comms.ParseRxData += new SerialPort.StringEventHandler(ParseRx);
            if (comms.device.Registered)
                comms.device.SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate9600,
                                         ComPort.eComDataBits.ComspecDataBits8,
                                         ComPort.eComParityType.ComspecParityNone,
                                         ComPort.eComStopBits.ComspecStopBits1,
                                         ComPort.eComProtocolType.ComspecProtocolRS232,
                                         ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                         ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                         false);
            else
                CrestronConsole.PrintLine("{0} ComPort[{1}] not registered, failed to set commSpec", _name, comms.device);
        }

        public void MakeString(String str)
        {
            SendString(String.Format("{0}{1}{2}{3}", hash, getId(), str, delimiter)); // <32 byte hash>00<CMD>\x0d
            //hash = "";
        }

        public override void SetPowerOn()
        {
            MakeString("PON"); // "'00PON',$0d"
            base.SetPowerOn();
        }

        public override void SetPowerOff()
        {
            MakeString("POF"); // "'00PON',$0d"
            base.SetPowerOff();
        }

        protected override void SendInput(string input)
        {
            MakeString(String.Format("IIS:{0}", input)); // "'00IIS:HD1',$0d"
            base.SendInput(input);
        }

        public override void SetSource(DisplaySources source)
        {
            switch (source)
            {
                case DisplaySources.HDMI_1: SendInput("HD1"); break;
                case DisplaySources.HDMI_2: SendInput("HD2"); break;
                case DisplaySources.HDMI_3: SendInput("HD3"); break;
                case DisplaySources.HDBT_1: SendInput("DL1"); break;
            }
            base.SetSource(source);
        }

        public override void SetPicMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON : MakeString("OSH:1"); break; // shutter on
                case PowerStates.OFF: MakeString("OSH:0"); break; // shutter off
                //default: base.SetPicMute(state); break;
            }
            base.SetPicMute(state);
        }

        public override void SetPicFreeze(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON : MakeString("OFZ:1"); break; // QFZ
                case PowerStates.OFF: MakeString("OFZ:0"); break;
                //default: base.SetPicFreeze(state); break;
            }
            base.SetPicFreeze(state);
        }
        public override void SetVolMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON : MakeString("AMT:1"); break;
                case PowerStates.OFF: MakeString("AMT:0"); break;
                //default: base.SetVolMute(state); break;
            }
            base.SetVolMute(state);
        }

        public override void SetVolume(int level)
        {
            CrestronConsole.PrintLine("Pana SetVolume({0})", level);
            //MakeString("AVL:" + Encoding.GetEncoding("ISO-8859-1").GetString(new byte[] { (byte)level }, 0, 1));
            MakeString(String.Format("AVL:{0:000}", level));
        }

        public override void SetVolumeUp()
        {
            base.SetVolumeUp();
            SetVolume(VolumeCurrent);
        }

        public override void SetVolumeDown()
        {
            base.SetVolumeDown();
            SetVolume(VolumeCurrent);
        }

        // Extended commands not included in base
        public override bool SendCommand(string str)
        {
            if (base.SendCommand(str))
                return true;
            {
                switch (str)
                {
                    case "GET_STATUS":
                    case "GET_POWER" : MakeString("QPW"); break;
                    case "GET_INPUT" : MakeString("QIN"); break;
                    case "GET_ONTIME": MakeString("Q$L"); break;
                    default: return false;
                }
            }
            return true;
        }

        protected override void pollTimerExpired(object obj)
        {
            if (PowerCurrent == PowerStates.OFF)
                _CurrentPoll = "GET_POWER";
            else
            {
                switch (_CurrentPoll)
                {
                    case "GET_POWER": _CurrentPoll = "GET_INPUT"; break;
                    default         : _CurrentPoll = "GET_POWER"; break;
                }
            }
            SendCommand(_CurrentPoll);
        }

        public void ParseRx(object sender, StringEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} ParseRx: {1}", _name, args.str);
            string str = "NTCONTROL 1 ";
            if (args.str.Contains(str)) // NTCONTROL 1 00002d2e\x0D
            {
                int start = args.str.IndexOf(str) + str.Length;
                string key = args.str.Substring(start, args.str.Length - start - 1);
                hash = Utils.MD5(String.Format("{0}:{1}:{2}", user, password, key)).ToLower();
                if(true)
                    CrestronConsole.PrintLine("{0} key:{1} hash:{2}", _name, key, hash);
            }
            else
            {
                switch(_CurrentPoll)
                {
                    case "GET_POWER":
                        //CrestronConsole.PrintLine("{0} ParseRx: {1}, {2}", _name, _CurrentPoll, args.str);
                        if (Utils.atoi(args.str) == 0)
                            SetPowerFeedback(PowerStates.OFF);
                        else
                            SetPowerFeedback(PowerStates.ON);
                        _CurrentPoll = "";
                        break;
                    case "GET_INPUT":
                        _CurrentPoll = "";
                        break;
                }
            }
        }

    }
}