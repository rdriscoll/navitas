using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes

// usage example
// RelayServer = new IPServerToSerialPortRelay(2300, "192.168.1.34", ComPorts[1], "IPToSerial");

namespace Navitas
{
    public class IPServerToSerialPortRelay
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

        TCPServer server;
        ComPort target;
        private CrestronQueue<String> serverRxQueue = new CrestronQueue<string>();
        private CrestronQueue<String> targetRxQueue = new CrestronQueue<string>();


        private int retries;
        private string loading = "-\\|/";
        private string delimServer = "\x0d";//\x0a";
        private string delimTarget = "\x0d";//\x0a";
        //private int keepAlive = 30;
        private byte debug = 5;
        public bool debugAsHex = true;

        public EthernetAdapterType etherPort = EthernetAdapterType.EthernetLANAdapter;

        public int serverMaxClients = 30;

        protected CTimer pollTimer;
        public int pollTimerRepeat = 6000;
        int pollTwice = 1;

        //private Crestron.SimplSharp.IPEndPoint RemoteEndPoint;
        //private System.Net.Sockets.Socket host;

        //private Boolean m_IsBound = false;
        //private List<Socket> m_Connections = new List<Socket>(50); // allow 50 sockets 
        //private static System.Threading.ManualResetEvent AllDone = new System.Threading.ManualResetEvent(false);

        #endregion
        #region comms

        public IPServerToSerialPortRelay(ComPort target, int serverPort, string targetAddress,
            EthernetAdapterType ether, int maxConnections, string name)
        {
            try
            {
                CrestronConsole.PrintLine("{0} Constructor", DeviceName);
                queueOut = new string[maxQueueSize];
                this.serverMaxClients = maxConnections;
                this.target = target;
                this.serverIpPort = serverPort;
                this.etherPort = ether;
                DeviceName = name;
                CreateIpServer();

                target.SerialDataReceived += new ComPortDataReceivedEvent(device_SerialDataReceived);
                if (target.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error("COM Port {0} couldn't be registered. Cause: {1}", target.DeviceName, target.DeviceRegistrationFailureReason);
                    CrestronConsole.PrintLine("COM Port {0} couldn't be registered. Cause: {1}", target.DeviceName, target.DeviceRegistrationFailureReason);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} constructor ERROR: {1}", DeviceName, e);
            }
        }

        protected void pollTimerExpired(object obj)
        {
            try
            {
                CrestronConsole.PrintLine("{0} pollTimerExpired {1}", DeviceName, pollTwice);
                if (pollTwice>0) pollTwice--;
                else
                {
                    CrestronConsole.PrintLine("{0} Start Relay Server", DeviceName);
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

                    if (target != null)
                    {
                        //Connect();
                    }
                    CrestronConsole.PrintLine("{0} Relay Server Setup", DeviceName);
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

        public void Dispose()
        {
            if (target != null)
            {
                target = null;
            }

            if (server != null)
            {
                //server.Stop();
                server.DisconnectAll();
                //server.Stop();
            }

        }

        public void SetDebug(byte b)
        {
            debug = b;
        }

        /*
public TCPServer(
	string addressToAcceptConnectionFrom,
	int portNumber,
	int bufferSize,
	EthernetAdapterType ethernetAdapterToBindTo,
	int numberOfConnections
)        */
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

//------------------------------------------------------- SERVER SIDE -------------------------------
        protected void queueTimerExpired(object obj)
        {

            //private int queueNext = 0, queueStart = 0;

            target.Send(queueOut[queueStart]);
            queueStart++;
            if (queueStart >= maxQueueSize)
                queueStart = 0;

            if (queueStart == queueNext) // queue caught up, stop till more data comes
                queueTimer.Stop();
        }

        public void queueToClient(string str)
        {
            bool flagRestart = false;
            if (str != null)
            if(str.Length>0)
            { // add this string to the relaying queue
                queueOut[queueNext] = str;
                if (queueStart == queueNext) // was stopped, flag a restart is needed
                    flagRestart = true;
                queueNext++;
                if (queueNext >= maxQueueSize)
                    queueNext = 0;
                if(flagRestart) queueTimer.Reset();
            }
        }

        public void SetDelimServer(string str)
        {
            delimServer = str;
        }

        void SocketStatusEventServer(TCPServer myserver, uint clientIndex, SocketStatus serverSocketStatus)
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
        private void ConnectCallbackAsyncServer(TCPServer myserver, uint clientIndex)
        {
            //CrestronConsole.PrintLine("ConnectCallbackAsync Start");
            if (myserver.ServerSocketStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                if (retries % 10 == 0)
                {
                    if (debug > 2)
                        CrestronConsole.PrintLine("\n{0} {1} ConnectCallbackAsyncServer {2} client {3}", DeviceName, serverIpAddress, myserver.ServerSocketStatus.ToString(), clientIndex);
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
            myserver.ReceiveDataAsync(clientIndex, new TCPServerReceiveCallback(ReceiveDataAsyncServer));
            myserver.WaitForConnectionAsync(ConnectCallbackAsyncServer);//AcceptCallbackHost);
            CrestronConsole.PrintLine("New Relay Client: {0}: {1}:{2}", clientIndex, myserver.GetAddressServerAcceptedConnectionFromForSpecificClient(clientIndex),serverIpPort);
        }


        void ReceiveDataAsyncServer(TCPServer myserver, uint clientIndex, int rxSize)
        {
            //CrestronConsole.PrintLine("ReceiveDataAsyncTarget Start rxSize {0} client:{1}", rxSize, clientIndex);
            if (rxSize > 0)
            {//
                string msg = Encoding.GetEncoding("ISO-8859-1").GetString(myserver.GetIncomingDataBufferForSpecificClient(clientIndex), 0, rxSize);
                queueToClient(msg);
                //CrestronConsole.PrintLine("{0} ReceiveCallbackAsyncServer. buffer: {1}", DeviceName, msg);
            }
            myserver.ReceiveDataAsync(clientIndex, new TCPServerReceiveCallback(ReceiveDataAsyncServer));
            //CrestronConsole.PrintLine("Reset Relay Client: {0}", clientIndex);
        }

        public bool DisConnectServer()
        {
            if (server.ServerSocketStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
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
                    CrestronConsole.PrintLine("{0} {1} Message to Send: {2}", DeviceName, serverIpAddress, Utils.CreatePrintableString(msg, debugAsHex));
                if (server != null)//ConnectServer())
                {
                    Byte[] b = Utils.GetBytes(AuthResponse + msg);
                    //CrestronConsole.PrintLine("server.MaxNumberOfClientSupported {0}", server.MaxNumberOfClientSupported);
                    AuthResponse = "";
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
                                    CrestronConsole.PrintLine("{0} {1}, {2} Tx: {3}", DeviceName, count, server.GetAddressServerAcceptedConnectionFromForSpecificClient(count), Utils.CreatePrintableString(b, debugAsHex));
                                else
                                {
                                    CrestronConsole.PrintLine("{0} {1} Tx Server sent with error: {2}, status: {3}, {4}", DeviceName, count, result, server.ServerSocketStatus, Utils.CreatePrintableString(b, debugAsHex));
                                    //DisConnect();
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
//------------------------------------------------------- TARGET SIDE -------------------------------
        public void SetDelimTarget(string str)
        {
            delimTarget = str;
        }

        private void device_SerialDataReceived(ComPort ReceivingComPort, ComPortSerialDataEventArgs args)
        {
            if (args.SerialData.Length > 0)
            {
                SendServer(args.SerialData);
            }
        }

        #endregion
    }
}