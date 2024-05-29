using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes

namespace Navitas
{
    public class Dynalite
    {
        public delegate void StringEventHandler(object sender, StringEventArgs e);
        public event StringEventHandler Send;
        //vars
        StringBuilder RxData = new StringBuilder();

        protected String name = "Dynalite";
        public IPClient IpComms;
        protected SerialPort SerialComms;
        //private ushort ipPort = 23;
        protected CTimer pollTimer;
        public int pollTimerRepeat = 30000;

        private ushort fadeTimeHz = 50; // 50Hz = 1 sec
        private byte mask = 0xff;
        private byte lastArea;

        public Dynalite(string name)
        {
            this.name = name;
        }
        public void SetComms(IPClient comms)
        {
            this.IpComms = comms;
            comms.ParseRxData += new IPClient.StringEventHandler(ParseRx);
            comms.debugAsHex = true;
            comms.SetDebug(5);
            pollTimer = new CTimer(pollTimerExpired, this, 1, pollTimerRepeat);
        }
        public void SetComms(SerialPort comms)
        {
            this.SerialComms = comms;
            comms.ParseRxData += new SerialPort.StringEventHandler(ParseRx);
            comms.debugAsHex = true;
            if (comms.device.Registered)
                comms.device.SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate9600,
                                         ComPort.eComDataBits.ComspecDataBits8,
                                         ComPort.eComParityType.ComspecParityNone,
                                         ComPort.eComStopBits.ComspecStopBits1,
                                         ComPort.eComProtocolType.ComspecProtocolRS232,
                                         ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                         ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                         false);
            comms.SetDebug(5);
            pollTimer = new CTimer(pollTimerExpired, this, 1, pollTimerRepeat);
        }

        protected virtual void Initialize()
        {
        }
        public void Dispose()
        {
            if (IpComms != null)
                IpComms.Dispose();
            if (SerialComms != null)
                SerialComms.Dispose();
            if (pollTimer != null)
            {
                pollTimer.Stop();
                pollTimer.Dispose();
            }
        }
        public void SetName(string name)
        {
            this.name = name;
        }
        public string GetName()
        {
            return name;
        }

        public void SendString(string str)
        {
            if (Send != null)
                Send(this, new StringEventArgs(str));
            if (IpComms != null)
            {
                //CrestronConsole.PrintLine("{0} SendString {1}", name, Utils.CreatePrintableString(str, true));
                IpComms.Send(str);
            }
            else
                CrestronConsole.PrintLine("{0} no IpComms", name);
            if (SerialComms != null)
                SerialComms.Send(str);
        }

        public void MakeString(byte[] b)
        {
            string str = String.Format("{0}{1}",
                       Utils.GetString(b),
                       Utils.GetString(new byte[] { (byte)(0x100-Utils.AddBytes(b)) }));
            //CrestronConsole.PrintLine("{0} Tx: ", name, Utils.CreatePrintableString(str, true));

            SendString(str);
        }
        public void PollArea(byte area) // TODO
        {
            //sTemp= STX + "&Update=" + chr(Area) + ETX; // tell master to poll all channels in area
        }
        public void RecallPreset(byte area, byte preset)
        {
            CrestronConsole.PrintLine("{0} RecallPreset[{1},{2}]", name, area, preset);
            if(area != 0)
                lastArea = area;
            MakeString(new byte[]
            {
                0x1C            , // Byte 0 Message type (Logical message - 0x1C, Physical message 0x5C)
	            area            , // Byte 1 Area
	            (byte)(preset-1), // Byte 2 Preset (0 origin)
	            0x65            , // Byte 3 OpCode
	            (byte)(fadeTimeHz % 0x100), // Byte 4 Fade - low (default of 0x64, equal to 2 seconds)
                (byte)(fadeTimeHz / 0x100), // Byte 5 Fade - high (default of 0x00, when Byte 2 is 0x64 produces a 2)
                mask              // Byte 6 Join Mask (default is 0xff)
            });
            PollArea(area);
        }

        public void SetLevel(byte lvl)
        {
        }
        public void SetChannel(byte chan)
        {
        }
        public void SetPreset(byte chan)
        {
        }
        public void DoQuery(byte chan)
        {
        }
        protected void pollTimerExpired(object obj) // TODO - poll correctly, this is just a random preset poll to keep the connection alive
        {
            try
            {
                if (lastArea != 0)
                {
                    CrestronConsole.PrintLine("{0} PollPreset for area {1}", name, lastArea);
                    MakeString(new byte[]
                    {
                        0x1C    , // Byte 0 Message type (Logical message - 0x1C, Physical message 0x5C)
	                    lastArea, // Byte 1 Area
	                    0       , // Byte 2 Preset (0 origin)
	                    0x63    , // Byte 3 OpCode
	                    0       , // Byte 4 Fade - low (default of 0x64, equal to 2 seconds)
                        0       , // Byte 5 Fade - high (default of 0x00, when Byte 2 is 0x64 produces a 2)
                        mask      // Byte 6 Join Mask (default is 0xff)
                    });
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} pollTimerExpired {1}", name, e.Message);
            }

        }
        public void ParseLogicalMessage(byte[] b)
        {
            if (b.Length >= 8)
            {
                byte MessageType = b[0]; // Message type (Logical message - 0x1C, Physical message 0x5C)
                byte AREA = b[1]; // Area
                byte ChannelLevel = b[2]; // Channel level
                byte Channel = b[2]; // Channel Number (0 origin)
                byte Preset = b[3]; // Preset Number (0 origin)
                byte OpCode = b[3]; // OpCode
                byte Level = b[4]; // Channel Level (01 - 100%, FF = 0%)
                byte Fade = b[4]; // Fade
                byte Offset = b[4]; // Channel offset
                byte ChannelPreset = b[4]; // Preset Number (0 origin)
                byte Bank = b[5]; // Preset bank
                byte LevelCurrent = b[5]; // Current Channel Level (01 - 100%, FF = 0%)
                byte Join = b[6]; // iJoin, default is FFh
                byte Checksum = b[7]; // checksum

                switch (b[3]) // OpCode
                {
                    case 0x17: break;	// Panic on	
                    case 0x18: break;	// Panic off
                    case 0x60:	// Level reply
                        CrestronConsole.PrintLine("{0} - Channel Level (0x60), Area {1}, Channel {2}, Level {3}", name, AREA, Channel, 0xFF - Level);
                        break;
                    case 0x6B: // Channel preset
                        CrestronConsole.PrintLine("{0} - Channel Preset, Area {1}, Channel {2}, Preset {3}", name, AREA, Channel, ChannelPreset);
                        DoQuery(Channel);
                        break;
                    case 0x61: { break; } // Level request
                    case 0x68: { break; } // Start Ramp Down
                    case 0x69: { break; } // Start Ramp Up
                    case 0x71: // Linear set level channel (100ms to 25.5s)
                        CrestronConsole.PrintLine("{0} - Linear Channel Level, Area {1}, Channel {2}, Level {3}", name, AREA, Channel, 0xFF - Level);
                        SetLevel(Level);
                        SetChannel(Channel);
                        break;
                    case 0x72: // Linear set level channel (1s to 3min15s)
                    case 0x73: // Linear set level channel (1min to 22min)
                    case 0x76: // Stop Ramping Channel
                        DoQuery(Channel);
                        break;
                    case 0x80: // Fade channel 1 to a level
                        switch (Offset)
                        {
                            case 0xFF: Channel = 1; break;
                            case 0x00: Channel = 5; break;
                            case 0x01: Channel = 9; ; break;
                            case 0x02: Channel = 13; break;
                            default:
                                CrestronConsole.PrintLine("{0} - Channel Level (0x80), Offset {3}", name, Offset);
                                break;
                        }
                        CrestronConsole.PrintLine("{0} - Channel Level (0x80), Area {1}, Channel {2}, Level {3}, Offset {4}", name, AREA, Channel, 0xFF - Level, Offset);
                        SetLevel(ChannelLevel);
                        SetChannel(Channel);
                        break;
                    case 0x81: // Fade channel 2 to a level
                        switch (Offset)
                        {
                            case 0xFF: Channel = 2; break;
                            case 0x00: Channel = 6; break;
                            case 0x01: Channel = 10; break;
                            case 0x02: Channel = 14; break;
                            default:
                                CrestronConsole.PrintLine("{0} - Channel Level (0x81), Offset {1}", name, Offset);
                                break;
                        }
                        CrestronConsole.PrintLine("{0} - Channel Level (0x81), Area {1}, Channel {2}, Level {3}, Offset {4}", name, AREA, Channel, 0xFF - Level, Offset);
                        SetLevel(ChannelLevel);
                        SetChannel(Channel);
                        break;
                    case 0x82: // Fade channel 3 to a level
                        switch (Offset)
                        {
                            case 0xFF: Channel = 3; break;
                            case 0x00: Channel = 7; break;
                            case 0x01: Channel = 11; break;
                            case 0x02: Channel = 15; break;
                            default:
                                CrestronConsole.PrintLine("{0} - Channel Level (0x82), Offset {1}", name, Offset);
                                break;
                        }
                        CrestronConsole.PrintLine("{0} - Channel Level (0x82), Area {1}, Channel {2}, Level {3}, Offset {4}", name, AREA, Channel, 0xFF - Level, Offset);
                        SetLevel(ChannelLevel);
                        SetChannel(Channel);
                        break;
                    case 0x83: // Fade channel 4 to a level
                        switch (Offset)
                        {
                            case 0xFF: Channel = 4; break;
                            case 0x00: Channel = 8; break;
                            case 0x01: Channel = 12; break;
                            case 0x02: Channel = 16; break;
                            default:
                                CrestronConsole.PrintLine("{0} - Channel Level (0x83), Offset {1}", name, Offset);
                                break;
                        }
                        CrestronConsole.PrintLine("{0} - Channel Level (0x83), Area {1}, Channel {2}, Level {3}, Offset {4}", name, AREA, Channel, 0xFF - Level, Offset);
                        SetLevel(ChannelLevel);
                        SetChannel(Channel);
                        break;

                    // Area 
                    case 0x05: // Area nudge down
                    case 0x06: // Area nudge up
                    case 0x6A: // Stop Ramping All Channels
                        //SetPreset(null); // This will force channel modules in the current area to send a status query
                        break;

                    case 0x79: // Classic Area control
                        CrestronConsole.PrintLine("{0} - Classic Area control, Area {1}, Channel {2}, Level {3}", name, AREA, Channel, 0xFF - Level);
                        SetLevel(Level);
                        SetChannel(Channel);
                        break;

                    // Presets
                    case 0x62: // Preset reply
                    case 0x65: // Linear Preset Message
                        Preset = Channel;
                        CrestronConsole.PrintLine("{0} - Linear Preset Message, Area {1}, Preset {2}, Bank {3}", name, AREA, Preset, Bank);
                        SetPreset(Preset);
                        break;
                    case 0x00: // Classic Preset 1
                    case 0x01: // Classic Preset 2
                    case 0x02: // Classic Preset 3
                    case 0x03: // Classic Preset 4
                        Preset++;
                        Preset = (byte)((Bank * 8) + Preset);
                        CrestronConsole.PrintLine("{0} - Classic Preset Message, Area {1}, Preset {2}, Bank {3}", name, AREA, Preset, Bank);
                        SetPreset(Preset);
                        break;
                    case 0x0a: // Classic Preset 5
                    case 0x0b: // Classic Preset 6
                    case 0x0c: // Classic Preset 7
                    case 0x0d: // Classic Preset 8
                        Preset -= 5;
                        Preset = (byte)((Bank * 8) + Preset);
                        CrestronConsole.PrintLine("{0} - Classic Preset Message, Area {1}, Preset {2}, Bank {3}", name, AREA, Preset, Bank);
                        SetPreset(Preset);
                        break;
                }
            }
        }
        public void ParsePhysicalMessage(byte[] b)
        {
            CrestronConsole.PrintLine("{0} Physical Message", name);
        }
        public void ParseBlockMessage(byte[] b)
        {
            if (b.Length >= 8)
            {
                CrestronConsole.PrintLine("{0} Block Message", name);
                byte MessageType = b[0]; // Message type (Logical message - 0x1C, Physical message 0x5C)
                byte DeviceType = b[1]; // Device type
                byte Address = b[2]; // Box Address
                byte OpCode = b[3]; // OpCode
                byte Checksum = b[7]; // checksum
                ushort boxData;
                switch (OpCode)
                {
                    case 0x00: // Flash box enable (declaration sent by holding button for 5 sec) 
                        CrestronConsole.PrintLine("{0} Flash box enable", name);
                        // \x5C\xA7\x01\x00\x03\x54\x00\xA5	// Flash UPan v3.54 Box 1 Enable (declaration)
                        // B4 Major Version
                        // B5 Minor Version
                        // B6 \x00 
                        break;
                    case 0x01: // Setup Box: Master
                        CrestronConsole.PrintLine("{0} Setup Box: Master", name);
                        // \x5C\xA7\x01\x01\x01\x01\x00\xF9	// Setup UPan Box 1: Master Listen to me
                        // \x5C\xA7\x01\x01\x01\x00\x00\xF9	// Setup UPan Box 1: Master Go back to work
                        // B4 \x01
                        // B5 \x01
                        // B6 Command (\x01 - Listen to me, \x00 - Go back to work) 
                        break;
                    case 0x03: // Read status (sends lots of these commands to Address 0000hex when searching network) 
                        CrestronConsole.PrintLine("{0} Read status", name);
                        // \x5C\xA7\x01\x03\x03\x1A\x00\xDC	// Read UPan Box 1 Addr 031Ahex 
                        // B4 Address high byte
                        // B5 Address low  byte
                        // B6 \x00
                        break;
                    case 0x05: // Echo status 
                        CrestronConsole.PrintLine("{0} Echo status", name);
                        // \x5C\xA7\x01\x05\x03\x1A\xFF\xDB	// Echo UPan Box 1 Addr 031Ahex Data FFhex 
                        // B4 Address high byte
                        // B5 Address low  byte
                        // B6 Data
                        break;
                    case 0x09: // Light level request 
                        CrestronConsole.PrintLine("{0} Light level request", name);
                        // \x5C\xA5\x05\x09\x00\x00\x00\xF1	// Light Level Request PE Cell 5 is 1F5hex 
                        // B4 \x00
                        // B5 \x00
                        // B6 \x00 
                        break;
                    case 0x0a: // Light level status 
                        //byte boxID	   = Address;
                        boxData = (ushort)(b[5] * 0x100 + b[6]);
                        CrestronConsole.PrintLine("{0} Light level status, box ID {1}, data {2}", name, Address, boxData);
                        // \x5C\xA5\x05\x0A\x00\x01\xF5\xFA	// Light Level of PE Cell 5 is 1F5hex 
                        // B4 \x00
                        // B5 Level high byte
                        // B6 Level low  byte
                        break;
                    case 0x30: // BLOCK EE Read Enable Address 0000hex
                        CrestronConsole.PrintLine("{0} BLOCK EE Read Enable Address", name);
                        // \x5C\xA7\x01\x30\x00\x00\x00\xCC	// BLOCK EE Read Enable UPan Box 1 Addr 0000hex
                        // (\x18A * 2 = \x314) 
                        // B4 Address/2 high byte
                        // B5 Address/2 low  byte
                        // B6 \x00
                        break;
                    case 0x31: // BLOCK EE Read Ack Address
                        CrestronConsole.PrintLine("{0} BLOCK EE Read Ack Address", name);
                        // \x5C\xA7\x01\x31\x01\x8A\x00\x40	// BLOCK EE Read Ack UPan Box 1 Addr 0314hex Err 00hex
                        // (\x18A * 2 = \x314) 
                        // B4 Address/2 high byte
                        // B5 Address/2 low  byte
                        // B6 \x00
                        break;
                    case 0x40: // Request status 
                        CrestronConsole.PrintLine("{0} Request status", name);
                        // \x5C\xA7\x01\x40\x00\x00\x01\xb1	// Request UPan status Box 1 
                        // B4 \x00
                        // B5 \x00
                        // B6 \x01 
                        break;
                    case 0x41: // Read device 
                        CrestronConsole.PrintLine("{0} Read device", name);
                        // \x5C\xA7\x01\x41\x03\x78\x01\x3f	// Reply UPan status Box 1 03-78-01 
                        // B4 Status Byte1
                        // B5 Status Byte2
                        // B6 Status Byte3 
                        break;
                    case 0x43: // Button state
                        boxData = b[6];
                        CrestronConsole.PrintLine("{0} Button state, box ID {1}, data {2}", name, Address, boxData);
                        // \x5C\xA7\x04\x43\x01\x00\xFF\xB6	// UPan Box 4 button 1 pressed
                        // \x5C\xA7\x04\x43\x01\x00\x00\xB5	// UPan Box 4 button 1 released 
                        // B4 \x01
                        // B5 \x00
                        // B6 \xFF (button mask, \x00 for release)
                        break;
                    case 0x49: // IR state
                        boxData = b[6];
                        CrestronConsole.PrintLine("{0} IR state, box ID {1}, data {2}", name, Address, boxData);
                        // B4 \x01
                        // B5 \x00
                        // B6 \xFF (button mask, \x00 for release) 
                        break;
                    case 0x80: // Request Version 
                        CrestronConsole.PrintLine("{0} Request Version", name);
                        // \x5C\xA7\x01\x80\x00\x00\x00\x7C	// Request Version UPan (Master) Box 1
                        // B4 \x00
                        // B5 \x00
                        // B6 \x00 
                        break;
                }
            }
        }
        public void ParseDimmerMessage(byte[] b)
        {
            CrestronConsole.PrintLine("{0} Dimmer Message", name);
        }

        public void ParseRx(object sender, StringEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} ParseRx: {1}", name, Utils.CreatePrintableString(args.str, true));
            try
            {
                if (args.str.Length > 8) // Todo, queue and dequeue
                {
                    byte[] b = Utils.GetBytes(args.str);
                    switch(b[0])
                    {
                        case 0x1C: ParseLogicalMessage (b); break;	// Logical	
                        case 0x5C: ParsePhysicalMessage(b); break;	// Physical	
                        case 0x6C: ParseBlockMessage   (b); break;	// Block data	
                        case 0xAC: ParseDimmerMessage  (b); break;	// Dimmer	
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("{0} ParseRx Exception: {1}", name, e.ToString());
            }

        }
    }
}