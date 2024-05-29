using System;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;    // For Threading

namespace Navitas
{

    public class SerialPort
    {
        public delegate void OnlineEventHandler(object sender, BoolEventArgs e);
        public delegate void StringEventHandler(object sender, StringEventArgs e);

        public event StringEventHandler ParseRxData;

        public ComPort device;
        private CrestronQueue<String> RxQueue = new CrestronQueue<string>();
        private Thread RxHandler;
        //private UInt32 port;
        private string DeviceName = "Serial port";
        private string delim = String.Empty; // = "\x0d\x0a";
        private int rxLength = 0;
        private byte debug = 0;
        public bool debugAsHex = false;
        private bool loopEnable = true;

        public SerialPort(ComPort port, string name)
        {
            this.device = port;
            this.DeviceName = name;
            device.SerialDataReceived += new ComPortDataReceivedEvent(device_SerialDataReceived);

            if (device.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                ErrorLog.Error("COM Port {0} couldn't be registered. Cause: {1}", port, device.DeviceRegistrationFailureReason);
                CrestronConsole.PrintLine("COM Port {0} couldn't be registered. Cause: {1}", port, device.DeviceRegistrationFailureReason);
            }
            if (device.Registered)
            {
                /*
                device.SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate9600,
                                         ComPort.eComDataBits.ComspecDataBits8,
                                         ComPort.eComParityType.ComspecParityNone,
                                         ComPort.eComStopBits.ComspecStopBits1,
                                         ComPort.eComProtocolType.ComspecProtocolRS232,
                                         ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                         ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                         false);
                 */ 
                loopEnable = true; 
                RxHandler = new Thread(RxMethod, null, Thread.eThreadStartOptions.Running);
            }
        }

        public void Initialize()
        {
            ErrorLog.Error("COM Port {0} Initialize", device);
        }

        public void Dispose()
        {
            //CrestronConsole.PrintLine("SerialPort: {0} Dispose", DeviceName);
            //CrestronConsole.PrintLine("SerialPort: {0} aborting RxHandler", DeviceName);
            RxHandler.Abort();
            //CrestronConsole.PrintLine("SerialPort: {0} RxQueue.Enqueue(null)", DeviceName);
            if (RxQueue != null)
                loopEnable = false;
                //RxQueue.Enqueue(null); // The RxThread will terminate when it receives a null
            //CrestronConsole.PrintLine("SerialPort: {0} setting to null", DeviceName);
            device = null;
            //CrestronConsole.PrintLine("SerialPort: {0} Dispose done", DeviceName);
        }

        public void SetDelim(string str)
        {
            delim = str;
        }

        public void SetDebug(byte b)
        {
            debug = b;
        }

        public void Send(string msg)
        {
            //if (debug > 1)
                CrestronConsole.PrintLine("{0} Tx: {1}", DeviceName, Utils.CreatePrintableString(msg, debugAsHex));
            device.Send(msg);
        }

        private void device_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} Rx SerialData: {1}", DeviceName, Utils.CreatePrintableString(args.SerialData, debugAsHex));
            RxQueue.Enqueue(args.SerialData);
        }

        public StringBuilder ParseRx(StringBuilder RxData)
        {
            String msg = String.Empty;
            int len = 0;
            msg = RxData.ToString();
            //CrestronConsole.PrintLine("{0} ParseRx({1}): {2}", DeviceName, RxData.Length, Utils.CreatePrintableString(msg, debugAsHex));
            //if (debug > 0)
                CrestronConsole.PrintLine("{0} Rx: {1}", DeviceName, Utils.CreatePrintableString(msg, debugAsHex));
            if (delim.Length > 0)
            {
                len = msg.IndexOf(Convert.ToChar(delim)); // find the delimiter
                if (len > 0)
                    len += delim.Length;
            }
            else if (rxLength > 0)
            {
                if (msg.Length >= rxLength)
                    len = rxLength; //find the delimiter
            }
            else
                len = RxData.Length;
            while (len > 0) // delimiter found
            { // create temporary string with matched data.
                try
                {
                    msg = msg.Substring(0, len);
                    //CrestronConsole.PrintLine(" msg.Length {0}. len: {1}", msg.Length, len);
                    if (ParseRxData != null)
                        ParseRxData(this, new StringEventArgs(msg));
                    RxData.Remove(0, len); // remove data from COM buffer
                    msg = RxData.ToString();
                    if (delim.Length > 0)
                    {
                        len = msg.IndexOf(Convert.ToChar(delim)); // find the delimiter
                        if (len > 1)
                            len += delim.Length;
                    }
                    else if (rxLength > 0)
                    {
                        if (msg.Length >= rxLength)
                            len = rxLength; //find the delimiter
                    }
                    else
                        len = RxData.Length;
                }
                catch (Exception ex)
                {
                    ErrorLog.Error("{0} ParseRx Exception in thread: {1}", DeviceName, ex.ToString());
                    CrestronConsole.PrintLine("{0} ParseRx Exception in thread: {1}", DeviceName, ex.ToString());
                    if (RxData == null)
                        CrestronConsole.PrintLine("RxData == null");
                    else
                        CrestronConsole.PrintLine("RxData.Length {0}", RxData.Length);
                }
            }
            return RxData;
        }

        object RxMethod(object obj)
        {
            StringBuilder RxData = new StringBuilder();
            while (device != null)
            {
                try
                {
                    if(!loopEnable)
                    {
                        CrestronConsole.PrintLine("{0} Rx loop disabled", DeviceName);
                        return null; // terminate the thread
                    }
                    if (RxQueue.Disposed)
                    {
                        CrestronConsole.PrintLine("{0} RxQueue.Disposed", DeviceName);
                        return null; // terminate the thread
                    }
                    if (RxQueue == null)
                    {
                        CrestronConsole.PrintLine("{0} RxQueue == null", DeviceName);
                        //return null; // terminate the thread
                    }
                    if (RxQueue.Peek() == null)
                    {
                        //CrestronConsole.PrintLine("{0} RxQueue.Peek() == null", DeviceName);
                        //return null; // terminate the thread
                    }
                    if (RxQueue.IsEmpty)
                    {
                        //CrestronConsole.PrintLine("{0} RxQueue.IsEmpty", DeviceName);
                    }
                    string tempString = RxQueue.Dequeue();
                    if (tempString == null)
                    {
                        CrestronConsole.PrintLine("{0} Rx: null", DeviceName);
                        return null; // terminate the thread
                    }
                    tempString = tempString.TrimStart((char)0);
                    if (tempString.Length == 0)
                    {
                        CrestronConsole.PrintLine("{0} tempString.Length == 0", DeviceName);
                        //return null; // terminate the thread
                    }
                    //else
                    //    CrestronConsole.PrintLine("{0} tempString.Length:{1}", DeviceName, tempString.Length);
                    RxData.Append(tempString); //Append received data to the buffer
                    if (RxData.Length > 0)
                        RxData = ParseRx(RxData);
                }
                catch (Exception ex)
                {
                    //ErrorLog.Error("{0} RxMethod Exception in thread: {1}", DeviceName, ex.ToString());
                    CrestronConsole.PrintLine("{0} RxMethod {1} in thread", DeviceName, ex.Message);
                }
            }
            CrestronConsole.PrintLine("{0} RxMethod exitted while loop", DeviceName);
            return null;
        }
    }
}