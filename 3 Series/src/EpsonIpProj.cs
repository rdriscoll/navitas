using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;    // For Threading
using Crestron.SimplSharp.Cryptography;

namespace Navitas
{
    public class EpsonIpProj : Display
    {
        private string delimiter = "\x0D";
        private string hash = String.Empty;
        private string user = ""; // todo: set way to change from default ""
        private string password = ""; // todo: set way to change from default "panasonic"
        private ushort ipPort = 3629;

        public EpsonIpProj(string name)
             : base(name)
        {
        }

        protected override void Initialize()
        {
            WarmMsecs = 2000;
            CoolMsecs = 4000;
            base.Initialize();
        }

        public override void SetComms(IPClient comms)
        {
            base.SetComms(comms);
            comms.SetDelim(delimiter);
            comms.ParseRxData += new IPClient.StringEventHandler(ParseRx);
            comms.OnlineFb += new IPClient.OnlineEventHandler(EvOnline);
            comms.SetDebug(5);
        }

        public void MakeString(String str)
        {
            SendString(str);
        }

        public override void SetPowerOn()
        {
            MakeString("IMPWR ON\x0d"); // IMPWR ON\x0d
            base.SetPowerOn();
        }

        public override void SetPowerOff()
        {
            MakeString("IMPWR OFF\x0d"); // "'%1POWR 0',$0d"
            base.SetPowerOff();
        }

        protected void SendInput(ushort input)
        {
            MakeString("SOURCE " + Encoding.GetEncoding("ISO-8859-1").GetString(new byte[] { (byte)input }, 0, 1));
        }

        public override void SetSource(DisplaySources source)
        {
            switch (source)
            {
                case DisplaySources.HDMI_1: SendInput(0x30); break;
                case DisplaySources.HDMI_2: SendInput(0xA0); break;
                case DisplaySources.DP_1: SendInput(0x70); break;

                case DisplaySources.VID_1: SendInput(0x41); break;
                case DisplaySources.SVID_1: SendInput(0x42); break;
                case DisplaySources.COMP_1: SendInput(0x43); break;
                case DisplaySources.COMP_2: SendInput(0x44); break;

                case DisplaySources.RGB_1: SendInput(0x11); break; // Input Card 1-1
                case DisplaySources.RGB_2: SendInput(0x21); break; // Input Card 2-1
                case DisplaySources.RGB_3: SendInput(0xBE); break; // ??

                case DisplaySources.LAN_1: SendInput(0x53); break;
                //case DisplaySources.DVI_1 : SendInput(0xFF); break;
                //case DisplaySources.DVI_2 : SendInput(0xFF); break;
                //case DisplaySources.HDMI_3: SendInput(0xFF); break;
                //case DisplaySources.ATV_1 : SendInput(0xFF); break;
                //case DisplaySources.DTV_1 : SendInput(0xFF); break;
            }
            base.SetSource(source);
        }

        public override void SetPicMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON: MakeString("MUTE ON"); break; // no vol mute, this is AV mute
                case PowerStates.OFF: MakeString("MUTE OFF"); break;
                default: base.SetPicMute(state); break;
            }
            base.SetPicMute(state);
        }

        public override void SetVolMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON: SetVolume(VolLevelMin); break;
                case PowerStates.OFF: SetVolume(_volumeCurrent); break;
                default: base.SetVolMute(state); break;
            }
            base.SetVolMute(state);
        }

        public override void SetVolume(int level)
        {
            MakeString("VOL " + Encoding.GetEncoding("ISO-8859-1").GetString(new byte[] { (byte)level }, 0, 1));
        }

        public override void SetVolumeUp()
        {
            base.SetVolumeUp();
            MakeString("VOL UP");
        }

        public override void SetVolumeDown()
        {
            base.SetVolumeDown();
            MakeString("VOL DOWN");
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
                    case "GET_POWER": MakeString("PWR?"); break;
                    case "GET_INPUT": MakeString("SOURCE?"); break;
                    case "GET_ONTIME": MakeString("LAMP?"); break;
                    case "GET_SN": MakeString("SNO?"); break;
                    case "GET_FW": MakeString("ASINFO? 12"); break;
                    case "GET_VOL": MakeString("VOL?"); break;
                    case "GET_ASPECT": MakeString("ASPECT?"); break;
                    case "GET_FREEZE": MakeString("FREEZE?"); break;
                    case "GET_PICMUTE": MakeString("MUTE?"); break;
                    default: return false;
                }
            }
            return true;
        }

        public void SendExtendedCommand(string str)
        {
            SendCommand(str);
        }
        public void SetSerialNumber(string serialNumber)
        {
            this.serialNumber = serialNumber;
        }

        protected override void pollTimerExpired(object obj)
        {
            if (_powerCurrent == PowerStates.OFF)
                _CurrentPoll = "PWR?";
            else
            {
                switch (_CurrentPoll)
                {
                    case "PWR?": _CurrentPoll = "SOURCE?"; break;
                    default: _CurrentPoll = "PWR?"; break;
                }
            }
            MakeString(_CurrentPoll);
        }

        public void ParseRx(object sender, StringEventArgs args)
        {
            CrestronConsole.PrintLine("Epson ParseRx: {0}", args.str);
            if (args.str.Contains("ERR"))
            {
                // Projector returns “ERR” and a return key code (Hex 0D) and a colon when it receives invalid commands.
                // ERR
                // :
                string EventIdName;
                _ErrorCodes.TryGetValue((uint)Utils.atoi(args.str), out EventIdName);
                CrestronConsole.PrintLine("Epson ERROR: {0}", EventIdName);

                // (note3) Projector initiates the process of the input video signal recognition when it receives a SOURCE command. 
                //          If the signal of the video input is changing (for example, from SVGA to XGA by A/V controller) during the process of the input video signal recognition, the projector returns “ERR”.
                // (note4) Projectors execute the PWR OFF command after they start completely.
                // (note5) Projectors might return “ERR” when “Quick Setup” and “Auto Focus” functions are set to ON.
            }
            else if (args.str.Contains(":"))
                //(note1) When a projector receives the PWR ON command, it tries to ignite the lamp by activating the ballast unit. In case that the lamp fails to be ignited, it tries to ignite the lamp three times at maximum. When the lamp fails to be ignited three times, it is a lamp failure.
                //          The projector returns a colon within 40, 70 and 100 seconds when successful in the first, second and third times respectively.
                //(note2) When the input vide sync signal is stable, a colon is returned within 5 seconds. However, it may take more than 5 seconds when the input video sync signal is unstable.
                ;
            else
            {
                switch (_CurrentPoll)
                {
                    case "PWR?":
                        switch (Utils.atoi(args.str))
                        {
                            case 0: SetPowerFeedback(PowerStates.OFF); break; // at "network off" status check
                            case 1: SetPowerFeedback(PowerStates.ON); break;
                            case 2: SetPowerFeedback(PowerStates.WARMING); break;
                            case 3: SetPowerFeedback(PowerStates.COOLING); break;
                            case 4: SetPowerFeedback(PowerStates.OFF); break; // at "network on" statue check
                        }
                        _CurrentPoll = "";
                        break;
                    case "SOURCE?":
                        switch (Utils.atoi(args.str))
                        {
                            case 0x30: _sourceCurrent = DisplaySources.HDMI_1; break;
                            case 0xA0: _sourceCurrent = DisplaySources.HDMI_2; break;
                            case 0x70: _sourceCurrent = DisplaySources.DP_1; break;

                            case 0x41: _sourceCurrent = DisplaySources.VID_1; break;
                            case 0x42: _sourceCurrent = DisplaySources.SVID_1; break;
                            case 0x43: _sourceCurrent = DisplaySources.COMP_1; break;
                            case 0x44: _sourceCurrent = DisplaySources.COMP_2; break;

                            case 0x11: _sourceCurrent = DisplaySources.RGB_1; break;
                            case 0x21: _sourceCurrent = DisplaySources.RGB_2; break;
                            case 0xBE: _sourceCurrent = DisplaySources.RGB_3; break;

                            case 0x53: _sourceCurrent = DisplaySources.LAN_1; break;
                        }
                        _CurrentPoll = "";
                        break;
                }
            }
        }
        Dictionary<uint, string> _ErrorCodes = new Dictionary<uint, string>
        {
            { 0x00, "There is no error or the error is recovered" },
            { 0x01, "Fan error" },
            { 0x03, "Lamp failure at power on" },
            { 0x04, "High internal temperature error" },
            { 0x06, "Lamp error" },
            { 0x07, "Open Lamp cover door error" },
            { 0x08, "Cinema filter error" },
            { 0x09, "Electric dual-layered capacitor is disconnected" },
            { 0x0A, "Auto iris error" },
            { 0x0B, "Subsystem Error" },
            { 0x0C, "Low air flow error" },
            { 0x0D, "Air filter air flow sensor error" },
            { 0x0E, "Power supply unit error (Ballast)" },
            { 0x0F, "Shutter error" },
            { 0x10, "Cooling system error (peltiert element)" },
            { 0x11, "Cooling system error (Pump)" }
        };
    }
}