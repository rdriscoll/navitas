using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace Navitas
{

    public class Display
    {
        public delegate void PowerEventHandler(object sender, PowerEventArgs e);
        public delegate void DisplaySourceEventHandler(object sender, DisplaySourceEventArgs e);
        public delegate void LevelEventHandler(object sender, LevelEventArgs e);
        public delegate void UShortEventHandler(object sender, UShortEventArgs e);
        public delegate void StringEventHandler(object sender, StringEventArgs e);

        //delegates
        public event PowerEventHandler PowerFb;
        //public event PowerEventHandler VolMuteFb;
        //public event PowerEventHandler PicMuteFb;
        public event DisplaySourceEventHandler SourceFb;
        public event StringEventHandler Send;
        public event LevelEventHandler VolumeFb;

        //vars
        protected PowerStates _powerPending;
        public PowerStates PowerCurrent { get; set; }
        protected DisplaySources _sourcePending;
        protected DisplaySources _sourceCurrent;
        public PowerStates PicFreezeCurrent { get; set; }
        public PowerStates PicMuteCurrent { get; set; }
        public PowerStates VolMuteCurrent { get; set; }
        public int VolumeCurrent { get; set; }
        public int VolLevelMax { get; set; }
        public int VolLevelMin { get; set; }
        public int VolStep { get; set; }
        protected ushort _ID = 0;
        public ushort WarmMsecs { get; set; }
        public ushort CoolMsecs { get; set; }
        protected ushort _timerCurrent;
        protected String _name = "Projector";
        public IPClient IpComms;
        protected SerialPort SerialComms;

        protected CTimer powerTimer;
        protected CTimer pollTimer;
        public int pollTimerRepeat = 5000;
        public int powerTimerRepeat = 1000;
        protected string _CurrentPoll = String.Empty;
        public ushort DEFAULT_IP_PORT;

        public Display(string name)
        {
            this._name = name;
            if (WarmMsecs < 1)
                WarmMsecs = 30000;
            if (CoolMsecs < 1)
                CoolMsecs = 30000;
            pollTimer = new CTimer(pollTimerExpired, this, pollTimerRepeat, pollTimerRepeat);
        }

        public virtual void SetComms(IPClient comms)
        {
            this.IpComms = comms;
        }

        public virtual void SetComms(SerialPort comms)
        {
            this.SerialComms = comms;
        }

        protected virtual void Initialize()
        {
        }

        public void Dispose()
        {
            //CrestronConsole.PrintLine("Display: {0} Dispose", _name);
            if (powerTimer != null)
            {
                //CrestronConsole.PrintLine("Display: {0} stopping powerTimer", _name);
                powerTimer.Stop();
                //CrestronConsole.PrintLine("Display: {0} disposing powerTimer", _name);
                powerTimer.Dispose();
            }
            if (pollTimer != null)
            {
                //CrestronConsole.PrintLine("Display: {0} stopping pollTimer", _name);
                pollTimer.Stop();
                //CrestronConsole.PrintLine("Display: {0} disposing pollTimer", _name);
                pollTimer.Dispose();
            }
            //CrestronConsole.PrintLine("Display: {0} disposing IpComms", _name);
            if (IpComms != null)
                IpComms.Dispose();
            //CrestronConsole.PrintLine("Display: {0} disposing SerialComms", _name);
            if (SerialComms != null)
                SerialComms.Dispose();
            //CrestronConsole.PrintLine("Display: {0} Dispose done", _name);
        }

        protected string getId()
        {
            return String.Format("{0}", _ID).PadLeft(2, '0');
        }

        public void SetName(string name)
        {
            this._name = name;
        }

        public string GetName()
        {
            return _name;
        }

        public void SendString(string str)
        {
            //CrestronConsole.PrintLine("{0} SendString {1}", _name, str);
            if (Send != null)
                Send(this, new StringEventArgs(str));
            //else
            //    CrestronConsole.PrintLine("ERR {0} Send is null");
            if (IpComms != null)
            {
                bool success = IpComms.Send(str);
                if(!success)
                    CrestronConsole.PrintLine("ERR {0} Display.SendString, Failed to open port on {1}:{2}", _name, IpComms.IpAddress, IpComms.IpPort);
            }
            if (SerialComms != null)
                SerialComms.Send(str);
        }

        public virtual void SetPowerOn()
        {
            _powerPending = PowerStates.ON;
            CrestronConsole.PrintLine("Display {0} SetPowerOn, Pending: {1}, Current: {2}", _name, _powerPending, PowerCurrent);
            if (PowerCurrent == PowerStates.OFF)
            {
                bool alreadyRunning = _timerCurrent > 0;
                _timerCurrent = WarmMsecs;
                PowerCurrent = PowerStates.WARMING;
                if (PowerFb != null)
                    PowerFb(this, new PowerEventArgs(PowerCurrent, _timerCurrent));
                try
                {
                    if (powerTimer == null || !alreadyRunning)
                    {
                        powerTimer = new CTimer(timerExpired, this, powerTimerRepeat, powerTimerRepeat);
                        //powerTimer = new CTimer(timerExpired, WarmMsecs);//, this, 1, powerTimerInterval);
                        CrestronConsole.PrintLine("{0} creating powerTimer", _name);
                    }
                    else
                    {
                        powerTimer.Reset(1, 1000); ;
                        CrestronConsole.PrintLine("{0} WARMING already running", _name);
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("{0} SetPowerOn powerTimer.Reset() {1}", _name, e.Message);
                }
            }
        }

        public virtual void SetPowerOff()
        {
            _powerPending = PowerStates.OFF;
            CrestronConsole.PrintLine("Display {0} SetPowerOff, Pending: {1}, Current: {2}", _name, _powerPending, PowerCurrent);
            if (PowerCurrent == PowerStates.ON)
            {
                bool alreadyRunning = _timerCurrent > 0;
                _timerCurrent = CoolMsecs;
                PowerCurrent = PowerStates.COOLING;
                if (PowerFb != null)
                    PowerFb(this, new PowerEventArgs(PowerCurrent, _timerCurrent));
                try
                {
                    if (powerTimer == null || !alreadyRunning)
                    {
                        powerTimer = new CTimer(timerExpired, this, powerTimerRepeat, powerTimerRepeat);
                        CrestronConsole.PrintLine("{0} creating powerTimer", _name);
                    }
                    else
                    {
                        powerTimer.Reset(1, 1000); ;
                        CrestronConsole.PrintLine("{0} COOLING already running", _name);
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("{0} SetPowerOff powerTimer.Reset() {1}", _name, e.Message);
                }
            }
        }

        public virtual void SetPowerToggle()
        {
            SetPower(PowerStates.TOGGLE);
        }

        public virtual void SetPower(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.OFF:
                    SetPowerOff();
                    break;
                case PowerStates.ON:
                    SetPowerOn();
                    break;
                case PowerStates.TOGGLE:
                    if(PowerCurrent == PowerStates.ON)
                        SetPowerOff();
                    else
                        SetPowerOn();
                    break;
            }
        }

        public virtual void SetPowerFeedback(PowerStates state)
        {
            //CrestronConsole.PrintLine("{0} SetPowerFeedback {1}", _name, state);
            if (PowerCurrent != state || _timerCurrent > 0)
            {
                PowerCurrent = state;
                if (PowerFb != null)
                    PowerFb(this, new PowerEventArgs(PowerCurrent, _timerCurrent));
            }
        }

        public virtual void SetSourceFeedback(DisplaySources input)
        {
            //CrestronConsole.PrintLine("{0} SetSourceFeedback {1}", _name, input);
            _sourceCurrent = input;
            if (SourceFb != null)
                SourceFb(this, new DisplaySourceEventArgs(_sourceCurrent));
        }

        protected virtual void SendInput(string str)
        {
            //CrestronConsole.PrintLine("{0} SendInput {1}", _name, input);
            if (PowerCurrent == PowerStates.ON)
            {
                try
                {
                    SetSourceFeedback((DisplaySources)Enum.Parse(typeof(DisplaySources), str, true));
                }
                catch
                {
                }
            }
        }

        protected virtual void SendInput(ushort input) // this is the actual string protocol to send to device.
        {
            SendInput(String.Format("{0:X2}", input));
        }

        public virtual void SetSource(DisplaySources source)
        {
            _sourcePending = source;
        }

        public virtual void SetPicMute(PowerStates state)
        {
            if (state == PowerStates.TOGGLE)
                SetPicMute(PicMuteCurrent == PowerStates.OFF ? PowerStates.ON : PowerStates.OFF);
            if (PowerCurrent == PowerStates.ON)
                PicMuteCurrent = state;
        }
        public virtual void SetPicFreeze(PowerStates state)
        {
            if (state == PowerStates.TOGGLE)
                SetPicFreeze(PicFreezeCurrent == PowerStates.OFF ? PowerStates.ON : PowerStates.OFF);
            if (PicFreezeCurrent == PowerStates.ON)
                PicFreezeCurrent = state;
        }

        public virtual void SetVolMute(PowerStates state)
        {
            if (state == PowerStates.TOGGLE)
                SetVolMute(VolMuteCurrent == PowerStates.OFF ? PowerStates.ON : PowerStates.OFF);
            if (PowerCurrent == PowerStates.ON)
                VolMuteCurrent = state;
        }
        public virtual void SetVolume(int level)
        {
            if (PowerCurrent == PowerStates.ON)
            {
                VolumeCurrent = level;
                if (VolumeFb != null)
                    VolumeFb(this, new LevelEventArgs((ushort)VolumeCurrent, (ushort)Utils.ConvertRanges(VolumeCurrent, VolLevelMin, VolLevelMax, 0, 65535)));
            }
        }

        public void SetVolumePercent(int level)
        {
            //CrestronConsole.PrintLine("SetVolumePercent({0})", level);
            VolumeCurrent = Utils.ConvertRanges(level, 0, 100, VolLevelMin, VolLevelMax);
            SetVolume(VolumeCurrent);
        }

        public virtual void SetVolumeUp()
        {
            if (PowerCurrent == PowerStates.ON)
            {
                if (VolumeCurrent + VolStep < VolLevelMax)
                    VolumeCurrent += VolStep;
                else
                    VolumeCurrent = VolLevelMax;
                SetVolume(VolumeCurrent);
            }
        }

        public virtual void SetVolumeDown()
        {
            if (PowerCurrent == PowerStates.ON)
            {
                if (VolumeCurrent > VolLevelMin + VolStep)
                    VolumeCurrent -= VolStep;
                else
                    VolumeCurrent = VolLevelMin;
                SetVolume(VolumeCurrent);
            }
        }
        public virtual void SetChanUp()
        {
        }
        public virtual void SetChanDown()
        {
        }

        public virtual bool SendCommand(string str)
        {
            //CrestronConsole.PrintLine("{0} SendCommand {1}", _name, str);
            string delim = "INPUT_";
            if (str.Contains(delim))
            {
                int pos = str.IndexOf(delim);
                string res = str.Substring(pos + delim.Length, str.Length - pos - delim.Length);
                try
                {
                    SetSource((DisplaySources)Enum.Parse(typeof(DisplaySources), str, true));
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                switch (str)
                {
                    case "POWER_ON": SetPowerOn(); break;
                    case "POWER_OFF": SetPowerOff(); break;
                    /*
                    case "INPUT_HDMI_1": SetSource(DisplaySources.HDMI_1); break;
                    case "INPUT_HDMI_2": SetSource(DisplaySources.HDMI_2); break;
                    case "INPUT_HDMI_3": SetSource(DisplaySources.HDMI_3); break;
                    case "INPUT_HDMI_4": SetSource(DisplaySources.HDMI_4); break;
                    case "INPUT_HDBT_1": SetSource(DisplaySources.HDBT_1); break;
                    case "INPUT_COMP_1": SetSource(DisplaySources.COMP_1); break;
                    case "INPUT_COMP_2": SetSource(DisplaySources.COMP_2); break;
                    case "INPUT_VID_1": SetSource(DisplaySources.VID_1); break;
                    case "INPUT_VID_2": SetSource(DisplaySources.VID_2); break;
                    case "INPUT_VGA_1": SetSource(DisplaySources.RGB_1); break;
                    case "INPUT_DTV": SetSource(DisplaySources.DTV_1); break;
                    case "INPUT_ATV": SetSource(DisplaySources.ATV_1); break;
                    */
                    case "POWER_TOGGLE": SetPower(PowerStates.TOGGLE); break;
                    case "PIC_MUTE_ON": SetPicMute(PowerStates.ON); break;
                    case "PIC_MUTE_OFF": SetPicMute(PowerStates.OFF); break;
                    case "VOL_MUTE_ON": SetVolMute(PowerStates.ON); break;
                    case "VOL_MUTE_OFF": SetVolMute(PowerStates.OFF); break;
                    case "VOL_MUTE_TOGGLE": SetVolMute(PowerStates.TOGGLE); break;
                    case "VOL_UP": SetVolumeUp(); break;
                    case "VOL_DN": SetVolumeDown(); break;
                    case "CHAN_UP": SetChanUp(); break;
                    case "CHAN_DN": SetChanDown(); break;
                    /*
                    case "ASPECT_4_3"  : SendString(String.Format("kc {0} 01\x0D", _ID)); break;
                    case "ASPECT_16_9" : SendString(String.Format("kc {0} 02\x0D", _ID)); break;
                    case "ASPECT_ZOOM_1":SendString(String.Format("kc {0} 04\x0D", _ID)); break;
                    case "ASPECT_ZOOM_2":SendString(String.Format("kc {0} 05\x0D", _ID)); break;
                    case "ASPECT_NORM" : SendString(String.Format("kc {0} 06\x0D", _ID)); break;
                    case "ASPECT_14_9" : SendString(String.Format("kc {0} 07\x0D", _ID)); break;
                    case "ASPECT_SCAN" : SendString(String.Format("kc {0} 08\x0D", _ID)); break;
                    case "PIP_ON"      : SendString(String.Format("kn {0} 01\x0D", _ID)); break;
                    case "PIP_OFF"     : SendString(String.Format("kn {0} 00\x0D", _ID)); break;
                    */
                    default: return false;
                }
            }
            return true;
        }

        private void timerExpired(object obj)
        {
            try
            {
                //CrestronConsole.PrintLine("{0} timerExpired {1}", _name, _timerCurrent);
                if (_timerCurrent < 1000)
                {
                    _timerCurrent = 0;
                    if (PowerCurrent == PowerStates.COOLING)
                        PowerCurrent = PowerStates.OFF;
                    else if (PowerCurrent == PowerStates.WARMING)
                        PowerCurrent = PowerStates.ON;

                    if (_powerPending != PowerCurrent)
                        SetPower(_powerPending);
                    else
                    {
                        if (PowerCurrent == PowerStates.ON)
                            SetSource(_sourcePending);
                        powerTimer.Stop();
                        powerTimer.Dispose();
                    }
                }
                else
                {
                    _timerCurrent -= 1000;
                }
                if (PowerFb != null)
                    PowerFb(this, new PowerEventArgs(PowerCurrent, _timerCurrent));
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} timerExpired {1}", _name, e.Message);
            }
        }

        protected virtual void pollTimerExpired(object obj)
        {

        }
    }
}