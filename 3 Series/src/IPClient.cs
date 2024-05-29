using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading

namespace Navitas
{
    public class IPClient
    {
        public delegate void OnlineEventHandler(object sender, BoolEventArgs e);
        public delegate void StringEventHandler(object sender, StringEventArgs e);

        public event OnlineEventHandler OnlineFb;
        public event StringEventHandler ParseRxData;

        public Action CommunicationReceived;

        #region variables

        public string DeviceName = "TCP Client";
        private string IpAddress;
        public int IpPort = 23;
        public string Password = "";         // Stores the Password
        private string AuthResponse = "";     // Authentication Required
        private int BufferSize = 1024;
        //x private Thread RxHandler;
        TCPClient client;
        //private CrestronQueue<String> RxQueue = new CrestronQueue<string>();
        private int retries;
        private string loading = "-\\|/";
        private string delim;// = "\x0d\x0a";
        //private int keepAlive = 30;
        private byte debug = 0;
        public bool debugAsHex = false;

        protected CTimer queueTimer;
        protected CTimer RxTimer;
        protected CTimer retryTimer;
        public int queueTimerRepeat = 500;
        public int retryTimeOut = 500;
        public int RxTimeout = 60000;
        private byte RxWaiting = 0;
        private byte RetryFlag = 0;
        private const int maxQueueSize = 30;
        private int queueNext = 0, queueStart = 0;
        private string[] queueOut;

        #endregion
        #region comms

        public IPClient(string IpAddress, int IpPort, string name)
        {
            try
            {
                queueOut = new string[maxQueueSize];
                this.IpAddress = IpAddress;
                this.IpPort = IpPort;
                DeviceName = name;
                CreateIpClient();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} constructor ERROR: {2}", DeviceName, IpAddress, e);
            }
        }
        public void Dispose()
        {
            //x RxHandler.Abort();
            //x if (RxQueue != null)
            //x     loopEnable = false;
            if (client != null)
            {
                try
                {
                    client.DisconnectFromServer();
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("{0} {1} Dispose DisconnectFromServer {2}", DeviceName, IpAddress, e.Message);
                }
            }
            if (client != null)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("{0} {1} Dispose {2}", DeviceName, IpAddress, e.Message);
                }
            }
            if (RxTimer != null)
            {
                RxTimer.Stop();
                RxTimer.Dispose();
            }
            if (queueTimer != null)
            {
                queueTimer.Stop();
                queueTimer.Dispose();
            }
        }
        public void HandleLinkUp()
        {
            client.HandleLinkUp();
        }
        public void HandleLinkLoss()
        {
            client.HandleLinkLoss();
        }
        protected void CreateIpClient()
        {
            try
            {
                if (debug > 0)
                    CrestronConsole.PrintLine("{0} {1} CreateIpClient", DeviceName, IpAddress);
                client = new TCPClient(new IPEndPoint(IPAddress.Parse(IpAddress), IpPort), BufferSize);
                if (client != null)
                {
                    client.SocketStatusChange += new TCPClientSocketStatusChangeEventHandler(SocketStatusEvent);
                    client.SocketSendOrReceiveTimeOutInMs = 0;//100000;
                    Connect();
                    queueTimer = new CTimer(queueTimerExpired, this, 1, queueTimerRepeat);
                    queueTimer.Stop();
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} CreateIpClient ERROR: {2}", DeviceName, IpAddress, e);
            }
        }
        public bool Connect()
        {
            //if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)// && client.ClientStatus != SocketStatus.SOCKET_STATUS_WAITING)
            if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED && client.ClientStatus != SocketStatus.SOCKET_STATUS_WAITING)
            {
                try
                {
                    SocketErrorCodes result = client.ConnectToServerAsync(ConnectCallbackAsync);
                    //if (debug > 4)
                    //    CrestronConsole.PrintLine("{0} {1} Connect to Server {2}", DeviceName, IpAddress, result);
                    return client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED || result == SocketErrorCodes.SOCKET_OPERATION_PENDING ? true : false;
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("{0} {1} ConnectToServerAsync: {2}", DeviceName, IpAddress, e.Message);
                }
            }
            return client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED ? true : false;
        }
        public bool DisConnect()
        {
            if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
            {
                CrestronConsole.PrintLine("{0} {1} client.DisconnectFromServer()", DeviceName, IpAddress);
                client.DisconnectFromServer();
                return true;
            }
            return false;
        }

        #endregion 
        #region callbacks

        private void SocketStatusEvent(TCPClient client, SocketStatus clientSocketStatus)
        {
            try
            {
                if (debug > 2)
                    CrestronConsole.PrintLine("{0} {1} SocketStatusEvent {2}", DeviceName, IpAddress, client.ClientStatus.ToString());
                if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    if (client.ClientStatus == SocketStatus.SOCKET_STATUS_NO_CONNECT)
                    {}
                    //CrestronConsole.PrintLine("{0} {1} SocketStatus == !connected, calling Disconnect()", DeviceName, IpAddress);
                    //DisConnect(); // Todo - stop from doing this multiple times quickly
                    Connect();
                }
                else
                {
                    retries = 0;
                    RetryFlag = 0;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} SocketStatusEvent: {2}", DeviceName, IpAddress, e.Message);
            }
        }
        private void ConnectCallbackAsync(TCPClient client)
        {
            try
            {
                if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    if (RetryFlag == 0)
                    {
                        RetryFlag = 2;
                        if (retryTimer == null)
                            retryTimer = new CTimer(retryTimerExpired, this, 1, retryTimeOut);
                        else
                            retryTimer.Reset();
                    }
                }
                else
                {
                    retries = 0;
                    RetryFlag = 0;
                    if(OnlineFb != null)
                        OnlineFb(this, new BoolEventArgs(true));
                    client.ReceiveDataAsync(new TCPClientReceiveCallback(ReceiveDataAsync));
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} ConnectCallbackAsync: {2}", DeviceName, IpAddress, e.Message);
            }
        }
        private void ReceiveDataAsync(TCPClient client, int rxSize)
        {
            try
            {
                if (rxSize > 0)
                {
                    if (RxWaiting > 1)
                    {
                        RxWaiting = 0;
                        if (RxTimer != null)
                            RxTimer.Stop();
                    }
                    string msg = Encoding.GetEncoding("ISO-8859-1").GetString(client.IncomingDataBuffer, 0, rxSize);
                    if (msg.Length > 0)
                    {
                        if (debug > 0)
                            CrestronConsole.PrintLine("{0} {1} Rx: {2}", DeviceName, IpAddress, Utils.CreatePrintableString(msg, debugAsHex));
                        //x RxQueue.Enqueue(msg);
                        pushToQueue(msg);
                    }
                    else
                        CrestronConsole.PrintLine("{0 {1} ReceiveCallbackAsync. empty message: {2}", DeviceName, IpAddress, msg);
                }
                if (client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                    client.ReceiveDataAsync(new TCPClientReceiveCallback(ReceiveDataAsync));
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} ReceiveDataAsync: {2}", DeviceName, IpAddress, e.Message);
            }
        }
        private void SendCallBack(TCPClient client, int bytes)
        {
            try
            {
                //CrestronConsole.PrintLine("{0} {1} bytes sent {2}", DeviceName, IpAddress, bytes);
                if (bytes < 1)
                {
                    CrestronConsole.PrintLine("{0} {1} ClientStatus {2}", DeviceName, IpAddress, client.ClientStatus);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} SendCallBack: {2}", DeviceName, IpAddress, e.Message);
            }

        }
        
        #endregion
        #region general

        public void SetDelim(string str)
        {
            delim = str;
        }
        public void SetDebug(byte b)
        {
            debug = b;
        }
        public bool Send(string msg)
        {
            try
            {
                if (Connect())//client.ClientStatus == SocketStatus.SOCKET_STATUS_CONNECTED)
                {
                    Byte[] b = Utils.GetBytes(AuthResponse + msg);
                    AuthResponse = "";
                    SocketErrorCodes result = client.SendDataAsync(b, b.Length, SendCallBack);
                    if (RxWaiting == 0 && (result == SocketErrorCodes.SOCKET_OK || result == SocketErrorCodes.SOCKET_OPERATION_PENDING))
                    {
                        CrestronConsole.PrintLine("{0} {1} Starting RxTimer", DeviceName, IpAddress);
                        RxWaiting = 2;
                        if (RxTimer == null)
                            RxTimer = new CTimer(RxTimerExpired, this, 1, RxTimeout);
                        else
                            RxTimer.Reset();
                    }
                    if (debug > 1)
                    {
                        if (result == SocketErrorCodes.SOCKET_OK || result == SocketErrorCodes.SOCKET_OPERATION_PENDING)
                            CrestronConsole.PrintLine("{0} {1} Tx: {2}", DeviceName, IpAddress, Utils.CreatePrintableString(b, debugAsHex));
                        else
                            CrestronConsole.PrintLine("{0} {1} Tx sent with error: {2}, status: {3}, {4}", DeviceName, IpAddress, result, client.ClientStatus, Utils.CreatePrintableString(b, debugAsHex));
                    }
                    return true;
                }
                else
                {
                    if (debug > 1)
                        CrestronConsole.PrintLine("{0} {1} Not online, can't send Tx: {2}", DeviceName, IpAddress, Utils.CreatePrintableString(msg, debugAsHex));
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} Send Exception: {2}", DeviceName, IpAddress, e);
                return false;
            }
            return false;
        }
        public void pushToQueue(string str)
        {
            bool flagRestart = false;
            if (str != null)
                if (str.Length > 0)
                { // add this string to the relaying queue
                    queueOut[queueNext] = str;
                    if (queueStart == queueNext) // was stopped, flag a restart is needed
                        flagRestart = true;
                    queueNext++;
                    if (queueNext >= maxQueueSize)
                        queueNext = 0;
                    if (flagRestart) queueTimer.Reset();
                }
        }
        protected void queueTimerExpired(object obj)
        {
            try
            {
                //private int queueNext = 0, queueStart = 0;
                if (ParseRxData != null)
                    ParseRxData(this, new StringEventArgs(queueOut[queueStart]));
                queueStart++;
                if (queueStart >= maxQueueSize)
                    queueStart = 0;
                if (queueStart == queueNext) // queue caught up, stop till more data comes
                    queueTimer.Stop();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} queueTimerExpired: {2}", DeviceName, IpAddress, e.Message);
            }
        }
        protected void RxTimerExpired(object obj)
        {
            try
            {
                if (RxWaiting == 1)
                {
                    RxWaiting = 0;
                    if (RxTimer != null)
                        RxTimer.Stop();
                    CrestronConsole.PrintLine("{0} {1} RxTimerExpired: disconnecting", DeviceName, IpAddress);
                    DisConnect();
                }
                else if (RxWaiting > 0)
                    RxWaiting--;
                else if (RxTimer != null)
                    RxTimer.Stop();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} RxTimerExpired: {2}", DeviceName, IpAddress, e.Message);
            }
        }
        protected void retryTimerExpired(object obj)
        {
            try
            {
                if (RetryFlag == 1)
                {
                    RetryFlag = 0;
                    if (retryTimer != null)
                        retryTimer.Stop();
                    //CrestronConsole.PrintLine("{0} {1} RxTimerExpired: disconnecting", DeviceName, IpAddress);
                    if (client.ClientStatus != SocketStatus.SOCKET_STATUS_CONNECTED)
                    {
                        Connect();
                        if (retries % 50 == 0)
                        {
                            if (OnlineFb != null)
                                OnlineFb(this, new BoolEventArgs(false));
                            if (debug > 2 && retries > 0)
                                CrestronConsole.PrintLine("{0} {1} Waiting, retries: {2}, {3}", DeviceName, IpAddress, retries, client.ClientStatus);
                        }
                        else
                        {
                            if (debug > 2)
                                CrestronConsole.Print(loading[retries % 4].ToString());
                        }
                        retries++;
                    }
                }
                else if (RetryFlag > 0)
                    RetryFlag--;
                else if (retryTimer != null)
                    retryTimer.Stop();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} {1} retryTimerExpired: {2}", DeviceName, IpAddress, e.Message);
            }
        }

        #endregion
    }
}
