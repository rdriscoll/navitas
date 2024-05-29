using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;    // For Threading

namespace Navitas
{
    public class LgMonitor : Display
    {
        private string delimiter = "\x0D";
        private ushort _currentChan;

        public LgMonitor(string name)
            : base(name)
        {
            DEFAULT_IP_PORT = 23;
            WarmMsecs = 5000;
            CoolMsecs = 5000;
            VolLevelMin = 0;
            VolLevelMax = 64;
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
            comms.SetDebug(0);
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
            comms.SetDebug(2);
        }

        public void MakeString(String str)
        {
            SendString(String.Format("{0}{1}", str, delimiter)); // <CMD>\x0d
        }

        public override void SetPowerOn()
        {
            MakeString(String.Format("ka {0:X2} 01", _ID)); // "'ka 01 01',$0d"
            base.SetPowerOn();
        }

        public override void SetPowerOff()
        {
            MakeString(String.Format("ka {0:X2} 00", _ID)); // "'ka 01 00',$0d"
            base.SetPowerOff();
        }

        protected override void SendInput(string input)
        {
            MakeString(String.Format("xb {0:X2} {1:X2}", _ID, input)); // "'xb 01 00',$0d"
            base.SendInput(input.ToString());
        }

        public override void SetSource(DisplaySources source)
        {
            switch (source)
            {
                case DisplaySources.HDMI_1: SendInput(90); break;
                case DisplaySources.HDMI_2: SendInput(91); break;
                case DisplaySources.HDMI_3: SendInput(92); break;
                case DisplaySources.HDMI_4: SendInput(93); break;
                case DisplaySources.DTV_1 : SendInput(00); break;
                case DisplaySources.VID_1 : SendInput(20); break;
                case DisplaySources.VID_2 : SendInput(21); break;
                case DisplaySources.COMP_1: SendInput(40); break;
                case DisplaySources.COMP_2: SendInput(41); break;
                case DisplaySources.RGB_1 : SendInput(50); break;
            }
            base.SetSource(source);
        }

        public override void SetPicMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON : MakeString(String.Format("kd {0:X2} 00\x0D", _ID)); break; // shutter on
                case PowerStates.OFF: MakeString(String.Format("kd {0:X2} 01\x0D", _ID)); break; // shutter off
                default: base.SetPicMute(state); break;
            }
        }
        public override void SetVolMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON : MakeString(String.Format("ke {0:X2} 00\x0D", _ID)); break; // mute on
                case PowerStates.OFF: MakeString(String.Format("ke {0:X2} 01\x0D", _ID)); break; // mute off
                default: base.SetVolMute(state); break;
            }
        }

        public override void SetVolume(int level)
        {
            base.SetVolume(level);
            SendString(String.Format("kf {0:X2} {1}\x0D", _ID, level));
        }

        public override void SetChanUp()
        {
            MakeString(String.Format("mc {0:X2} 00\x0D", _ID));
        }

        public override void SetChanDown()
        {
            MakeString(String.Format("mc {0:X2} 01\x0D", _ID));
        }

        public void SendChannelKey(ushort ch)
        {
            CrestronConsole.PrintLine("{0} SetTvChannel {1}", _name, ch);
            SendString(String.Format("mc {0:X2} 1{1}\x0D", _ID, ch));
        }

        public void SetChannel(ushort ch)
        {
            ushort val = ch;
            ushort[] a = { 0, 0, 0, 0 };
            for (int i = 4; i > 0; i--) // 0123 [0]3(012)
            {
                a[i - 1] = (ushort)(val % 10);
                val /= 10;
            }
            for (int i = 0; i < 4; i++)
                SendChannelKey(a[i]);
            _currentChan = ch;
        }

        // Extended commands not included in base
        public override bool SendCommand(string str)
        {
            if (base.SendCommand(str))
                return true;
            {
                switch (str)
                {
                    // 'mc' commands are remote emulation
                    case "POWER_TOGGLE" : SendString(String.Format("mc {0:X2} 08\x0D", _ID)); break;
                    case "ASPECT_4_3"   : SendString(String.Format("kc {0:X2} 01\x0D", _ID)); break;
                    case "ASPECT_16_9"  : SendString(String.Format("kc {0:X2} 02\x0D", _ID)); break;
                    case "ASPECT_ZOOM_1": SendString(String.Format("kc {0:X2} 04\x0D", _ID)); break;
                    case "ASPECT_ZOOM_2": SendString(String.Format("kc {0:X2} 05\x0D", _ID)); break;
                    case "ASPECT_NORM"  : SendString(String.Format("kc {0:X2} 06\x0D", _ID)); break;
                    case "ASPECT_14_9"  : SendString(String.Format("kc {0:X2} 07\x0D", _ID)); break;
                    case "ASPECT_SCAN"  : SendString(String.Format("kc {0:X2} 08\x0D", _ID)); break;
                    case "PIP_ON"       : SendString(String.Format("kn {0:X2} 01\x0D", _ID)); break;
                    case "PIP_OFF"      : SendString(String.Format("kn {0:X2} 00\x0D", _ID)); break;
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
                    default: _CurrentPoll = "GET_POWER"; break;
                }
            }
            SendCommand(_CurrentPoll);
        }

        public void ParseRx(object sender, StringEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} ParseRx: {1}", _name, args.str);
            string[] str = args.str.Split(' '); // "a 01 OK01x"
            if(str.Length > 2)
            {
                if (_ID == Convert.ToInt32(Regex.Match(str[1], @"\d+").Value))
                {
                    ushort data = Convert.ToUInt16(Regex.Match(str[2], @"\d+").Value);
                    /*
                    CrestronConsole.PrintLine("cmd : {0}", str[0]);
                    CrestronConsole.PrintLine("ID  : {0}", str[1]);
                    CrestronConsole.PrintLine("data: {0}", str[2]);
                    */
                    switch (str[0])
                    {
                        case "a":
                            //SetPowerFeedback(data == 0 ? PowerStates.OFF : PowerStates.ON);
                            SetPowerFeedback(data == 0 ? PowerStates.OFF : PowerStates.ON);
                            break;
                        case "b":
                            //CrestronConsole.PrintLine("input rx");
                            switch(data)
                            {
                                case 0: _sourceCurrent = DisplaySources.DTV_1; break;
                                case 10: _sourceCurrent = DisplaySources.ATV_1; break;
                                case 20: _sourceCurrent = DisplaySources.VID_1; break;
                                case 21: _sourceCurrent = DisplaySources.VID_2; break;
                                case 40: _sourceCurrent = DisplaySources.COMP_1; break;
                                case 41: _sourceCurrent = DisplaySources.COMP_2; break;
                                case 90: _sourceCurrent = DisplaySources.HDMI_1; break;
                                case 91: _sourceCurrent = DisplaySources.HDMI_2; break;
                                case 92: _sourceCurrent = DisplaySources.HDMI_3; break;
                                case 93: _sourceCurrent = DisplaySources.HDMI_4; break;
                            }
                            SetSourceFeedback(_sourceCurrent);
                            break;
                        case "c":
                            CrestronConsole.PrintLine("aspect rx");
                            break;
                        case "d":
                            CrestronConsole.PrintLine("picmute rx");
                            break;
                        case "e":
                            /*
                            //CrestronConsole.PrintLine("volmute rx");
                            MuteFb(this, new BoolEventArgs(data == 0));
                            _mute = (data == 0);
                            if (MuteFb != null)
                                MuteFb(this, new BoolEventArgs(_mute));
                             */ 
                            break;
                        case "f": // volume;
                            CrestronConsole.PrintLine("data: {0}", data);
                            /*
                            _volume = data;
                            if (VolumeFb != null)
                                VolumeFb(this, new UShortEventArgs((ushort)(_volume * 1024)));
                             */ 
                            break;
                    }
                }
            }
        }
    }
}