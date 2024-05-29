using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes
//using AVPlus;

namespace Navitas // CP-WU5505
{
    public class HitachiProj : Display
    {
        private string hash = String.Empty;
        //private string user = "Administrator"; // todo: set way to change from default "Administrator"
        private string password = ""; // todo: set way to change from default ""
        public ushort ALTERNATE_IP_PORT = 9715;
        public byte commandId = 0;

        public HitachiProj(string name)
            : base(name) 
        { 
            DEFAULT_IP_PORT = 23; // either 23 or 9715. PJLink 4352
            WarmMsecs = 4000;
            CoolMsecs = 4000;
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
            comms.ParseRxData += new IPClient.StringEventHandler(ParseRx);
            comms.debugAsHex = true;
            comms.IpPort = DEFAULT_IP_PORT;
            comms.SetDebug(3);
        }

        public override void SetComms(SerialPort comms)
        {
            base.SetComms(comms);
            comms.debugAsHex = true;
            comms.ParseRxData += new SerialPort.StringEventHandler(ParseRx);
            if (comms.device.Registered)
                comms.device.SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate19200,
                                         ComPort.eComDataBits.ComspecDataBits8,
                                         ComPort.eComParityType.ComspecParityNone,
                                         ComPort.eComStopBits.ComspecStopBits1,
                                         ComPort.eComProtocolType.ComspecProtocolRS232,
                                         ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                         ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                         false);
            else
                CrestronConsole.PrintLine("{0} ComPort[{1}] not registered, failed to set commSpec", _name, comms.device);
            //comms.SetDebug(5);
        }

        private string[] commandLabel =
        {
            "control",
            "query" ,
            "detail"
        };

        public void MakeString(String str)
        {
            if (IpComms != null && IpComms.IpPort == ALTERNATE_IP_PORT)
            {
                string str1 = String.Format("\x02\x0D\xBE\xEF\x03\x06\x00{0}", str); // data length byte 1 is always \x0D
                SendString(String.Format("{0}{1}{2}", hash, str1, Utils.GetString(new byte[] { Utils.AddBytes(str1) })));
            }
            else
                SendString(String.Format("{0}\xBE\xEF\x03\x06\x00{1}",hash, str));
        }

        public override void SetPowerOn()
        {
            CrestronConsole.PrintLine("{0} SetPowerOn", _name);
            MakeString("\xBA\xD2\x01\x00\x00\x60\x01\x00");
            base.SetPowerOn();
        }

        public override void SetPowerOff()
        {
            CrestronConsole.PrintLine("{0} SetPowerOff", _name);
            MakeString("\x2A\xD3\x01\x00\x00\x60\x00\x00");
            base.SetPowerOff();
        }

        public override void SetSource(DisplaySources source)
        {
            switch (source)
            {
                case DisplaySources.HDMI_1: MakeString("\x0E\xD2\x01\x00\x00\x20\x03\x00"); break;
                case DisplaySources.HDMI_2: MakeString("\x0E\xD2\x01\x00\x00\x20\x03\x01"); break;
                case DisplaySources.DVI_1: MakeString("\x0E\xD3\x01\x00\x00\x20\x03\x00"); break;

                case DisplaySources.VID_1: MakeString("\x6E\xD3\x01\x00\x00\x20\x01\x00"); break;
                case DisplaySources.COMP_1: MakeString("\xAE\xD1\x01\x00\x00\x20\x05\x00"); break;

                case DisplaySources.RGB_1: MakeString("\xFE\xD2\x01\x00\x00\x20\x01\x00"); break;
                case DisplaySources.RGB_2: MakeString("\x5E\xD1\x01\x00\x00\x20\x06\x00"); break;

                case DisplaySources.LAN_1: MakeString("\xFE\xD2\x01\x00\x00\x20\x00\x00"); break;
            }
            base.SetSource(source);
        }

        public override void SetPicMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON: MakeString("\x6B\xD9\x01\x00\x20\x30\x01\x00"); break;
                case PowerStates.OFF: MakeString("\xFB\xD8\x01\x00\x20\x30\x00\x00"); break;
                default: base.SetPicMute(state); break;
            }
            base.SetPicMute(state);
        }
        public override void SetPicFreeze(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON: MakeString("\x13\xD3\x01\x00\x02\x30\x01\x00"); break;
                case PowerStates.OFF: MakeString("\x83\xD2\x01\x00\x02\x30\x00\x00"); break;
                default: base.SetPicFreeze(state); break;
            }
            base.SetPicFreeze(state);
        }

        public override void SetVolMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON: SetVolume(VolLevelMin); break;
                case PowerStates.OFF: SetVolume(VolumeCurrent); break;
                default: base.SetVolMute(state); break;
            }
            base.SetVolMute(state);
        }

        public override void SetVolume(int level)
        {
            MakeString("\x31\xD3\x03\x00\x01\x20\x01" + Encoding.GetEncoding("ISO-8859-1").GetString(new byte[] { (byte)level }, 0, 1));
        }

        public override void SetVolumeUp()
        {
            base.SetVolumeUp();
            MakeString("\x57\xD3\x04\x00\x01\x20\x00\x00");
        }

        public override void SetVolumeDown()
        {
            base.SetVolumeDown();
            MakeString("\x86\xD2\x05\x00\x01\x20\x00\x00");
        }

        // Extended commands not included in base
        public override bool SendCommand(string str)
        {
            _CurrentPoll = str;
            if (base.SendCommand(str))
                return true;
            switch (str)
            {
                case "GET_STATUS":
                case "GET_POWER"  : MakeString("\x19\xD3\x02\x00\x00\x60\x00\x00"); break;
                case "GET_INPUT"  : MakeString("\xCD\xD2\x02\x00\x00\x20\x00\x00"); break;
                case "GET_ONTIME" : MakeString("\xC2\xFF\x02\x00\x90\x10\x00\x00"); break;
                case "GET_VOL"    : MakeString("\x31\xD3\x02\x00\x01\x20\x00\x00"); break;
                case "GET_ASPECT" : MakeString("\xAD\xD0\x02\x00\x08\x20\x00\x00"); break;
                case "GET_FREEZE" : MakeString("\xB0\xD2\x02\x00\x02\x30\x00\x00"); break;
                case "GET_PICMUTE": MakeString("\xC8\xD8\x02\x00\x20\x30\x00\x00"); break;
                default: 
                    _CurrentPoll = "";  
                    return false;
            }
            return true;
        }

        public void SendExtendedCommand(string str)
        {
            SendCommand(str);
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
                    default: _CurrentPoll = "GET_POWER"; break;
                }
            }
            SendCommand(_CurrentPoll);
        }

        public void ParseRx(object sender, StringEventArgs args)
        {
            //CrestronConsole.PrintLine("Hitachi ParseRx: {0}", Utils.CreatePrintableString(args.str, true));
            if (args.str.Contains("\x1F\x04\x00")) // authentication
            {
                CrestronConsole.PrintLine("{0} authentication request", _name);
            }
            else if (args.str.Length == 8) // authentication
            {
                CrestronConsole.PrintLine("{0} authentication key sent", _name);
                hash = Utils.MD5(String.Format("{0}{1}", password, args.str)).ToLower();
                if (true)
                    CrestronConsole.PrintLine("{0} key:{1} hash:{2}", _name, args.str, hash);
            }
            else
            {
                switch (_CurrentPoll)
                {
                    case "GET_POWER":
                        switch (args.str)
                        {
                            case "\x1D\x00\x01": SetPowerFeedback(PowerStates.ON); break;
                            case "\x1D\x00\x00": SetPowerFeedback(PowerStates.OFF); break;
                        }
                        _CurrentPoll = "";
                        break;
                    case "GET_INPUT":
                        switch (args.str)
                        {
                            case "\x1D\x00\x01": _sourceCurrent = DisplaySources.RGB_2 ; break; // PC
                            case "\x1D\x00\x02": _sourceCurrent = DisplaySources.HDMI_1; break;
                            case "\x1D\x00\x03": _sourceCurrent = DisplaySources.HDMI_2; break;
                            case "\x1D\x00\x04": _sourceCurrent = DisplaySources.RGB_1 ; break; // RGB
                        }
                        _CurrentPoll = "";
                        break;
                }
            }
        }
    }
}
