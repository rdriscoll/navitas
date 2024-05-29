using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes

namespace Navitas
{
    public class LumensDocCam
    {
        public delegate void StringEventHandler(object sender, StringEventArgs e);
        public event StringEventHandler Send;
        //vars
        StringBuilder RxData = new StringBuilder();

        protected string _name = "Lumens";
        protected SerialPort SerialComms;
        protected IROutputPort IRSerialComms;
        private string delimiter = "\xAF";

        protected bool _currentPower = false;
        protected bool _currentArm = false;

        public LumensDocCam(string name)
        {
            this._name = name;
        }
        public void SetComms(SerialPort comms)
        {
            this.SerialComms = comms;
            comms.ParseRxData += new SerialPort.StringEventHandler(ParseRx);
            comms.SetDebug(2);
            if (comms.device.Registered)
                comms.device.SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate9600,
                                         ComPort.eComDataBits.ComspecDataBits8,
                                         ComPort.eComParityType.ComspecParityNone,
                                         ComPort.eComStopBits.ComspecStopBits1,
                                         ComPort.eComProtocolType.ComspecProtocolRS232,
                                         ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                         ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                         false);
        }
        public void SetIRSerialComms(IROutputPort comms)
        {
            this.IRSerialComms = comms;
            if (comms.Registered)
                comms.SetIRSerialSpec(eIRSerialBaudRates.ComspecBaudRate9600,
                                      eIRSerialDataBits.ComspecDataBits8,
                                      eIRSerialParityType.ComspecParityNone,
                                      eIRSerialStopBits.ComspecStopBits1,
                                      Encoding.Default);
        }
        protected virtual void Initialize()
        {
        }
        public void Dispose()
        {
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
            if (SerialComms != null)
                SerialComms.Send(str);
            if (IRSerialComms != null)
                IRSerialComms.SendSerialData(str);
        }

        public void MakeString(string str)
        {
            SendString(String.Format("\xA0{0}\x00\x00{1}", str, delimiter));
        }
        public void PowerOn(bool lamp)
        {
            if (lamp)
                MakeString("\xB0\x01"); // "\xA0\xB0\x01\x00\x00\xAF"
            else
                MakeString("\xB1\x01"); // "\xA0\xB1\x01\x00\x00\xAF" // all
            _currentPower = true;
        }
        public void PowerOff(bool lamp)
        {
            if (lamp)
                MakeString("\xB0\x00"); // "\xA0\xB0\x00\x00\x00\xAF"
            else
                MakeString("\xB1\x00"); // "\xA0\xB1\x00\x00\x00\xAF" // all
            _currentPower = false;
        }
        public void PowerToggle()
        {
            if (_currentPower)
                PowerOff(true);
            else
                PowerOn(true);
        }
        public void ArmLight(bool state)
        {
            if (state) // BaseLamp
                MakeString("\xC1\x01"); // "\xA0\xC0\x01\x00\x00\xAF"
            else
                MakeString("\xC1\x00"); // "\xA0\xC1\x00\x00\x00\xAF"
            _currentArm = state;
        }
        public void ArmToggle()
        {
            ArmLight(!_currentArm);
        }

        public void ZoomIn()
        {
            MakeString("\x1D\x00"); // "\xA0\x1D\x00\x00\x00\xAF"
        }
        public void ZoomOut()
        {
            MakeString("\x1D\x01"); // "\xA0\x1D\x01\x00\x00\xAF"
        }
        public void ZoomStop()
        {
            MakeString("\x10\x00"); // "\xA0\x10\x00\x00\x00\xAF"
        }
        public void FocusIn()
        {
            MakeString("\x1A\x00"); // "\xA0\x1A\x00\x01\x00\xAF"
        }
        public void FocusOut()
        {
            MakeString("\x1A\x01"); // "\xA0\x1A\x01\x01\x00\xAF"
        }
        public void FocusStop()
        {
            MakeString("\x19\x00"); // "\xA0\x19\x00\x00\x00\xAF"
        }

        public void ParseRx(object sender, StringEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} ParseRx: {1}", _name, args.str);
            try
            {
                //RxData = RxData.Append(args.str);

            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} ParseRx Exception: {1}", _name, e.ToString());
            }

        }
    }
}