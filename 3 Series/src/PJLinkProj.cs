using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;    // For Threading
using Crestron.SimplSharp.Cryptography;
// !!!!!!!!!!Incomplete

namespace Navitas
{
    public class PJLinkProj : Display
    {
        private string delimiter = "\x0D";
        private string hash = String.Empty;
        private string user = "admin1"; // todo: set way to change from default "admin1"
        private string password = "PJLink"; // todo: set way to change from default "panasonic"
        //private ushort ipPort = 1024;

        public PJLinkProj(string name)
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
            //comms.SetDebug(5);
        }

        public override void SetComms(SerialPort comms)
        {
            base.SetComms(comms);
            comms.SetDelim(delimiter);
            comms.ParseRxData += new SerialPort.StringEventHandler(ParseRx);
        }

        public void MakeString(String str)
        {
            SendString(String.Format("{0}{1}{2}{3}", hash, getId(), str, delimiter)); // <32 byte hash>00<CMD>\x0d
            //hash = "";
        }

        public override void SetPowerOn()
        {
            MakeString("POWR 1"); // "'%1POWR 1',$0d"
            base.SetPowerOn();
        }

        public override void SetPowerOff()
        {
            MakeString("POWR 0"); // "'%1POWR 0',$0d"
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
                case PowerStates.ON : MakeString("SH:1"); break; // shutter on
                case PowerStates.OFF: MakeString("SH:0"); break; // shutter off
                default: base.SetPicMute(state); break;
            }
            base.SetPicMute(state);
        }

        protected override void pollTimerExpired(object obj)
        {
            _CurrentPoll = "POWR?";
            MakeString(_CurrentPoll);
        }

        public static string MD5(string s)
        { 
            MD5CryptoServiceProvider x = new MD5CryptoServiceProvider();
            return BitConverter.ToString(x.ComputeHash(Encoding.Default.GetBytes(s))).Replace("-", string.Empty);
        }

        public void ParseRx(object sender, StringEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} ParseRx: {1}", _name, args.str);
            string str = "NTCONTROL 1 ";
            if (args.str.Contains(str)) // NTCONTROL 1 00002d2e\x0D
            {
                int start = args.str.IndexOf(str) + str.Length;
                string key = args.str.Substring(start, args.str.Length - start - 1);
                hash = MD5(String.Format("{0}:{1}:{2}", user, password, key)).ToLower();
                if(true)
                    CrestronConsole.PrintLine("{0} key:{1} hash:{2}", _name, key, hash);
            }
            else if (_CurrentPoll.Length > 0)
            {
                if (args.str.Contains("POWR?"))
                    SetPowerFeedback(PowerStates.OFF);
                else if (args.str.Contains("001"))
                    SetPowerFeedback(PowerStates.ON);
                _CurrentPoll = "";
            }
        }

    }
}