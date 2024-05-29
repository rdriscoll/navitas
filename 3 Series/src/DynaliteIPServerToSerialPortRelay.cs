using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes

// usage example
// RelayServer = new DynaliteIPServerToSerialPortRelay(2300, "192.168.1.34", ComPorts[1], "IPToSerial");

namespace Navitas
{
    public class DynaliteIPServerToSerialPortRelay
    {
        #region variables

        public string DeviceName = "TCP To Serial Server";
        public int serverIpPort = 2300; // this app
        private string serverIpAddress = "0.0.0.0"; // any address
        public string Password = "";         // Stores the Password
        private string AuthResponse = "";     // Authentication Required
        private int BufferSize = 1024;

        protected CTimer queueTimer;
        public int queueTimerRepeat = 500;
        private const int maxQueueSize = 30;
        private int queueNext = 0, queueStart = 0;
        private string[] queueOut;
        StringBuilder SerialBuffer = new StringBuilder();

        TCPServer server;
        ComPort targetComPort;
        private CrestronQueue<String> serverRxQueue = new CrestronQueue<string>();
        private CrestronQueue<String> targetComPortRxQueue = new CrestronQueue<string>();
        private Dictionary<uint, byte> clientAreas = new Dictionary<uint, byte>();

        private int retries;
        private string loading = "-\\|/";
        private string delimServer = "\x0d";//\x0a";
        private string delimtargetComPort = "\x0d";//\x0a";
        //private int keepAlive = 30;
        private byte debug = 5;
        public bool debugAsHex = true;

        public EthernetAdapterType etherPort = EthernetAdapterType.EthernetLANAdapter;

        public int serverMaxClients = 30;

        protected CTimer pollTimer;
        public int pollTimerRepeat = 6000;
        int pollTwice = 1;

        #endregion
        #region comms

        public DynaliteIPServerToSerialPortRelay(ComPort targetComPort, int serverPort, string targetComPortAddress,
            EthernetAdapterType ether, int maxConnections, string name)
        {
            try
            {
                CrestronConsole.PrintLine("{0} Constructor", DeviceName);
                queueOut = new string[maxQueueSize];
                this.serverMaxClients = maxConnections;
                this.targetComPort = targetComPort;
                this.serverIpPort = serverPort;
                this.etherPort = ether;
                DeviceName = name;
                CreateIpServer();

                targetComPort.SerialDataReceived += new ComPortDataReceivedEvent(ComPort_SerialDataReceived);
                if (targetComPort.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error("{0} couldn't be registered. Cause: {1}", targetComPort.DeviceName, targetComPort.DeviceRegistrationFailureReason);
                    CrestronConsole.PrintLine("{0} couldn't be registered. Cause: {1}", targetComPort.DeviceName, targetComPort.DeviceRegistrationFailureReason);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} constructor ERROR: {1}", DeviceName, e);
            }
        }
        public void Dispose()
        {
            if (queueTimer != null)
            {
                queueTimer.Stop();
                queueTimer.Dispose();
            }
            if (pollTimer != null)
            {
                pollTimer.Stop();
                pollTimer.Dispose();
            }
            //CrestronConsole.PrintLine("Display: {0} disposing SerialComms", _name);
            if (targetComPort != null)
            {
                targetComPort = null;
            }
            if (server != null)
            {
                try
                {
                    server.DisconnectAll();
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("{0} Dispose DisconnectAll Exception {1}", DeviceName, e.ToString());
                }
            }

        }
        public void SetDebug(byte b)
        {
            debug = b;
        }
        protected void CreateIpServer()
        {
            try
            {
                CrestronConsole.PrintLine("{0} CreateIpServer", DeviceName);
                pollTwice = 1;
                pollTimer = new CTimer(pollTimerExpired, this, 1, pollTimerRepeat);
                queueTimer = new CTimer(queueTimerExpired, this, 1, queueTimerRepeat);
                queueTimer.Stop();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} CreateIpServer ERROR: {1}", DeviceName, e);
            }
        }

        #endregion
        #region callbacks

        protected void pollTimerExpired(object obj)
        {
            try
            {
                CrestronConsole.PrintLine("{0} pollTimerExpired {1}", DeviceName, pollTwice);
                if (pollTwice>0) pollTwice--;
                else
                {
                    CrestronConsole.PrintLine("{0} Start Relay Server", DeviceName, targetComPort.DeviceName);
                    if (debug > 0)
                        CrestronConsole.PrintLine("{0} CreateIpServer relay", DeviceName);

                    server = new TCPServer(serverIpAddress, serverIpPort, BufferSize, etherPort, serverMaxClients);
                    if (server != null)
                    {
                        CrestronConsole.PrintLine("{0} server != null", DeviceName);
                        server.SocketSendOrReceiveTimeOutInMs = 0;// 100000;
                        server.HandleLinkUp();
                        server.WaitForConnectionAsync(ConnectCallbackAsyncServer);//AcceptCallbackHost);
                        server.SocketStatusChange += new TCPServerSocketStatusChangeEventHandler(SocketStatusEventServer);
                        //serverRxHandler = new Thread(serverRxMethod, null, Thread.eThreadStartOptions.Running);
                    }
                    else
                        CrestronConsole.PrintLine("{0} server == null", DeviceName);

                    if (targetComPort != null)
                    {
                        //Connect();
                    }
                    CrestronConsole.PrintLine("{0} Relay Server Setup on {1}, {2}:{3}", DeviceName, targetComPort.DeviceName, etherPort.ToString(), serverIpPort);
//                    CrestronConsole.PrintLine("stop poll");
                    pollTimer.Stop();
                }
            }
            catch (Exception e)
            {
//                CrestronConsole.PrintLine("stop poll");
                pollTimer.Stop();
                CrestronConsole.PrintLine("{0} CreateIpServer ERROR: {1}", DeviceName, e);
            }
        }

//------------------------------------------------------- SERVER SIDE -------------------------------
        protected void queueTimerExpired(object obj)
        {
            try
            {
                //private int queueNext = 0, queueStart = 0;
                CrestronConsole.PrintLine("{0} {1} Tx: {2}", DeviceName, targetComPort.DeviceName, Utils.CreatePrintableString(queueOut[queueStart], true));
                targetComPort.Send(queueOut[queueStart]);
                queueStart++;
                if (queueStart >= maxQueueSize)
                    queueStart = 0;
                if (queueStart == queueNext) // queue caught up, stop till more data comes
                    queueTimer.Stop();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} queueTimerExpired: {1}", DeviceName, e);
            }

        }
        void SocketStatusEventServer(TCPServer myserver, uint clientIndex, SocketStatus serverSocketStatus)
        {
            try
            {
                //            CrestronConsole.PrintLine("SocketStatusEvent Server");
                if (debug > 2)
                    CrestronConsole.PrintLine("{0} {1} SocketStatusEvent {2} client {3}", DeviceName, serverIpAddress, myserver.ServerSocketStatus.ToString(), clientIndex);
                if (myserver.ServerSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    //CrestronConsole.PrintLine("{0} SocketStatusEvent. {1} not connected, status {2}", DeviceName, serverIpAddress, myserver.ServerSocketStatus.ToString());
                    if (myserver.ServerSocketStatus == SocketStatus.SOCKET_STATUS_NO_CONNECT)
                    {
                    }
                }
                else
                {
                    retries = 0;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} SocketStatusEventServer: {1}", DeviceName, e);
            }
        }
        private void ConnectCallbackAsyncServer(TCPServer myserver, uint clientIndex)
        {
            try
            {
                //CrestronConsole.PrintLine("ConnectCallbackAsync Start");
                if (myserver.ServerSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    if (retries % 10 == 0)
                    {
                        if (debug > 2)
                            CrestronConsole.PrintLine("{0} {1} ConnectCallbackAsyncServer {2} client {3}", DeviceName, myserver.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex), myserver.ServerSocketStatus.ToString(), clientIndex);
                    }
                    else
                    {
                        if (debug > 2)
                            CrestronConsole.Print(loading[retries % 4].ToString());
                    }
                    retries++;
                }
                else
                {
                    if (debug > 2)
                        CrestronConsole.PrintLine("{0} {1} ConnectCallbackAsyncServer {2}", DeviceName, serverIpAddress, myserver.ServerSocketStatus.ToString());
                    retries = 0;

                }
                if (server.NumberOfClientsConnected < server.MaxNumberOfClientSupported)
                {
                    if (myserver.ClientConnected(clientIndex))
                    {
                        CrestronConsole.PrintLine("{0} New Relay Client: {1} at {2}:{3}", DeviceName, clientIndex, myserver.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex), serverIpPort);
                        server.WaitForConnectionAsync(ConnectCallbackAsyncServer);//AcceptCallbackHost);
                        myserver.ReceiveDataAsync(clientIndex, new TCPServerReceiveCallback(ReceiveDataAsyncServer));
                    }
                    else
                        CrestronConsole.PrintLine("{0} Relay Client {1} is not connected, not calling ReceiveDataAsync", DeviceName, clientIndex);
                }
                else
                    CrestronConsole.PrintLine("{0} {1} server.MaxNumberOfClientSupported {2} reached", DeviceName, myserver.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex), server.MaxNumberOfClientSupported);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} ConnectCallbackAsyncServer: {1}", DeviceName, e);
            }
        }
        void ReceiveDataAsyncServer(TCPServer myserver, uint clientIndex, int rxSize)
        {
            try
            {
                //CrestronConsole.PrintLine("ReceiveDataAsynctargetComPort Start rxSize {0} client:{1}", rxSize, clientIndex);
                if (rxSize > 0)
                {//
                    string msg = Encoding.GetEncoding("ISO-8859-1").GetString(myserver.GetIncomingDataBufferForSpecificClient(clientIndex), 0, rxSize);
                    CrestronConsole.PrintLine("{0} {1} Rx from client {2}: {3}", DeviceName, myserver.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex), clientIndex, Utils.CreatePrintableString(msg, true));
                    queueToClient(msg);
                    if (msg.IndexOf("\x1C") == 0)
                    {
                        byte[] b = Utils.GetBytes(msg);
                        byte area = b[1];
                        if (!clientAreas.ContainsKey(clientIndex))
                        {
                            CrestronConsole.PrintLine("{0} {1} Client {2} is area {3}. Adding to dictionary", DeviceName, targetComPort.DeviceName, clientIndex, area);
                            clientAreas.Add(clientIndex, area);
                        }
                    }
                }
                myserver.ReceiveDataAsync(clientIndex, new TCPServerReceiveCallback(ReceiveDataAsyncServer));
                //CrestronConsole.PrintLine("Reset Relay Client: {0}", clientIndex);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} ReceiveDataAsyncServer {2}", DeviceName, serverIpAddress, e.Message);
            }
        }

        #endregion
        #region server side

        public void queueToClient(string str)
        {
            bool flagRestart = false;
            if (str != null && str.Length > 0)
            { // add this string to the relaying queue
                CrestronConsole.PrintLine("{0} queueToClient, queueNext:{1}, {2}", DeviceName, queueNext, Utils.CreatePrintableString(str, true));
                queueOut[queueNext] = str;
                if (queueStart == queueNext) // was stopped, flag a restart is needed
                    flagRestart = true;
                queueNext++;
                if (queueNext >= maxQueueSize)
                    queueNext = 0;
                if (flagRestart)
                {
                    queueTimer.Reset();
                    //CrestronConsole.PrintLine("{0} queueTimer.Reset()", DeviceName);
                }
            }
        }
        public void SetDelimServer(string str)
        {
            delimServer = str;
        }
        public bool DisConnectServer()
        {
            if (server.ServerSocketStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                CrestronConsole.PrintLine("{0} {1} server.DisconnectAll()", DeviceName, serverIpAddress);
                server.DisconnectAll();//  .DisconnectFromServer();
                //client.Dispose();
                return true;
            }
            return false;
        }
        public bool SendServer(string msg)
        {
            try
            {
                if (debug > 3)
                    CrestronConsole.PrintLine("{0} {1} Rx: {2}", DeviceName, targetComPort.DeviceName, Utils.CreatePrintableString(msg, debugAsHex));
                if (server != null)//ConnectServer())
                {
                    Byte[] b = Utils.GetBytes(AuthResponse + msg);
                    //CrestronConsole.PrintLine("server.MaxNumberOfClientSupported {0}", server.MaxNumberOfClientSupported);
                    AuthResponse = "";

                    if (msg.IndexOf("\x1C") == 0)
                    {
                        byte[] b1 = Utils.GetBytes(msg);
                        byte area = b1[1];
                        CrestronConsole.PrintLine("{0} {1} Send to area: {2}", DeviceName, targetComPort.DeviceName, area);
                        
                        foreach(KeyValuePair<uint,byte> ca in clientAreas)
                        {
                            if(ca.Value.Equals(area))
                            {
                                CrestronConsole.PrintLine("{0} {1} Send area {2} message to client {3}", DeviceName, targetComPort.DeviceName, area, ca.Key.ToString());
                                bool flag = false;
                                try
                                {
                                    flag = server.ClientConnected(ca.Key);
                                }
                                catch (Exception e)
                                {
                                    CrestronConsole.PrintLine("server.ClientConnected({0}) Exception\n{1}", ca.Key, e);
                                }
                                if (flag)
                                {
                                    SocketErrorCodes result = server.SendDataAsync(ca.Key, b, b.Length, null);
                                    if (debug > 1)
                                    {
                                        if (result == SocketErrorCodes.SOCKET_OK || result == SocketErrorCodes.SOCKET_OPERATION_PENDING)
                                            CrestronConsole.PrintLine("{0} {1} client {2} Tx: {3}", DeviceName, server.GetAddressServerAcceptedConnectionFromForSpecificClient(ca.Key), ca.Key, Utils.CreatePrintableString(b, debugAsHex));
                                        else
                                        {
                                            CrestronConsole.PrintLine("{0} {1} Tx Server sent with error: {2}, status: {3}, {4}", DeviceName, ca.Key, result, server.ServerSocketStatus, Utils.CreatePrintableString(b, debugAsHex));
                                            //DisConnect();
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //for (uint count = 1; count <= server.MaxNumberOfClientSupported; count++)
                        for (uint count = 1; count <= server.NumberOfClientsConnected; count++)
                        {
                            bool flag = false;
                            try
                            {
                                flag = server.ClientConnected(count);
                            }
                            catch (Exception e)
                            {
                                CrestronConsole.PrintLine("server.ClientConnected({0}) Exception\n{1}", count, e);
                            }
                            if (flag)
                            {
                                SocketErrorCodes result = server.SendDataAsync(count, b, b.Length, null);
                                if (debug > 1)
                                {
                                    if (result == SocketErrorCodes.SOCKET_OK || result == SocketErrorCodes.SOCKET_OPERATION_PENDING)
                                        CrestronConsole.PrintLine("{0} {1} client {2} Tx: {3}", DeviceName, server.GetAddressServerAcceptedConnectionFromForSpecificClient(count), count, Utils.CreatePrintableString(b, debugAsHex));
                                    else
                                    {
                                        CrestronConsole.PrintLine("{0} {1} Tx Server sent with error: {2}, status: {3}, {4}", DeviceName, count, result, server.ServerSocketStatus, Utils.CreatePrintableString(b, debugAsHex));
                                        //DisConnect();
                                    }
                                }
                            }
                        }
                    }
                    return true;
                }
                else
                {
                    ////Connect();   
                    if (debug > 1)
                        CrestronConsole.PrintLine("{0} {1} null, can't send Tx: {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(msg, debugAsHex));
                    //    CrestronConsole.PrintLine("{0} {1} Not online, can't send Tx: {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(msg, debugAsHex));
                    return false;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} SendServer Exception: {2}", DeviceName, serverIpAddress, e);
                return false;
            }
            //return false;
        }


//------------------------------------------------------- SERVER SIDE END ---------------------------

        #endregion
        #region comPort side
//------------------------------------------------------- targetComPort SIDE -------------------------------
        public void SetDelimtargetComPort(string str)
        {
            delimtargetComPort = str;
        }
        private void ComPort_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            int i = 0;
            int j = 0;
            try
            {
                //CrestronConsole.PrintLine("{0} {1} Rx: {2}", DeviceName, targetComPort.DeviceName, Utils.CreatePrintableString(args.SerialData, true));
                if (args.SerialData.Length > 0)
                {
                    SerialBuffer.Append(args.SerialData);
                    //CrestronConsole.PrintLine("Serialbuffer {0}", Utils.CreatePrintableString(SerialBuffer.ToString(), true));
                    while (SerialBuffer.Length > 7)
                    {
                        i = 0;
                        string str = SerialBuffer.ToString();
                        i++;
                        //CrestronConsole.PrintLine("{0} {1} SerialBuffer.Length: {2}, {3}", DeviceName, serverIpAddress, SerialBuffer.Length, Utils.CreatePrintableString(str, true));
                        int index = str.IndexOf("\x1C");
                        if (index > 0)
                        {
                            CrestronConsole.PrintLine("{0} {1} Removing leading junk from {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(str, true));
                            i = 10;
                            SerialBuffer.Remove(0, index);
                            i++;
                            CrestronConsole.PrintLine("{0} {1} SerialBuffer is now {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(SerialBuffer.ToString(), true));
                        }
                        else if (index == 0)
                        {
                            //CrestronConsole.PrintLine("{0} {1} SerialBuffer B4: {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(SerialBuffer.ToString(), true));
                            //string msg = SerialBuffer.Remove(0, 8).ToString(); // this just clears the buffer and leaves msg empty
                            //CrestronConsole.PrintLine("{0} {1} SendServer: {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(msg, true));
                            //CrestronConsole.PrintLine("{0} {1} SerialBuffer: {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(SerialBuffer.ToString(), true));
                            i = 20;
                            SendServer(SerialBuffer.ToString());
                            i++;
                            SerialBuffer.Length = 0;
                            i++;
                        }
                        else
                        {
                            CrestronConsole.PrintLine("{0} {1} clearing unwanted data: {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(str, true));
                            i = 30;
                            SerialBuffer.Length = 0;
                            i++;
                            //CrestronConsole.PrintLine("{0} {1} new len {2} val {3}", DeviceName, serverIpAddress, SerialBuffer.Length, Utils.CreatePrintableString(SerialBuffer.ToString(), true));
                        }
                        j++;
                    }
                }
                else
                {
                    CrestronConsole.PrintLine("Rx Length: {0}", args.SerialData.Length);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} ComPort_SerialDataReceived {2}-{3} exception: {4}", DeviceName, serverIpAddress, j,i, e.ToString());
            }
        }

        #endregion
    }
}
