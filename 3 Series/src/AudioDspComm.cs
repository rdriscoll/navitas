using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronSockets;
using Crestron.SimplSharpPro.CrestronThread;        	// For Threading
using AVPlus.ThirdPartyCommon;
//using AVPlus.ThirdPartyCommon.Custom;

namespace Navitas
{
    public class AudioDspComm
    {
        public delegate void AudioNodeEventHandler(object sender, BSSAudioNodeEventArgs e);
        public delegate void StringEventHandler(object sender, StringEventArgs e);
        public delegate void OnlineEventHandler(object sender, BoolEventArgs e);
        //public event OnlineEventHandler OnlineFb;
        public OnlineEventHandler OnlineFb;
        public event StringEventHandler Send;
        public AudioNodeEventHandler AudioNodeFb;

        //vars
        //public ControlSystem cs;
        StringBuilder RxData = new StringBuilder();

        protected String _name = "Audio DSP";
        public IPClient comms;

        //private string Password = "";         // Stores the Password
        //private ushort Port = 1023;
        public List<BssNode> nodes = new List<BssNode>();
        public bool subscribe;
        public bool temp_mute;
        private string delimiter = "\x03";
        protected CTimer pollTimer;
        public int pollTimerRepeat = 30000;

        #region dictionaries

        public Dictionary<byte, string> BSS_VIRTUAL_DEVICE_ID = new Dictionary<byte, string>()
        {
            { 0x00, "Basic setting" },
            { 0x01, "Links" },
            { 0x02, "Logic object" },
            { 0x03, "Audio processing object" }
        };

        public Dictionary<byte, string> BSS_NODE_FUNC_DICT = new Dictionary<byte, string>()// BSS_NODE_FUNC
        {
            { 0, "n-input gain" },
            { 1, "Mixer gain" },
            { 2, "source select" },
            { 3, "matrix router" },
            { 4, "matrix mixer" }
        };

        #endregion
        #region constructor

        public void Dispose()
        {
            //CrestronConsole.PrintLine("Audio DSP: {0} Dispose", _name);
            if (comms != null)
                comms.Dispose();
            if (pollTimer != null)
            {
                pollTimer.Stop();
                pollTimer.Dispose();
            }
            //CrestronConsole.PrintLine("Audio DSP: {0} Dispose done", _name);
        }
        public AudioDspComm(IPClient comms)
        {
            try
            {
                this.comms = comms;
                comms.SetDelim(delimiter);
                comms.ParseRxData += new IPClient.StringEventHandler(ParseRx);
                comms.debugAsHex = true;
                comms.SetDebug(1);
                pollTimer = new CTimer(pollTimerExpired, this, 1, pollTimerRepeat);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} constructor {1}", comms.DeviceName, e.Message);
            }
        }
        public void SendString(string str)
        {
            //CrestronConsole.PrintLine("{0} SendString {1}", _name, Utils.CreateHexPrintableString(str));
            if (Send != null)
                Send(this, new StringEventArgs(str));
            if (comms != null)
                comms.Send(str);
        }

        #endregion
        #region commands
        /*
        public void VolUp(ushort deviceID, uint objectID, int index)
        {
            SetBSSnInputGainDB(deviceID, objectID, index, 10);
        }
        public void VolDown(ushort deviceID, uint objectID, int index)
        {
            SetBSSnInputGainDB(deviceID, objectID, index, -20);
        }
         * */
        public void SetVolume(ushort deviceID, uint objectID, int index, int level)
        {
            SetBSSnInputGainDB(deviceID, objectID, index, level);
        }
        public void SetVolume(ushort deviceID, uint objectID, int level)
        {
            SetBSSnInputGainDB(deviceID, objectID, level);
        }
        public void SetVolumePercent(ushort deviceID, uint objectID, int index, int level)
        {
            SetBSSnInputGainPercent(deviceID, objectID, index, level);
        }
        public void VolMute(ushort deviceID, uint objectID, int index, bool state)
        {
            temp_mute = state;
            SetBSSnInputGainMute(deviceID, objectID, index, temp_mute);
        }
        public void VolMute(ushort deviceID, uint objectID, bool state) // master
        {
            temp_mute = state;
            SetBSSnInputGainMute(deviceID, objectID, temp_mute);
        }
        public void VolMute(ushort deviceID, uint objectID, int index)
        {
            temp_mute = !temp_mute;
            VolMute(deviceID, objectID, index, temp_mute);
        }
        private void MakeBssString(string s) // BSS
        {
            byte chk = 0;
            List<byte> bl = Utils.GetBytes(s).ToList<byte>();
            byte[] b1 = bl.ToArray();
            foreach (byte b in bl)
                chk = (byte)(chk ^ b);
            bl.Add(chk);
            int i;
            for (i = 0; i < bl.Count; i++)
                if (bl[i] == 0x1B)
                    bl.Insert(i + 1, 0x95);
            byte[] items = { 0x02, 0x03, 0x06, 0x15 };
            for (i = 0; i < bl.Count; i++)
            {
                if (items.Contains(bl[i]))
                {
                    bl[i] = (byte)(bl[i] + 0x80);
                    bl.Insert(i, 0x1b);
                }
            }
            //byte[] b2 = bl.ToArray();
            string str = "\x02" + Utils.GetString(bl.ToArray()) + "\x03";
            SendString(str);
            //ParseBss(str);
        }
        #region mixer-gains

        /* mixer gains
         * in 5, 5dB: 02, 88, 85, 8C, 1B, 83, 00, 01, 20, [01, F4, 00, 00, C3, 50], C5, 03
         */
        public void SetBSSMixerGainAbsolute(ushort deviceID, uint objectID, int index, int level)
        { // level = dB * 100
            double lev;
            if (level < -1000)
                lev = (Math.Log10(Math.Abs(level / 100)) * -200000) + 100000;
            else
                lev = level * 100; // -1000 -> -100,000
            MakeBssString(String.Format("{0}{1}\x03{2}{3}{4}",
                         Utils.GetString(new byte[] { (byte)BSS_DI.DI_SETSV }),
                         Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                         Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                         Utils.GetString(Utils.GetBytesFromLong((index - 1) * 0x64, 2)),
                         Utils.GetString(Utils.GetBytesFromLong((long)lev, 4))));
            BssNode bn = nodes.Find(x => x.ObjectID == (uint)objectID);
            if (bn == null)
            {
                CrestronConsole.PrintLine("adding MixerGainAbsolute, deviceID: 0x{0:X2}, objectID:0x{1:X3}, index:0x{2:X2}, level: 0x{3:X4}", deviceID, objectID, (index - 1) * 0x64, lev);
                bn = new BssNode(deviceID, objectID, BSS_NODE_FUNC.GAIN_MIXER);
                nodes.Add(bn);
                MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00\x00",
                             Utils.GetString(new byte[] { (byte)BSS_DI.DI_SUBSCRIBESV }),
                             Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                             Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                             Utils.GetString(Utils.GetBytesFromLong((index - 1) * 0x64, 2))));
            }
        }
        public void SetBSSMixerGainMute(ushort deviceID, uint objectID, int index, bool state)
        {
            MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00{4}",
                          Utils.GetString(new byte[] { (byte)BSS_DI.DI_SETSV }),
                          Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                          Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                          Utils.GetString(Utils.GetBytesFromLong(0x1F + index, 2)),
                          state == true ? "\x01" : "\x00"));
            BssNode bn = nodes.Find(x => x.ObjectID == (uint)objectID);
            if (bn == null)
            {
                CrestronConsole.PrintLine("adding MixerGainMute, deviceID: 0x{0:X2}, objectID:0x{1:X3}, index:0x{2:X2}, state: {3}", deviceID, objectID, 0x1F + index, state);
                bn = new BssNode(deviceID, objectID, BSS_NODE_FUNC.GAIN_MIXER);
                nodes.Add(bn);
                MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00\x00",
                             Utils.GetString(new byte[] { (byte)BSS_DI.DI_SUBSCRIBESV }),
                             Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                             Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                             Utils.GetString(Utils.GetBytesFromLong(0x1F + index, 2))));
            }
        }
        public void SetBSSMixerGainAbsolute(ushort deviceID, uint objectID, int level) // BSS master
        {
            SetBSSMixerGainAbsolute(deviceID, objectID, 0xC9, level); // index byte for master 1 is 0x4E20, master 2 is 0x4E22 (not supported)
        }
        public void SetBSSMixerGainDB(ushort deviceID, uint objectID, int level) // BSS master
        {
            SetBSSMixerGainAbsolute(deviceID, objectID, level * 100);
        }
        public void SetBSSMixerGainDB(ushort deviceID, uint objectID, int index, int level)
        {
            SetBSSMixerGainAbsolute(deviceID, objectID, index, level * 100); // level to 2 decimal places
        }
        public void SetBSSMixerGainMute(ushort deviceID, uint objectID, bool state)
        {
            SetBSSnInputGainMute(deviceID, objectID, 0x42, state); // index byte for master is 0x42 + 0x1F = 0x61
        }

        #endregion
        #region n-input-gains //0x00010300012D
        /* n-input gains
         * gain 1  10db: 02, 88, 85, 8C, 1B, 83, 00, 01, 2E, [00, 00, 00, 01, 86, A0], 8A, 03
         * gain 6 -10db: 02, 88, 85, 8C, 1B, 83, 00, 01, 2E, [00, 05, FF, FE, 79, 60], B0, 03
         */
        public void SetBSSnInputGain(ushort deviceID, uint objectID, int index, long lev)
        { // level = dB * 100
            try
            {
                string s1 = Utils.GetString(new byte[] { (byte)BSS_DI.DI_SETSV });
                string s2 = Utils.GetString(Utils.GetBytesFromLong(deviceID, 2));
                string s3 = Utils.GetString(Utils.GetBytesFromLong(objectID, 3));
                string s4 = Utils.GetString(Utils.GetBytesFromLong(index - 1, 2));
                string s5 = Utils.GetString(Utils.GetBytesFromLong(lev, 4));
                string s6 = String.Format("{0}{1}\x03{2}{3}{4}", s1, s2, s3, s4, s5);
                byte[] b = Utils.GetBytes(s1);

                MakeBssString(s6);
                //MakeBssString(String.Format("{0}{1}\x03{2}{3}{4}",
                //              Utils.GetString(new byte[] { (byte)BSS_DI.DI_SETSV }),
                //              Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                //              Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                //              Utils.GetString(Utils.GetBytesFromLong(index - 1, 2)),
                //              Utils.GetString(Utils.GetBytesFromLong(l, 4))));
                BssNode bn = nodes.Find(x => x.ObjectID == (uint)objectID);
                if (bn == null)
                {
                    CrestronConsole.PrintLine("adding InputGain, deviceID: 0x{0:X2}, objectID:0x{1:X3}, index:0x{2:X2}, level: 0x{3:X4}", deviceID, objectID, index - 1, lev);
                    bn = new BssNode(deviceID, objectID, BSS_NODE_FUNC.GAIN_N_INPUT);
                    nodes.Add(bn);
                    MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00\x00",
                                 Utils.GetString(new byte[] { (byte)BSS_DI.DI_SUBSCRIBESV }),
                                 Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                                 Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                                 Utils.GetString(Utils.GetBytesFromLong(0x1F + index, 2))));
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} SetBSSnInputGainAbsolute Exception: {1}", comms.DeviceName, e);
            }
        }
        public void SetBSSnInputGainAbsolute(ushort deviceID, uint objectID, int index, int level)
        { // level = dB * 100
            double lev;
            if (level < -1000)
                lev = (Math.Log10(Math.Abs(level / 100)) * -200000) + 100000;
            else
                lev = level * 100; // -1000 -> -100,000
            SetBSSnInputGain(deviceID, objectID, index, (long)lev);
        }
        public void SetBSSnInputGainAbsolute(ushort deviceID, uint objectID, int level) // BSS master
        {
            SetBSSnInputGainAbsolute(deviceID, objectID, 0x61, level); // index byte for master is 0x61
        }
        public void SetBSSnInputGainDB(ushort deviceID, uint objectID, int level) // BSS master
        {
            SetBSSnInputGainAbsolute(deviceID, objectID, level * 100);
        }
        public void SetBSSnInputGainDB(ushort deviceID, uint objectID, int index, int level)
        {
            SetBSSnInputGainAbsolute(deviceID, objectID, index, level * 100); // level to 2 decimal places
        }
        public void SetBSSnInputGainPercent(ushort deviceID, uint objectID, int index, int level)
        {
            SetBSSnInputGain(deviceID, objectID, index, level * 10000); // level to 2 decimal places
        }
        /*
         * gain 1 mute on : 02, 88, 85, 8C, 1B, 83, 00, 01, 2E, [00, 20, 00, 00, 00, 01], 8C, 03
         * gain 8 mute on : 02, 88, 85, 8C, 1B, 83, 00, 01, 2E, [00, 27, 00, 00, 00, 01], 8B, 03
         * gain 8 mute off: 02, 88, 85, 8C, 1B, 83, 00, 01, 2E, [00, 27, 00, 00, 00, 00], 8B, 03
         *          "     \x02\x88 {0}    \x1B\x83 {1}          {2}    \x00\x00\x00 {3}"
         */
        public void SetBSSnInputGainMute(ushort deviceID, uint objectID, int index, bool state)
        {
            MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00{4}",
                          Utils.GetString(new byte[] { (byte)BSS_DI.DI_SETSV }),
                          Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                          Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                          Utils.GetString(Utils.GetBytesFromLong(0x1F + index, 2)),
                          state == true ? "\x01" : "\x00"));
            BssNode bn = nodes.Find(x => x.ObjectID == (uint)objectID);
            if (bn == null)
            {
                CrestronConsole.PrintLine("adding InputGainMute, deviceID: 0x{0:X2}, objectID:0x{1:X3}, index:0x{2:X2}, level: 0x{3:X4}", deviceID, objectID, 0x1F + index, state);
                bn = new BssNode(deviceID, objectID, BSS_NODE_FUNC.GAIN_N_INPUT);
                nodes.Add(bn);
                MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00\x00",
                             Utils.GetString(new byte[] { (byte)BSS_DI.DI_SUBSCRIBESV }),
                             Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                             Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                             Utils.GetString(Utils.GetBytesFromLong(0x1F + index, 2))));
            }
        }
        public void SetBSSnInputGainMute(ushort deviceID, uint objectID, bool state)
        {
            SetBSSnInputGainMute(deviceID, objectID, 0x42, state); // index byte for master is 0x42 + 0x1F = 0x61
        }
        #endregion
        #region source matrix
        /*
         * source 1-1: 02, 88, 85, 8C, 1B, 83, 00, 01, 6A, 00, 00, 00, 00, 00, 01, E8, 03
         * source 3-4: 02, 88, 85, 8C, 1B, 83, 00, 01, 6A, 00, 1B, 82, 00, 00, 00, 04, EF, 03
         * source 1-2: 02, 88, 85, 8C, 1B, 83, 00, 01, 6A, 00, 01, 00, 00, 00, 01, E9, 03
         *          "\x02\x88 {0}    \x1B\x83 {1}          {2}   \x00\x00  {3}",
         */
        public void SetBSSSourceMatrix(ushort deviceID, uint objectID, int input, int output)
        {
            MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00{4}",
                        Utils.GetString(new byte[] { (byte)BSS_DI.DI_SETSV }),
                        Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                        Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                        Utils.GetString(Utils.GetBytesFromLong(output - 1, 2)),
                        Utils.GetString(Utils.GetBytesFromLong(input, 2))));
            BssNode bn = nodes.Find(x => x.ObjectID == (uint)objectID);
            if (bn == null)
            {
                CrestronConsole.PrintLine("adding SourceMatrix object 0x{0:X3}", objectID);
                bn = new BssNode(deviceID, objectID, BSS_NODE_FUNC.SOURCE_SELECT);
                nodes.Add(bn);
                MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00\x00",
                             Utils.GetString(new byte[] { (byte)BSS_DI.DI_SUBSCRIBESV }),
                             Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                             Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                             Utils.GetString(Utils.GetBytesFromLong(output - 1, 2))));
            }
        }
        public void SetBSSSourceSelector(ushort deviceID, uint objectID, int index)
        {
            SetBSSSourceMatrix(deviceID, objectID, index, 1);
        }
        #endregion
        #region matrix-router
        /*
         * 1-1 on : 02, 88, 85, 8C, 1B, 83, 00, 01, 6B, [00, 00, 00, 00, 00, 01], E9, 03
         * 1-1 off: 02, 88, 85, 8C, 1B, 83, 00, 01, 6B, [00, 00, 00, 00, 00, 00], E8, 03
         * 7-4 on : 02, 88, 85, 8C, 1B, 83, 00, 01, 6B, [01, 86, 00, 00, 00, 01], 6E, 03 // input is lower byte of \x86
         *       "\x02\x88 {0}    \x1B\x83 {1}          {2}    \x00\x00\x00 {3}",
         * in and out are {2}
         *  in 1 =     \x?0,  in 2 =     \x?1,  in 3 =     \x?2
         * out 1 = \x00\x0?, out 2 = \x00\x8?, out 3 = \x01\x0?, out 4 = \x01\x8?
         */
        public void SetBSSMatrixRouter(ushort deviceID, uint objectID, int input, int output, bool state)
        {
            MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00{4}",
                        Utils.GetString(new byte[] { (byte)BSS_DI.DI_SETSV }),
                        Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                        Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                        Utils.GetString(Utils.GetBytesFromLong((output - 1) * 0x80 + (input - 1), 2)),
                        state == true ? "\x01" : "\x00"));
            BssNode bn = nodes.Find(x => x.ObjectID == (uint)objectID);
            if (bn == null)
            {
                CrestronConsole.PrintLine("adding SourceMatrix object 0x{0:X3}", objectID);
                bn = new BssNode(deviceID, objectID, BSS_NODE_FUNC.MATRIX_ROUTER);
                nodes.Add(bn);
                MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00\x00",
                             Utils.GetString(new byte[] { (byte)BSS_DI.DI_SUBSCRIBESV }),
                             Utils.GetString(Utils.GetBytesFromLong(deviceID, 2)),
                             Utils.GetString(Utils.GetBytesFromLong(objectID, 3)),
                             Utils.GetString(Utils.GetBytesFromLong((output - 1) * 0x80 + (input - 1), 2))));
            }
        }
        #endregion
        /*
         * preset 0: 02, 8C, 00, 00, 00, 00, 8C, 03
         * preset 1: 02, 8C, 00, 00, 00, 01, 8D, 03
         */
        public void SetBSSParamPreset(int index)
        {
            CrestronConsole.PrintLine("SetBSSParamPreset {0}: {1}", index, Utils.CreatePrintableString(
                String.Format("\x8C{0}", Utils.GetString(Utils.GetBytesFromLong(index, 4))),true));
            MakeBssString(String.Format("\x8C{0}", Utils.GetString(Utils.GetBytesFromLong(index, 4))));
        }
        public void SetBSSVenuePreset(int index)
        {
            CrestronConsole.PrintLine("SetBSSVenuePreset {0}: {1}", index, Utils.CreatePrintableString(
               String.Format("\x8B{0}", Utils.GetString(Utils.GetBytesFromLong(index, 4))), true));
            MakeBssString(String.Format("\x8B{0}", Utils.GetString(Utils.GetBytesFromLong(index, 4))));
        }
        #endregion
        
        protected void pollTimerExpired(object obj) // TODO - poll correctly, this is just a random subscribe poll to keep the connection alive
        {
            try
            {
                if (nodes != null && nodes.Count > 0)
                {
                    CrestronConsole.PrintLine("BSS poll");
                    MakeBssString(String.Format("{0}{1}\x03{2}{3}\x00\x00\x00\x00",
                                 Utils.GetString(new byte[] { (byte)BSS_DI.DI_SUBSCRIBESV }),
                                 Utils.GetString(Utils.GetBytesFromLong(nodes[0].DeviceID, 2)),
                                 Utils.GetString(Utils.GetBytesFromLong(nodes[0].ObjectID, 3)),
                                 Utils.GetString(Utils.GetBytesFromLong(0x1F + 1, 2))));
                }
                else if (nodes == null)
                    CrestronConsole.PrintLine("BSS can't poll, nodes == null");
                else
                    CrestronConsole.PrintLine("BSS can't poll, nodes.Count: {0}", nodes.Count);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} pollTimerExpired {1}", comms.DeviceName, e.Message);
            }
        }
        public void ParseRx(object sender, StringEventArgs args)
        {
            string str = args.str;
            CrestronConsole.PrintLine("{0} ParseRx, len({1}) {2}", _name, str.Length, str);
            bool got_escape = false;
            byte checksum = 0;
            byte prev_checksum = 0;
            try
            {
                while (str.Contains("\x02") && str.Contains("\x03"))
                {
                    CrestronConsole.PrintLine("{0} found valid string: {0}", comms.DeviceName, Utils.CreateHexPrintableString(str));
                    byte[] b1 = new byte[str.Length];
                    int index = str.IndexOf("\x03");
                    int len = str.Length;
                    if (index < str.Length - 1)
                    {
                        b1 = Utils.GetBytes(str.Remove(index + 1));
                        str = str.Substring(index + 1);
                    }
                    else
                    {
                        b1 = Utils.GetBytes(str);
                        str = String.Empty;
                    }
                    CrestronConsole.PrintLine("{0} str: {1}", comms.DeviceName, Utils.CreateHexPrintableString(str));
                    StringBuilder sb = new StringBuilder();
                    foreach (byte b in b1)
                    {
                        if (b == 0x02) // STX
                        {
                            checksum = 0;
                            sb.Length = 0;
                            got_escape = false;
                            sb.Append(Utils.GetString(new byte[] { b }));
                        }
                        else if (b == 0x03) //ETX
                        {
                            if (b1[b1.Count() - 2] == prev_checksum)
                                CrestronConsole.PrintLine("{0} checksum valid: {1}", comms.DeviceName, Utils.CreateHexPrintableString(sb.ToString()));
                            else
                                CrestronConsole.PrintLine("{0} checksum failed, Rx:{1}, Calculated:{2}", comms.DeviceName, b1[b1.Count() - 2], prev_checksum);
                            got_escape = false;
                        }
                        else if (b == 0x1B) // ESC
                            got_escape = true;
                            //byte b2 = got_escape ? (byte)(b & 0x0F) : b; // remove high byte
                       else
                        {
                            byte b2 = got_escape ? (byte)(b & 0x0F) : b; // remove high byte
                            sb.Append(Utils.GetString(new byte[] { b2 }));
                            prev_checksum = checksum;
                            checksum = (byte)(checksum ^ b2);
                            got_escape = false;
                        }
                    }
                    b1 = Utils.GetBytes(sb.ToString());
                    BSS_DI header = (BSS_DI)b1[1];
                    switch (header)
                    {
                        /* BSS preset String format
                           *   B[ 0] = 0x02 = STX
                           *   B[ 1] = 0x8C = ID (0x8B:DI_VENUE_PRESET_RECALL, 0x8C:DI_PARAM_PRESET_RECALL)
                           *   B[ 2] = 0x00 = preset index 1 B0
                           *   B[ 3] = 0x27 = preset index 1 B1
                           *   B[ 4] = 0x00 = preset index 2 B2
                           *   B[ 5] = 0x00 = preset index 2 B3
                           *   B[ 6] = 0x8B = checksum
                           *   B[ 7] = 0x03 = ETX
                           */
                        case BSS_DI.DI_VENUE_PRESET_RECALL:
                            CrestronConsole.PrintLine("Venue preset {0} recalled", b1[5]);
                            break;
                        case BSS_DI.DI_PARAM_PRESET_RECALL:
                            CrestronConsole.PrintLine("Parameter preset {0} recalled", b1[5]);
                            break;
                        default:
                            /* BSS String format
                             *   B[ 0] = 0x02 = STX
                             *   B[ 1] = 0x88 = ID (0x88:DI_SETSV, 0x89:DI_SUBSCRIBESV, 0x8A:DI_UNSUBSCRIBESV, 0x8D:DI_SETSVPERCENT,
                             *                      0x8E:DI_SUBSCRIBESVPERCENT, 0x8F:DI_UNSUBSCRIBESVPERCENT, 0x90:DI_BUMPSVPERCENT)
                             *   B[ 2] = 0x85 = HiQ Device Node B0
                             *   B[ 3] = 0x8C = HiQ Device Node B1
                             *   B[ 4] = 0x03 = Virtual device ID (0x00:Basic settings, 0x01:Links, 0x02:Logic objects, 0x03:Audio processing objects)
                             *   B[ 5] = 0x00 = HiQ Object ID Node B0
                             *   B[ 6] = 0x01 = HiQ Object ID Node B1
                             *   B[ 7] = 0x2E = HiQ Object ID Node B2
                             *   B[ 8] = 0x00 = object index 1 B0
                             *   B[ 9] = 0x27 = object index 1 B1
                             *   B[10] = 0x00 = object index 2 B0
                             *   B[11] = 0x00 = object index 2 B1
                             *   B[12] = 0x00 = object index 2 B2
                             *   B[13] = 0x00 = object index 2 B3
                             *   B[14] = 0x8B = checksum
                             *   B[15] = 0x03 = ETX
                             */
                            ushort deviceID = 0;
                            int i;
                            for (i = 2; i < 4; i++)
                                deviceID = (ushort)(deviceID * 0x100 + b1[i]);
                            uint objectID = 0;
                            for (i = 5; i < 8; i++)
                                objectID = objectID * 0x100 + b1[i];
                            long val1 = 0;
                            for (i = 8; i < 10; i++)
                                val1 = val1 * 0x100 + b1[i];
                            long val2 = 0;
                            for (i = 10; i < 14; i++)
                                val2 = val2 * 0x100 + b1[i];
                            CrestronConsole.PrintLine("HiQ device address 0x{0:X2}{1:X2}, object ID 0x{2:X2}{3:X2}{4:X2}", b1[2], b1[3], b1[5], b1[6], b1[7]);
                            if (BSS_VIRTUAL_DEVICE_ID.ContainsKey(b1[4]))
                                CrestronConsole.PrintLine("BSS_VIRTUAL_DEVICE_ID: {0}", BSS_VIRTUAL_DEVICE_ID[b1[4]]);
                            CrestronConsole.PrintLine("val 1: 0x{0:X} ({0}), val 2: 0x{1:X} ({1})", val1, val2);

                            switch (b1[4])
                            {
                                case 0x00: // Basic settings
                                    break;
                                case 0x01: // Links
                                    break;
                                case 0x02: // Logic objects
                                    break;
                                case 0x03: // Audio processing objects
                                    break;
                            }
                            switch (header)
                            {
                                case BSS_DI.DI_SETSV: // Set a control parameter
                                    BssNode bn = nodes.Find(x => x.ObjectID == (uint)objectID);
                                    if (bn == null)
                                    {
                                        CrestronConsole.PrintLine("adding {0} object 0x{1:X3}", "unknown", objectID);
                                    }
                                    else
                                    {
                                        CrestronConsole.PrintLine("{0} object 0x{1:X3}", BSS_NODE_FUNC_DICT[(byte)bn.Func], objectID);
                                    }
                                    break;
                                case BSS_DI.DI_SUBSCRIBESV: // Return the current value of and subscribe to a control parameter.
                                    break;
                                case BSS_DI.DI_UNSUBSCRIBESV: // Unsubscribe from a control parameter.
                                    break;
                                case BSS_DI.DI_SETSVPERCENT: // Set a control parameter by percentage
                                    break;
                                case BSS_DI.DI_SUBSCRIBESVPERCENT: // Return the current value of and subscribe to a control parameter as a percentage of its total range.
                                    break;
                                case BSS_DI.DI_UNSUBSCRIBESVPERCENT: // subscribe from a state variable previously subscribed as a percentage of its total range.
                                    break;
                                case BSS_DI.DI_BUMPSVPERCENT: // Increment a control parameter by the given signed percentage of its total range.
                                    break;
                                default:
                                    CrestronConsole.PrintLine("Unknown command type: {0}", Utils.CreateHexPrintableString(new byte[] { b1[1] }));
                                    break;
                            }
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} Exception: {1}", comms.DeviceName, e.ToString());
            }

        }
    }
    #region enums
    public enum BSS_DI // Direct inject protocol headers
    {
        DI_SETSV = 0x88,
        DI_SUBSCRIBESV = 0x89,
        DI_UNSUBSCRIBESV = 0x8A,
        DI_VENUE_PRESET_RECALL = 0x8B,
        DI_PARAM_PRESET_RECALL = 0x8C,
        DI_SETSVPERCENT = 0x8D,
        DI_SUBSCRIBESVPERCENT = 0x8E,
        DI_UNSUBSCRIBESVPERCENT = 0x8F,
        DI_BUMPSVPERCENT = 0x90
    }
    public enum BSS_NODE_FUNC
    {
        GAIN_N_INPUT = 0,
        GAIN_MIXER = 1,
        SOURCE_SELECT = 2,
        MATRIX_ROUTER = 3,
        MATRIX_MIXER = 4,
    }
    public class BssNode // keep track of node types
    {
        public BSS_NODE_FUNC Func { get; private set; } // if full HiQ is 0x858c0300016c
        public ushort DeviceID { get; private set; }    // would be 0x858c
        public uint ObjectID { get; set; }        // would be 0x00016c

        public BssNode(ushort deviceID, uint objectID, BSS_NODE_FUNC func)
        {
            this.DeviceID = deviceID;
            this.ObjectID = ObjectID;
            this.Func = func;
        }
    }
    /*
    public class BssNode: BssNode
    {
        public ushort subscribed { get; private set; }
        public ushort subscribe;
        public List<uint> inputs;
        public List<uint> outputs;
        public uint levelVal;
        public ushort levelPercent;
    }
     * */
    #endregion
}
