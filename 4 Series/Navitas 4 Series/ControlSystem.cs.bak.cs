using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Crestron.SimplSharp;                          // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.CrestronSockets;          // TCPClient
using Crestron.SimplSharpPro;                       // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        // For Threading
using Crestron.SimplSharpPro.DeviceSupport;         // For Generic Device Support
using Crestron.SimplSharpPro.EthernetCommunication; // EISC
using Crestron.SimplSharpPro.DM;                    // DM
using Crestron.SimplSharpPro.DM.Cards;
using Crestron.SimplSharpPro.DM.Endpoints;
using Crestron.SimplSharpPro.DM.Endpoints.Transmitters;
using Crestron.SimplSharpPro.DM.Endpoints.Receivers;
// For DynamicDriver. 
// Need to add references "Display", "EpsonProj", "ProTransports" and "ThirdPartyCommon"
//using Crestron.Display.EpsonProj;
//using ProTransports;
using Crestron.ThirdPartyCommon.Interfaces;

namespace Navitas
{
    //public delegate void OnlineEventHandler(object sender, BoolEventArgs e);
    //public delegate void StringEventHandler(object sender, StringEventArgs e);

    public class ControlSystem : CrestronControlSystem
    {
        private bool debug = false;
        #region constants
        // IPIDS
        private const ushort IPID_EISC = 0x0A;
        private byte[] IPID_UIS = { 0x03,0x04 };
        private const ushort IPID_VIDMATRIX = 0x30; // 0x30 = DM Matrix
        private const byte IPID_DMTX_BASE = 0x31; // 0x31,0x32 = DmTx
        private const byte IPID_DMRX_BASE = 0x41; // 0x41,0x42 = DmRx
        private string configFilePath = String.Empty;
        private string configFileName = String.Empty;
        private Config config;

        public ThreeSeriesTcpIpEthernetIntersystemCommunications eisc;
        public Switch vidMatrix;
        public List<DmHDBasedTEndPoint> DmTransmitters;
        public List<EndpointReceiverBase> DmReceivers;
        public List<CardDevice> vidMatrixInputCards;
        public List<DmOutputModuleBase> vidMatrixOutputCards;
        
        public Dictionary<ushort, Relay> relays;

        private List<UiWithLocation> uis;

        private CTimer CommonTimer;
        private CTimer powerTimer;
        private long powerTimerInterval = 50;
        private int powerBar;

        private ushort[] EISC_ANA_VOL = { 1, 2 };
        private ushort[] EISC_ANA_MIC = { 11, 12 };
        private ushort[] EISC_ANA_SRC = { 21, 22 };
        private const ushort EISC_ANA_MIC_INPUT = 31;

        private ushort[] EISC_DIG_VOL_MUTE_ON  = {  1,  3 };
        private ushort[] EISC_DIG_VOL_MUTE_OFF = {  2,  4 };
        private ushort[] EISC_DIG_MIC_MUTE_ON  = { 11, 13 };
        private ushort[] EISC_DIG_MIC_MUTE_OFF = { 12, 14 };
        private ushort[] EISC_DIG_LIGHTS_UP       = { 21, 26 };
        private ushort[] EISC_DIG_LIGHTS_DOWN     = { 22, 27 };
        private ushort[] EISC_DIG_LIGHTS_PRESET_1 = { 23, 28 };
        private ushort[] EISC_DIG_LIGHTS_PRESET_2 = { 24, 29 };
        private ushort[] EISC_DIG_LIGHTS_PRESET_3 = { 25, 30 };
        private ushort[] EISC_DIG_MIC_INPUT_ON  = { 91, 93 };
        private ushort[] EISC_DIG_MIC_INPUT_OFF = { 92, 94 };
        private ushort[] EISC_DIG_CAM_SEL       = { 157, 158 };

        private ushort[] EISC_SER_PROJ = { 1, 2 };

        const ushort SMART_ID_KEYPAD = 1;      // login keypad
        const ushort SMART_ID_SOURCE_LIST_0 = 2; // source select, not joined
        const ushort SMART_ID_SOURCE_LIST_JOINED_0 = 3; // top list when joined
        const ushort SMART_ID_SOURCE_LIST_JOINED_1 = 4; // bottom list when joined
        const ushort SMART_ID_DEST_LIST = 5;
        const ushort SMART_ID_MICS = 6;
        const ushort SMART_ID_CAMERA_DPAD = 7;
        const ushort SMART_ID_CAMERA_PRESETS = 8;
        const ushort SMART_ID_LIGHT_PRESETS = 9;
        //const ushort SMART_ID_VERT_DEST_LIST = 13;
        //const ushort SMART_ID_VERT_SOURCE_LIST_0 = 14; // source select, not joined
        //const ushort SMART_ID_VERT_SOURCE_LIST_JOINED_0 = 15; // top list when joined
        //const ushort SMART_ID_VERT_SOURCE_LIST_JOINED_1 = 16; // bottom list when joined

        const byte DIG_INC_MICS = 3;   //digIncJoin for SMART_ID_MICS

        // ui buttons
        private ushort[] DIG_HARD_BTNS = { 1, 2, 3, 4, 5 };
        const ushort DIG_POWER = 1;
        const ushort DIG_HOME = 2;
        const ushort DIG_LIGHTS = 3;
        const ushort DIG_VOL_UP = 4;
        const ushort DIG_VOL_DN = 5;
        const ushort DIG_VOL_MUTE = 6;

        const ushort DIG_PAGE_DEV = 10;
        const ushort DIG_PAGE_SPLASH = 11;
        const ushort DIG_PAGE_MAIN = 12;

        const ushort DIG_HAS_LIGHTS = 13;
        const ushort DIG_PICMUTE = 16;

        const ushort DIG_SUB_DOCCAM = 17;
        const ushort DIG_SUB_SINGLE_DISPLAY = 18;
        const ushort DIG_SUB_PROJ = 19;
        const ushort DIG_SUB_ONLINE = 20;
        const ushort DIG_SUB_LOGIN = 21;
        const ushort DIG_SUB_CONFIRM = 22;
        const ushort DIG_SUB_COUNTDOWN = 23;
        const ushort DIG_SUB_JOIN_0 = 24;
        const ushort DIG_SUB_JOIN_1 = 25;
        const ushort DIG_SUB_LIGHTS = 26;
        const ushort DIG_SUB_MIC = 27;
        const ushort DIG_SUB_RECORD = 28;
        const ushort DIG_SUB_CAM = 29;

        const ushort DIG_START = 30;
        const ushort DIG_CONFIRM = 31;
        const ushort DIG_CANCEL = 32;
        const ushort DIG_JOIN = 33;
        const ushort DIG_HAS_JOIN = 34;
        const ushort DIG_RECORD = 35;
        const ushort DIG_HAS_RECORD = 36;
        const ushort DIG_MIC = 37;
        const ushort DIG_HAS_MIC = 38;
        const ushort DIG_CAM = 39;
        const ushort DIG_HAS_CAM = 40;

        const ushort DIG_MIC_VOL_UP = 74;
        const ushort DIG_MIC_VOL_DN = 75;
        const ushort DIG_MIC_VOL_MUTE = 76;

        const ushort DIG_LIGHTS_UP = 81;
        const ushort DIG_LIGHTS_DOWN = 82;
        const ushort DIG_LIGHTS_PRESET_1 = 83;
        const ushort DIG_LIGHTS_PRESET_2 = 84;
        const ushort DIG_LIGHTS_PRESET_3 = 85;

        const ushort DIG_DOCCAM_POWER = 91;
        const ushort DIG_DOCCAM_AUTOFOCUS = 92;
        const ushort DIG_DOCCAM_LAMP = 93;
        const ushort DIG_DOCCAM_BACKLIGHT = 94;
        const ushort DIG_DOCCAM_ZOOM_IN = 95;
        const ushort DIG_DOCCAM_ZOOM_OUT = 96;
        const ushort DIG_DOCCAM_FOCUS_IN = 97;
        const ushort DIG_DOCCAM_FOCUS_OUT = 98;
        const ushort DIG_DOCCAM_BRIGHT_UP = 99;
        const ushort DIG_DOCCAM_BRIGHT_DN = 100;

        const ushort DIG_AUD_DSP_ONLINE = 1001;
        const ushort DIG_AUD_DSP_VOL_UP = 1002;
        const ushort DIG_AUD_DSP_VOL_DOWN = 1003;
        const ushort DIG_AUD_DSP_VOL_MUTE = 1004;
        // analogs
        const ushort ANA_TIMER_PERCENT = 1;
        const ushort ANA_TIMER_VAL = 2;
        const ushort ANA_VOL_PERCENT = 3;
        const ushort ANA_VOL_ACTUAL = 4;
        const ushort ANA_VOL_MIC_PERCENT = 5;
        const ushort ANA_VOL_MIC_ACTUAL = 6;
        // serials
        const ushort SER_PASSWORD_TEXT = 1;
        const ushort SER_LOCATION_NAME = 2;
        const ushort SER_CUR_SEL_TITLE = 3;
        const ushort SER_CUR_SEL_BODY = 4;
        const ushort SER_CUR_SEL_TEXT = 5;
        const ushort SER_MESSAGES = 6;
        const ushort SER_TOP_TXT = 7;
        private ushort[] SER_LOCATION_NAMES = { 8, 9 };
        #endregion
        #region dictionaries
        Dictionary<ushort, string> IconsLg = new Dictionary<ushort, string>()
        {
            { 0, "AM-FM" },
            { 1, "CD" },
            { 2, "Climate" },
            { 3, "Display Alt" }, // black LCD monitor
            { 4, "Display" }, // blue LCD monitor
            { 5, "DVR" }, // DVR text on red btn
            { 6, "Energy Management" },
            { 7, "Favorites" },
            { 8, "Film Reel" },
            { 9, "Home" },
            { 10, "Internet Radio" },
            { 11, "iPod" },
            { 12, "iServer" },
            { 13, "Lights" },
            { 14, "Music Note" },
            { 15, "News" },
            { 16, "Pandora" },
            { 17, "Power" },
            { 18, "Satellite Alt" },
            { 19, "Satellite" },
            { 20, "Sec-Cam" }, // black on pivot mount
            { 21, "Security" },
            { 22, "Shades" },
            { 23, "User Group" },
            { 24, "Video Conferencing" },
            { 25, "Video Switcher" },
            { 26, "Wand" },
            { 27, "Weather" },
            { 29, "Speaker" },
            { 30, "Mic" },
            { 31, "Projector" },
            { 32, "Screen" },
            { 33, "Gear" },
            { 34, "Sec-Cam Alt" }, // white no PTZ
            { 35, "Document Camera" }, // lens over paper
            { 36, "Backgrounds" },
            { 37, "Gamepad" },
            { 38, "iMac" },
            { 39, "Laptop Alt" },
            { 40, "Laptop" },
            { 41, "MacBook Pro" },
            { 43, "Phone Alt" },
            { 44, "Phone" },
            { 42, "Music Note Alt" },
            { 45, "Pool" },
            { 46, "Airplay" },
            { 47, "Alarm Clock" },
            { 48, "AppleTV" },
            { 49, "AUX Plate" },
            { 50, "Document Camera Alt" }, // full doccam
            { 51, "Door Station" },
            { 52, "DVR Alt" }, // DVR and remote
            { 53, "Front Door Alt" },
            { 54, "Front Door" },
            { 55, "Jukebox" },
            { 56, "Piano" },
            { 57, "Playstation 3" },
            { 58, "Playstation Logo" },
            { 59, "Room Door" },
            { 60, "SmarTV" },
            { 61, "Sprinkler" },
            { 62, "Tablet" },
            { 63, "TV" }, // TV with remote
            { 64, "VCR" },
            { 65, "Video Conferencing Alt" },
            { 67, "Wii-U Logo" },
            { 69, "Wii" },
            { 70, "Xbox 360" },
            { 71, "Xbox Logo" },
            { 72, "Amenities" },
            { 73, "DirecTV" },
            { 74, "Dish Network" },
            { 75, "Drapes" },
            { 76, "Garage" },
            { 77, "Macros" },
            { 78, "Scheduler" },
            { 79, "Sirius-XM Satellite Radio" },
            { 80, "TiVo" },
            { 81, "Blu-ray" },
            { 82, "DVD" },
            { 83, "Record Player" },
            { 84, "Vudu" },
            { 85, "Home Alt" },
            { 86, "Sirius Satellite Radio" },
            { 87, "Rhapsody" },
            { 88, "Spotify" },
            { 89, "Tunein" },
            { 90, "XM Satellite Radio" },
            { 91, "LastFM" },
            { 92, "You Tube" },
            { 93, "Kaleidescape" },
            { 94, "Hulu" },
            { 95, "Netflix" },
            { 96, "Clapper" },
            { 98, "Web" },
            { 99, "PC" },
            { 100, "Amazon" },
            { 101, "Chrome" },
            { 102, "Blank" },
            { 103, "Fireplace" }
        };

        Dictionary<ushort, string> SocketStatusTxt = new Dictionary<ushort, string>()
        {
            { 0, "Not Connected" },
            { 1, "Waiting for Connection" },
            { 2, "Connected" },
            { 3, "Connection Failed" },
            { 4, "Connection Broken Remotely" },
            { 5, "Connection Broken Locally" },
            { 6, "Performing DNS Lookup" },
            { 7, "DNS Lookup Failed" },
            { 8, "DNS Name Resolved" },
            { 9, "Link lost" },
            { 10, "Invalid (Client) Socket Index/Socket does not exist" },
        };

        ushort[] eIconsLgToKey_Inputs =
        {
            102, // Blank
            50, // VidDev.DOCCAM = 1, IconsLg[50] = "Document Camera Alt"
            99, // PC
            40, // LAPTOP
            20, // CAM_1
            20  // CAM_2
        };

        ushort[] eIconsLgToKey_Outputs =
        {
            102, // Blank
            31, // VidDev.PROJ_1 = 1, IconsLg[50] = "Document Camera Alt"
            31, // PROJ_2
            31, // PROJ_3
            5,  // REC_1
            5,  // REC_2
            14  // AUDIO
        };
        #endregion
        #region variables
        private ushort confirmState; // what are you confirming in the confirm sub
        public byte[] remapVidMatrixOutputs = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        private byte[] progAudioFaderIndex = { 4, 5 };
        private byte[] lapelMicFaderIndex = { 2, 6 };
        private byte[] handMicFaderIndex = { 3, 7 };
        private byte[] gooseMicFaderIndex = { 1, 8 };
        private StringBuilder keypadString = new StringBuilder();
        public String PasswordBackDoor = "0428989608";
        private bool clearSrcBtnsOnSwitch = true;
        private bool clearDestBtnsOnSwitch = true;
        private List<LevelRamp> mics = new List<LevelRamp>();
        private byte[] FontSizeLine = { 14, 12 };

        //private IBasicVideoDisplay display;
        //private ushort ComPortProj = 1;

        private List<Display> displays = new List<Display>();
        private List<LumensDocCam> docCams = new List<LumensDocCam>();
        private List<IPClient> ipClients = new List<IPClient>();
        private UDPServer ipLights;
        private AudioDspComm audDspComms;

        #endregion
        #region constructor
        public ControlSystem()
            : base()
        {
            CrestronConsole.PrintLine("\nProgram {0} starting", InitialParametersClass.ApplicationNumber);
            //Thread.Sleep(10000); // to give time to catch with debugger
            Thread.MaxNumberOfUserThreads = 100;
            CrestronConsole.PrintLine("Program {0} waking from sleep", InitialParametersClass.ApplicationNumber);
            if(debug)
                SystemDetails();
            configFilePath = string.Format("NVRAM\\{0}", InitialParametersClass.ProgramIDTag);
            configFileName = string.Format("* System config.json", InitialParametersClass.ProgramIDTag);
            config = RecallConfig(configFilePath, configFileName);
            if(config == null)
                CrestronConsole.PrintLine("RecallConfig done, null returned");
            else
                CrestronConsole.PrintLine("RecallConfig done, name: {0}", config.name);
            if (config.locations == null)
                CrestronConsole.PrintLine("locations == null");
            ConfigSwitcher();
            //Subscribe to the controller events (System, Program, and Etherent)
            CrestronConsole.PrintLine("Subscribing to controller events");
            CrestronEnvironment.SystemEventHandler += new SystemEventHandler(cs_ControllerSystemEventHandler);
            CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(cs_ControllerProgramEventHandler);

            CrestronConsole.PrintLine("adding {0} mics", config.mics.Count);
            foreach (Level mic in config.mics)
                mics.Add(new LevelRamp(this, mic));

            CrestronConsole.PrintLine("Creating {0} relay ports", RelayPorts.Count);
            for(int i = 1; i >= RelayPorts.Count; i++)
            {
                Relay rel = (Relay)RelayPorts[i];
                //RelayPorts[i].StateChange += new RelayEventHandler(RelayChangeHandler);
                rel.Register();
            }
            if(VersiPorts.Count > 0)
            {
                CrestronConsole.PrintLine("Creating {0} IO ports", VersiPorts.Count);
                for(int i = 1; i >= VersiPorts.Count; i++)
                {
                    VersiPorts[i].VersiportChange += new VersiportEventHandler(VersiPortChangeHandler);
                    VersiPorts[i].Register();
                }
            }
            else if(DigitalInputPorts.Count > 0)
            {
                CrestronConsole.PrintLine("Creating {0} IO ports", VersiPorts.Count);
                for(int i = 1; i >= VersiPorts.Count; i++)
                {
                    DigitalInputPorts[i].StateChange += new DigitalInputEventHandler(DigitalInputChangeHandler);
                    DigitalInputPorts[i].Register();
                }
            }
            CrestronConsole.PrintLine("Creating {0} Input ports", DigitalInputPorts.Count);

            CrestronConsole.PrintLine("Registering EISC");
            eisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(IPID_EISC, "127.0.0.2", this);
            eisc.SigChange += new SigEventHandler(EiscSigChangeHandler);
            if (eisc.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                ErrorLog.Error("eisc failed registration. Cause: {0}", eisc.RegistrationFailureReason);
                CrestronConsole.PrintLine("eisc failed registration. Cause: {0}", eisc.RegistrationFailureReason);
            }
            eisc.OnlineStatusChange += new OnlineStatusChangeEventHandler(CommonOnlineHandler);

            CrestronConsole.PrintLine("Registering UIs");
            uis = new List<UiWithLocation>(); // location to be set on config load
            for (byte b = 1; b <= config.locations.Count; b++)
            {
                UiWithLocation ui = new UiWithLocation(b, IPID_UIS[b-1], config.locations.Find(x => x.Id == b), this); // id, ipid, cs
                uis.Add(ui);
                while (ui.AvDestSelected.Count < config.vidOutputs.Count)
                    ui.AvDestSelected.Add(false);                
                
                ui.Device.SigChange += new SigEventHandler(UiSigChangeHandler);
                if (ui.Device.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                { 
                    ErrorLog.Error("ui_{0} failed registration. Cause: {1}", ui.Id, ui.Device.RegistrationFailureReason);
                    CrestronConsole.PrintLine("ui_{0} failed registration. Cause: {1}", ui.Id, ui.Device.RegistrationFailureReason);
                }
                //else
                //    CrestronConsole.PrintLine("ui_{0} registration success", ui.Id);
                ui.Device.OnlineStatusChange += new OnlineStatusChangeEventHandler(UiOnlineHandler);
                try
                {
                    CrestronConsole.PrintLine("ui_{0} LoadSmartObjects", ui.Id);
                    ui.Device.LoadSmartObjects(Directory.GetApplicationDirectory() + "\\Resources\\Navitas TSW-750 v2.2.sgd");
                    //CrestronConsole.AddNewConsoleCommand(initUi1, "InitUi1", "", ConsoleAccessLevelEnum.AccessOperator);
                    ui.Device.SmartObjects[SMART_ID_KEYPAD       ].SigChange += new SmartObjectSigChangeEventHandler(PasswordEventHandler);
                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].SigChange += new SmartObjectSigChangeEventHandler(SourceSelectLocalEventHandler);
                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_0].SigChange += new SmartObjectSigChangeEventHandler(SourceSelectLoc00EventHandler);
                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_1].SigChange += new SmartObjectSigChangeEventHandler(SourceSelectLoc01EventHandler);
                    ui.Device.SmartObjects[SMART_ID_DEST_LIST].SigChange += new SmartObjectSigChangeEventHandler(DestSelectEventHandler);
                    ui.Device.SmartObjects[SMART_ID_MICS].SigChange += new SmartObjectSigChangeEventHandler(MicsEventHandler);
                    ui.Device.SmartObjects[SMART_ID_CAMERA_DPAD].SigChange += new SmartObjectSigChangeEventHandler(CamDPadEventHandler);
                    ui.Device.SmartObjects[SMART_ID_CAMERA_PRESETS].SigChange += new SmartObjectSigChangeEventHandler(CamPresetEventHandler);
                    ui.Device.SmartObjects[SMART_ID_LIGHT_PRESETS].SigChange += new SmartObjectSigChangeEventHandler(LightPresetEventHandler);
                }
                catch (Exception e)
                {
                    ErrorLog.Error("LoadSmartObjects exception: {0}", e.ToString());
                    CrestronConsole.PrintLine("LoadSmartObjects exception: {0}", e.ToString());
                }
            }
        }
        public override void InitializeSystem()
        {
            CrestronConsole.PrintLine("InitializeSystem");
            configAudioDsp();
            configDisplays();
            configDocCams();
        }

        #endregion
        #region event handlers
        private void cs_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    break;
                case (eProgramStatusEventType.Resumed):
                    break;
                case (eProgramStatusEventType.Stopping):
                    break;
            }
        }
        private void cs_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case (eSystemEventType.DiskInserted):
                    //Removable media was detected on the system
                    break;
                case (eSystemEventType.DiskRemoved):
                    //Removable media was detached from the system
                    break;
                case (eSystemEventType.Rebooting):
                    //The system is rebooting. 
                    //Very limited time to perform clean up and save any settings to disk.
                    break;
            }
        }
        private void EiscSigChangeHandler(GenericBase device, SigEventArgs args)
        {
            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    switch (args.Sig.Number)
                    {
                        //case EISC_??: doStuff(args.Sig.BoolValue); break;
                        default: ; break;
                    }
                    break;
            }
        }
        private void CommonOnlineHandler(GenericBase device, OnlineOfflineEventArgs args)
        {
            CrestronConsole.PrintLine("{0} online status {1}", device.ToString(), args.DeviceOnLine);
        }
        private void RelayChangeHandler(Relay device, RelayEventArgs args)
        {
            CrestronConsole.PrintLine("RelayChangeHandler, {0} args {1}", device.ToString(), args.State);
        }
        private void VersiportChangeHandler(Versiport device, VersiportEventArgs args)
        {
            CrestronConsole.PrintLine("VersiportChangeHandler, {0} args {1}", device.ToString(), args.Event);
            switch (args.Event)
            {
                case eVersiportEvent.NA: break;                             // Not available.
                case eVersiportEvent.DigitalInChange: break;                // The current state of the digital input has changed.
                case eVersiportEvent.DisablePullUpResistorChange: break;    // The state of the Disable Pull Up Resistor has changed. Only supported on devices that send feedback.
                case eVersiportEvent.DigitalOutChange: break;               // The current state of the digital output has changed.  Only supported on devices that send feedback.
                case eVersiportEvent.AnalogInChange: break;                 // The current value of the analog input has changed.
                case eVersiportEvent.AnalogMinChangeChange: break;          // The current value of the Minimum analog change needed to fire has changed. Only supported on devices that send feedback.
                case eVersiportEvent.VersiportConfigurationChange: break;   // The current configuration of the port has changed. Only supported on devices that send feedback.
            }
        }
        private void DigitalInputChangeHandler(DigitalInput device, DigitalInputEventArgs args)
        {
            CrestronConsole.PrintLine("DigitalInputChangeHandler, {0} args {1}", device.ToString(), args.State);
        }
        private void UiSigChangeHandler(GenericBase currentDevice, SigEventArgs args)
        {
            try
            {
                UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
                CrestronConsole.PrintLine("UiSigChangeHandler ui_{0} {1} {2}, {3}\n", ui.Id, args.Sig.Type, args.Sig.Number, args.Sig.Name);
                switch (args.Sig.Type)
                {
                    case eSigType.Bool:
                        switch (args.Sig.Number)
                        {
                            // momentary functions
                            // ..
                            // presses or releases handled separately
                            default:
                                if (args.Sig.BoolValue) // presses
                                {
                                    CrestronConsole.PrintLine("press {0}", args.Sig.Number);
                                    switch (args.Sig.Number)
                                    {
                                        case DIG_START:
                                            if(displays == null)
                                                configDisplays();
                                            if (config.loginRequired)
                                                ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_LOGIN].BoolValue = true;
                                            else
                                            {
                                                ToggleCountdownSub(currentDevice);
                                                //((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_MAIN].Pulse();
                                                //((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_DEV].Pulse();
                                                if (SystemControl != null && SystemControl.SystemControlType == eSystemControlType.Dmps3SystemControl)
                                                    ((Dmps3SystemControl)SystemControl).SystemPowerOn();
                                            }
                                            break;
                                        case DIG_CANCEL:
                                            CloseSubs(currentDevice);
                                            break;
                                        case DIG_POWER:
                                            confirmState = (ushort)args.Sig.Number;
                                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CONFIRM].BoolValue = true;
                                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = "Power off selected";
                                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = "Press confirm to power the system off or back to cancel";
                                            break;
                                        case DIG_CONFIRM:
                                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CONFIRM].BoolValue = false;
                                            switch (confirmState)
                                            {
                                                case DIG_POWER:
                                                    SystemPowerOff(currentDevice);
                                                    break;
                                                case DIG_JOIN:
                                                    JoinRooms(config.locations[0], config.locations[1]);
                                                    break;
                                            }
                                            break;
                                        case DIG_HOME:
                                            ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_SPLASH].Pulse();
                                            break;
                                        case DIG_LIGHTS:
                                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_LIGHTS].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_LIGHTS].BoolValue;
                                            ((BasicTriList)currentDevice).BooleanInput[DIG_HAS_LIGHTS].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_HAS_LIGHTS].BoolValue;
                                            break;
                                        case DIG_MIC:
                                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_MIC].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_MIC].BoolValue;
                                            //var test1 = config.vidInputs.Find(x => x.devType == (ushort)VidDev.CAM_1);
                                            //var test2 = config.vidInputs.Find(x => x.devType == (ushort)VidDev.CAM_2);
                                            //((BasicTriList)currentDevice).BooleanInput[DIG_HAS_CAM].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_HAS_MIC].BoolValue;
                                            break;
                                        case DIG_RECORD:
                                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_RECORD].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_RECORD].BoolValue;
                                            ((BasicTriList)currentDevice).BooleanInput[args.Sig.Number].BoolValue = !((BasicTriList)currentDevice).BooleanInput[args.Sig.Number].BoolValue;
                                            break;
                                        case DIG_PICMUTE:
                                            ((BasicTriList)currentDevice).BooleanInput[args.Sig.Number].BoolValue = !((BasicTriList)currentDevice).BooleanInput[args.Sig.Number].BoolValue;
                                            break;
                                        case DIG_JOIN:
                                            confirmState = (ushort)args.Sig.Number;
                                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CONFIRM].BoolValue = true;
                                            if (RoomIsJoined(ui.location))
                                            {
                                                ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = "Separate rooms";
                                                ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = "Press confirm to separate the rooms or back to cancel";
                                            }
                                            else
                                            {
                                                ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = "Join rooms";
                                                ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = "Press confirm to join the rooms or back to cancel";
                                            }
                                            break;
                                        case DIG_VOL_UP:
                                            ui.DoVol(Direction.UP);
                                            break;
                                        case DIG_VOL_DN:
                                            ui.DoVol(Direction.DOWN);
                                            break;
                                        case DIG_VOL_MUTE:
                                            DoMute(ui.location, PowerStates.TOGGLE);
                                            break;
                                        case DIG_AUD_DSP_ONLINE:
                                            //audDspComms.Connect();
                                            break;
                                        case DIG_DOCCAM_POWER:
                                        case DIG_DOCCAM_AUTOFOCUS:
                                        case DIG_DOCCAM_LAMP:
                                        case DIG_DOCCAM_BACKLIGHT:
                                        case DIG_DOCCAM_ZOOM_IN:
                                        case DIG_DOCCAM_ZOOM_OUT:
                                        case DIG_DOCCAM_FOCUS_IN:
                                        case DIG_DOCCAM_FOCUS_OUT:
                                        case DIG_DOCCAM_BRIGHT_UP:
                                        case DIG_DOCCAM_BRIGHT_DN:
                                            SendDocCamCommand(ui.location, args.Sig.Number);
                                            break;
                                        default:
                                            {
                                                if (args.Sig.Number > 149 && args.Sig.Number < 200)
                                                {
                                                }
                                                else
                                                {
                                                    //KeyValuePair<string, ushort> _keyVal = new KeyValuePair<string, ushort>();
                                                }
                                                break;
                                            }
                                    }
                                }
                                else  // releases
                                {
                                    switch (args.Sig.Number)
                                    {
                                        case DIG_VOL_UP:
                                        case DIG_VOL_DN:
                                            ui.DoVol(Direction.STOP);
                                            break;
                                        case DIG_LIGHTS_UP:
                                        case DIG_LIGHTS_DOWN:
                                            DoLightRamp(ui.location, Direction.STOP);
                                            break;
                                        default:
                                            if (args.Sig.Number > 149 && args.Sig.Number < 200)
                                                SendEiscDig(args.Sig.Number, args.Sig.BoolValue);
                                            break;
                                    }
                                }
                                break;
                        }
                        break;
                    case eSigType.UShort:
                        switch (args.Sig.Number)
                        {
                            case (15):
                                break;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("UiSigChangeHandler exception: {0}", e.ToString());
            }
        }
        private void CamDPadEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            CrestronConsole.PrintLine("CamDPadEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
        }
        private void CamPresetEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            CrestronConsole.PrintLine("CamPresetEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
        }
        private void LightPresetEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            CrestronConsole.PrintLine("LightPresetEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
        }
        private void MicsEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            ushort digIncJoin = DIG_INC_MICS; // Digital Increment Join value in Subpage Reference List determines how many digitals on each list item.
            ushort index = (ushort)(((join-1)/digIncJoin)+1);
            ushort function = (ushort)(((join-1)%digIncJoin)+1);

            if (args.Sig.Type == eSigType.Bool)
            {
                try
                {
                    CrestronConsole.PrintLine("MicsEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
                    if (args.Sig.BoolValue) // presses
                    {
                        switch (function)
                        {
                            case 1: // up
                                CrestronConsole.PrintLine(" mic {0} up", index);
                                mics[index-1].DoVol(Direction.UP); // not ui.DoMicVol
                                break;
                            case 2: // down
                                CrestronConsole.PrintLine(" mic {0} down", index);
                                mics[index - 1].DoVol(Direction.DOWN);
                                break;
                        }
                    }
                    else // release
                    {
                        switch (function)
                        {
                            case 1: // up
                            case 2: // down
                                mics[index - 1].DoVol(Direction.STOP);
                                break;
                        }
                    }
                    /*
                    ui.Device.SmartObjects[SMART_ID_MICS].BooleanInput[String.Format("fb{0}", join)].BoolValue = !ui.Device.SmartObjects[SMART_ID_MICS].BooleanInput[String.Format("fb{0}", join)].BoolValue;
                    ui.Device.SmartObjects[SMART_ID_MICS].UShortInput[String.Format("an_fb{0}", join)].UShortValue = (ushort)20000;
                    ui.Device.SmartObjects[SMART_ID_MICS].StringInput[String.Format("text-o{0}", 1)].StringValue = "Mic 1";
                    
                    */
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("MicsEventHandler exception: {0}", e.ToString());
                }
            }
            else if (args.Sig.Type == eSigType.UShort)
            {
                CrestronConsole.PrintLine("  ushort {0} {1}", args.Sig.Number, args.Sig.Name);
            }
            else
                CrestronConsole.PrintLine("         args.Sig.Type {0}", args.Sig.Type);
        }
        private void PasswordEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            CrestronConsole.PrintLine("PasswordEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
            if (args.Sig.Type == eSigType.Bool && args.Sig.BoolValue) // presses only
            {
                try
                {
                    CrestronConsole.PrintLine("PasswordEventHandler ui_{0} dig:{1} {2}", ui.Id, args.Sig.Name, args.Sig.BoolValue);
                    if (args.Sig.Name == "Del")
                    {
                        //CrestronConsole.PrintLine("{0} pressed", args.Sig.Name);
                        if (keypadString.Length > 0)
                            keypadString.Length -= 1;
                        ((BasicTriList)currentDevice).StringInput[SER_PASSWORD_TEXT].StringValue = keypadString.ToString();
                   }
                    else if (args.Sig.Name == "Enter")
                    {
                        //CrestronConsole.PrintLine("{0} pressed, ", args.Sig.Name);
                        if (keypadString.ToString().Equals(config.passwordUser) 
                            || keypadString.ToString().Equals(config.passwordAdmin)
                            || keypadString.ToString().Equals(PasswordBackDoor))
                        {
                            CrestronConsole.PrintLine("Password success, confirmState {0}", confirmState);
                            ((BasicTriList)currentDevice).StringInput[SER_PASSWORD_TEXT].StringValue = "";
                            switch(confirmState)
                            {
                                case DIG_POWER:
                                    ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_SPLASH].Pulse();
                                    break;
                                case DIG_JOIN:
                                    JoinRooms(config.locations[0], config.locations[1]);
                                    break;
                                case 0:
                                    ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_MAIN].Pulse();
                                    break;
                            }
                            confirmState = 0;
                            UpdateFb(currentDevice);
                        }
                        else
                        {
                            ((BasicTriList)currentDevice).StringInput[SER_PASSWORD_TEXT].StringValue = "Wrong";
                            ui.StartPassTimer();
                        }
                        keypadString.Length = 0;
                    }
                    else
                    {
                        CrestronConsole.PrintLine("join {0}", join);
                        if (join < 10)
                        {
                            keypadString.Append(args.Sig.Name);
                            ((BasicTriList)currentDevice).StringInput[SER_PASSWORD_TEXT].StringValue = keypadString.ToString();
                            CrestronConsole.PrintLine("keypadString {0}", keypadString);
                        }
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("PasswordEventHandler exception: {0}", e.ToString());
                }
            }
            else if (args.Sig.Type == eSigType.UShort)
            {
                CrestronConsole.PrintLine("  ushort {0} {1}", args.Sig.Number, args.Sig.Name);
            }
            else
                CrestronConsole.PrintLine("         args.Sig.Type {0}", args.Sig.Type);
        }
        private void DestSelectEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            //CrestronConsole.PrintLine("DestSelectEventHandler");
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            uint join = (uint)Utils.atoi(args.Sig.Name);
            if (args.Sig.Type == eSigType.Bool)
            {
                //CrestronConsole.PrintLine("DestSelectEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
                if (args.Sig.BoolValue) // presses only
                    DoDestPress(ui, join);
            }
            else if (args.Sig.Type == eSigType.UShort)
            {
                CrestronConsole.PrintLine("  ushort {0} {1}, value:{2}", args.Sig.Number, args.Sig.Name, args.Sig.UShortValue);
                if (args.Sig.Name.Contains("Held"))
                {
                    CrestronConsole.PrintLine("  held:{0}", config.vidOutputs[args.Sig.UShortValue - 1].devName);

                    switch (config.vidOutputs[args.Sig.UShortValue - 1].devType)
                    {
                            /*
                        case (ushort)VidDev.PROJ_1:
                        case (ushort)VidDev.PROJ_2:
                        case (ushort)VidDev.PROJ_3:
                            ui.CurrentDeviceControl = config.vidOutputs[args.Sig.UShortValue - 1].devType; 
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_PROJ].BoolValue = true;
                            break;
                             */ 
                        default:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = true;
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = String.Format("Button held");
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = String.Format("There are no controls for {0}", config.vidOutputs[args.Sig.UShortValue - 1].devName);
                            CommonTimer = new CTimer(delegate(object obj) { CloseSubs((GenericBase)obj); CommonTimer.Dispose(); }, currentDevice, 2000);
                            break;
                    }
                }
            }
            else
                CrestronConsole.PrintLine("         args.Sig.Type {0}", args.Sig.Type);
        }
        private void SourceSelectLocalEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            uint join = (uint)Utils.atoi(args.Sig.Name);
            if (args.Sig.Type == eSigType.Bool)
            {
                //CrestronConsole.PrintLine("SourceSelectLocalEventHandler ui_{0} dig:{1}, {2}, {3}", ui.Id, args.Sig.Number, args.Sig.Name, args.Sig.BoolValue);
                if (args.Sig.BoolValue) // presses only
                    DoSourcePress(ui, join);
                else // release
                    DoSourceRelease(ui, join);
            }
            else if (args.Sig.Type == eSigType.UShort)
            {
                CrestronConsole.PrintLine("  ushort {0} {1}, value:{2}", args.Sig.Number, args.Sig.Name, args.Sig.UShortValue);
                if (args.Sig.Name.Contains("Held"))
                {
                    CrestronConsole.PrintLine("  held:{0}", config.vidInputs[args.Sig.UShortValue-1].devName);

                    switch (config.vidInputs[args.Sig.UShortValue - 1].devType)
                    {
                        case (ushort)VidDev.CAM_1:
                        case (ushort)VidDev.CAM_2:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CAM].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CAM].BoolValue;
                            break;
                        case (ushort)VidDev.DOCCAM:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_DOCCAM].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_DOCCAM].BoolValue;
                            break;
                        default:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = true;
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = String.Format("Button held");
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = String.Format("There are no controls for {0}", config.vidInputs[args.Sig.UShortValue - 1].devName);
                            CommonTimer = new CTimer(delegate(object obj) { CloseSubs((GenericBase)obj); CommonTimer.Dispose(); }, currentDevice, 2000, 0);
                            break;
                    }
                }
            }
            else
                CrestronConsole.PrintLine("         args.Sig.Type {0}", args.Sig.Type);
        }
        private void SourceSelectLoc00EventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            uint join = (uint)Utils.atoi(args.Sig.Name);
            if (args.Sig.Type == eSigType.Bool)
            {
                //CrestronConsole.PrintLine("SourceSelectLocalEventHandler ui_{0} dig:{1}, {2}, {3}", ui.Id, args.Sig.Number, args.Sig.Name, args.Sig.BoolValue);
                if (args.Sig.BoolValue) // presses only
                    DoSourcePress(ui, join);
                else // release
                    DoSourceRelease(ui, join);
            }
            else if (args.Sig.Type == eSigType.UShort)
            {
                CrestronConsole.PrintLine("  ushort {0} {1}, value:{2}", args.Sig.Number, args.Sig.Name, args.Sig.UShortValue);
                if (args.Sig.Name.Contains("Held"))
                {
                    CrestronConsole.PrintLine("  held:{0}", config.vidInputs[args.Sig.UShortValue - 1].devName);

                    switch (config.vidInputs[args.Sig.UShortValue - 1].devType)
                    {
                        case (ushort)VidDev.CAM_1:
                        case (ushort)VidDev.CAM_2:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CAM].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CAM].BoolValue;
                            break;
                        default:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = true;
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = String.Format("Button held");
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = String.Format("There are no controls for {0}", config.vidInputs[args.Sig.UShortValue - 1].devName);
                            CommonTimer = new CTimer(delegate(object obj) { CloseSubs((GenericBase)obj); CommonTimer.Dispose(); }, currentDevice, 2000, 0);
                            break;
                    }
                }
            }
            else
                CrestronConsole.PrintLine("         args.Sig.Type {0}", args.Sig.Type);
        }
        private void SourceSelectLoc01EventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int firstInput = config.vidInputs.FindIndex(x => x.room == (byte)2);
            uint join = (uint)Utils.atoi(args.Sig.Name);
            if (args.Sig.Type == eSigType.Bool)
            {
                //CrestronConsole.PrintLine("SourceSelectLoc01EventHandler ui_{0} dig:{1}, {2}, {3}", ui.Id, args.Sig.Number, args.Sig.Name, args.Sig.BoolValue);
                if (args.Sig.BoolValue) // presses only
                    DoSourcePress(ui, join);
                else // release
                    DoSourceRelease(ui, join);
            }
            else if (args.Sig.Type == eSigType.UShort)
            {
                CrestronConsole.PrintLine("  ushort {0} {1}, value:{2}", args.Sig.Number, args.Sig.Name, args.Sig.UShortValue);
                if (args.Sig.Name.Contains("Held"))
                {
                    CrestronConsole.PrintLine("  held:{0}", config.vidInputs[args.Sig.UShortValue-1].devName);
                    switch (config.vidInputs[args.Sig.UShortValue - 1].devType)
                    {
                        case (ushort)VidDev.CAM_1:
                        case (ushort)VidDev.CAM_2:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CAM].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CAM].BoolValue;
                            break;
                        default:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = true;
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = String.Format("Button held");
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = String.Format("There are no controls for {0}", config.vidInputs[args.Sig.UShortValue - 1].devName);
                            CommonTimer = new CTimer(delegate(object obj) { CloseSubs((GenericBase)obj); CommonTimer.Dispose(); }, currentDevice, 2000, 0);
                            break;
                    }
                }
            }
            else
                CrestronConsole.PrintLine("         args.Sig.Type {0}", args.Sig.Type);
         }
        private void UiOnlineHandler(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            try
            {
                UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
                CrestronConsole.PrintLine("UiOnlineHandler ui_{0} {1}", ui.Id, args.DeviceOnLine.ToString());
                if (args.DeviceOnLine)
                {
                    ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_SPLASH].Pulse();
                    ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_ONLINE].BoolValue = true;
                    confirmState = 0;
                    UpdateFb(currentDevice);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("UiOnlineHandler exception: {0}", e.ToString());
            }
        }
        //private void MicrophoneChange(MicrophoneBase device, GenericEventArgs args)
        //{
        //    CrestronConsole.PrintLine("{0} MicrophoneChange ID {1}", device.ToString(), args.EventId);
        //}
        private void vidMatrixSystemChange(Switch device, DMSystemEventArgs args)
        {
            try
            {
                CrestronConsole.PrintLine("{0} vidMatrixSystemChange ID {1}, index {2}", device == null ? "" : device.ToString(), args.EventId, args.Index);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("vidMatrixSystemChange exception: {0}", e.ToString());
            }

        }
        private void vidMatrixInputChange(Switch device, DMInputEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} vidMatrixInputChange ID {1}, input {2}, stream {3}", device == null ? "" : device.ToString(), args.EventId, args.Number, args.Stream);
        }
        private void vidMatrixOutputChange(Switch device, DMOutputEventArgs args)
        {
            try
            {
                //CrestronConsole.PrintLine("vidMatrixOutputChange: out {0}", args.Number);
                int numberOfInputs;
                int numberOfOutputs;
                uint input = 0;
                if (device == null) // DMPS - inputs and outputs are just indexes
                {
                    device = vidMatrix;
                    numberOfInputs = SwitcherInputs.Count;
                    numberOfOutputs = SwitcherOutputs.Count;
                    if (args.Number <= numberOfOutputs)
                    {
                        uint output = args.Number;
                        switch (SwitcherOutputs[args.Number].CardInputOutputType)
                        {
                            case eCardInputOutputType.Dmps3DmOutput:
                                Card.Dmps3DmOutput Dmps3DmOutput = (Card.Dmps3DmOutput)SwitcherOutputs[output];
                                input = Dmps3DmOutput.VideoOutFeedback == null ? 0 : Dmps3DmOutput.VideoOutFeedback.Number;
                                break;
                            case eCardInputOutputType.Dmps3HdmiOutput:
                                Card.Dmps3HdmiOutput Dmps3HdmiOutput = (Card.Dmps3HdmiOutput)SwitcherOutputs[output];
                                input = Dmps3HdmiOutput.VideoOutFeedback == null ? 0 : Dmps3HdmiOutput.VideoOutFeedback.Number;
                                break;
                            case eCardInputOutputType.Dmps3ProgramOutput:
                                Card.Dmps3ProgramOutput Dmps3ProgramOutput = (Card.Dmps3ProgramOutput)SwitcherOutputs[output];
                                input = Dmps3ProgramOutput.VideoOutFeedback == null ? 0 : Dmps3ProgramOutput.VideoOutFeedback.Number;
                                break;
                            case eCardInputOutputType.Dmps3Aux1Output:
                                Card.Dmps3Aux1Output Dmps3Aux1Output = (Card.Dmps3Aux1Output)SwitcherOutputs[output];
                                input = Dmps3Aux1Output.VideoOutFeedback == null ? 0 : Dmps3Aux1Output.VideoOutFeedback.Number;
                                break;
                            case eCardInputOutputType.Dmps3Aux2Output:
                                Card.Dmps3Aux2Output Dmps3Aux2Output = (Card.Dmps3Aux2Output)SwitcherOutputs[output];
                                input = Dmps3Aux2Output.VideoOutFeedback == null ? 0 : Dmps3Aux2Output.VideoOutFeedback.Number;
                                break;
                            case eCardInputOutputType.Dmps3CodecOutput:
                                Card.Dmps3CodecOutput Dmps3CodecOutput = (Card.Dmps3CodecOutput)SwitcherOutputs[output];
                                input = Dmps3CodecOutput.VideoOutFeedback == null ? 0 : Dmps3CodecOutput.VideoOutFeedback.Number;
                                break;
                            default:
                                CrestronConsole.PrintLine("SwitcherOutput[{0}] is unknown type {1} [{2}]", args.Number, SwitcherOutputs[args.Number].CardInputOutputType, SwitcherOutputs[args.Number]);
                                break;
                        //CrestronConsole.PrintLine("Video Input {0} switched to Output {1}", input, output);
                        }
                    }
                }
                else // DM or HD
                {
                    numberOfInputs = device.NumberOfInputs;
                    numberOfOutputs = device.NumberOfOutputs;
                    if (args.Number <= numberOfOutputs)
                    {
                        if (device.Outputs.Contains(args.Number))
                        {
                            DMOutput output = device.Outputs[args.Number];
                            input = output.VideoOutFeedback == null ? 0 : output.VideoOutFeedback.Number;
                            //CrestronConsole.PrintLine("   device.Outputs[args.Number]: {0}", device.Outputs[args.Number]);
                        }
                        else
                            CrestronConsole.PrintLine("     ***no key: {0}", args.Number);
                    }
                }

                switch (args.EventId)
                {
                    case (DMOutputEventIds.VideoOutEventId)://Print what input is routed to the output.
                        {
                            //CrestronConsole.PrintLine("     Video Input {0} to Output {1}", input, args.Number);
                            DoVidOutFb(input, args.Number);
                            break;
                        }
                    case (DMOutputEventIds.AudioOutEventId)://Print what audio is routed to the output.
                        //CrestronConsole.PrintLine("     Audio Input {0} to Output {1}", input, args.Number);
                        //uiHandler.DoAudOutFb(input, args.Number);
                        break;
                    case (DMOutputEventIds.MasterVolumeFeedBackEventId):
                        CrestronConsole.PrintLine("     MasterVolumeFeedBackEventId");
                        break;
                    default:
                        //CrestronConsole.PrintLine("     args.EventId:{0}", args.EventId);
                        break;
                }
                //CrestronConsole.PrintLine("num inputs {0}, num outputs {1}", numberOfInputs, numberOfOutputs);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("vidMatrixOutputChange exception: {0}", e.ToString());
            }
            //CrestronConsole.PrintLine("{0} vidMatrixOutputChange ID {1}, output {2}, stream {3}", device == null ? "" : device.ToString(), args.EventId, args.Number, args.Stream);
        }
        private void dmTxBaseEventHandler(GenericBase device, BaseEventArgs args)
        {
            switch (args.EventId)
            {
                case DmTx200Base.VideoSourceFeedbackEventID:
                    CrestronConsole.PrintLine("{0} dmTxBaseEventHandler, VideoSourceFeedbackEventId {1}", device == null ? "" : device.ToString(), args.EventId.ToString());
                    break;

                default:
                    break;
            }
        }
        private void dmRxBaseEventHandler(GenericBase device, BaseEventArgs args)
        {
            switch (args.EventId)
            {
                case DmTx200Base.VideoSourceFeedbackEventID:
                    CrestronConsole.PrintLine("{0} dmRxBaseEventHandler, VideoSourceFeedbackEventId {1}", device == null ? "" : device.ToString(), args.EventId.ToString());
                    break;

                default:
                    break;
            }
        }
        private void dmTxDisplayPortInputStreamChangeEventHandler(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
        {
            CrestronConsole.PrintLine("{0} DisplayPortInputStreamChange {1}", inputStream.ToString(), args.ToString());
            switch (args.EventId)
            {
                case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId:
                    break;
            }
        }
        private void dmTxHdmiInputStreamChangeEventHandler(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
        {
            CrestronConsole.PrintLine("{0} HdmiInputStreamChange {1}", inputStream.ToString(), args.ToString());
            switch (args.EventId)
            {
                case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId: break;
            }
        }
        private void dmTxVgaInputStreamChangeEventHandler(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
        {
            CrestronConsole.PrintLine("{0} VgaInputStreamChange {1}", inputStream.ToString(), args.ToString());
            switch (args.EventId)
            {
                case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId:
                    break;
            }
        }
        private void dmTxHdmiAttributeChangeEventHandler(object sender, GenericEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} HdmiAttributeChange {1}", sender.ToString(), args.ToString());
            switch (args.EventId)
            {
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.FramesPerSecondFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.HorizontalResolutionFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.VerticalResolutionFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.InterlacedFeedbackEventId:
                    break;

                default:
                    break;
            }
        }
        private void dmTxDisplayPortAttributeChangeEventHandler(object sender, GenericEventArgs args)
        {
            CrestronConsole.PrintLine("{0} DisplayPortAttributeChange {1}", sender.ToString(), args.ToString());
            switch (args.EventId)
            {
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.FramesPerSecondFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.HorizontalResolutionFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.VerticalResolutionFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.InterlacedFeedbackEventId:
                    break;
            }
        }
        private void dmTxVgaAttributeChangeEventHandler(object sender, GenericEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} VgaAttributeChange {1}", sender.ToString(), args.ToString());
            switch (args.EventId)
            {
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.FramesPerSecondFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.HorizontalResolutionFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.VerticalResolutionFeedbackEventId:
                case Crestron.SimplSharpPro.DM.VideoAttributeEventIds.InterlacedFeedbackEventId:
                    break;
                default:
                    break;
            }
        }
        private void DisplayStateChangeEvent(DisplayStateObjects displayStateObject, IBasicVideoDisplay display, byte id)
        {
            switch (displayStateObject)
            {
                case DisplayStateObjects.Audio:
                    //_xpanel.StringInput[402].StringValue = display.Muted ? "muted" : "not muted";
                    //_xpanel.StringInput[401].StringValue = display.VolumePercent.ToString(CultureInfo.InvariantCulture);
                    //_xpanel.UShortInput[400].UShortValue = (ushort)display.VolumePercent;
                    break;
                case DisplayStateObjects.Connection:
                    //_xpanel.StringInput[30].StringValue = display.Connected ? "connected" : "not connected";
                    break;
                case DisplayStateObjects.Input:
                    //_xpanel.StringInput[403].StringValue = display.InputSource.InputType.ToString();
                    break;
                case DisplayStateObjects.Power:
                    //if (display.WarmingUp)
                        //_xpanel.StringInput[400].StringValue = "warming up";
                    //else if (display.CoolingDown)
                        //_xpanel.StringInput[400].StringValue = "cooling down";
                    //else
                        //_xpanel.StringInput[400].StringValue = display.PowerIsOn ? "on" : "off";
                    break;
            }
        }
        private void Dmps3HdmiInputStreamCec_CecChange(Cec cecDevice, CecEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((HdmiInputWithCEC)(cecDevice.Owner)).InputOutput;
            CrestronConsole.PrintLine("CEC Device Info Change on {0} Number {1} [{2}], Event Id {3}", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString(), args.EventId);
        }
        private void VgaDviInputPortVideoControlsBasic_ControlChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((VgaDviInputPort)(((VideoControlsBasic)sender).Owner)).InputOutput;
            CrestronConsole.PrintLine("VgaDviInputPortVideoControlsBasic_ControlChange Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void VgaDviInputPortVideoAttributesBasic_AttributeChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((VgaDviInputPort)(((VideoAttributesBasic)sender).Owner)).InputOutput;
            CrestronConsole.PrintLine("VideoAttributesBasic Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void BncInputPortVideoAttributes_AttributeChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((Component)(((VideoControlsBasic)sender).Owner)).InputOutput;
            CrestronConsole.PrintLine("VideoControlsBasic Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void BncInputPortVideoControls_ControlChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((Component)(((VideoAttributesBasic)sender).Owner)).InputOutput;
            CrestronConsole.PrintLine("VideoAttributesBasic Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void DmInputPortVideoAttributes_AttributeChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((Dmps3DmInputPort)(((VideoAttributesEnhanced)sender).Owner)).InputOutput;
            CrestronConsole.PrintLine("VideoControlsEnhanced Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void Dmps3HdmiOutputStreamCec_CecChange(Cec cecDevice, CecEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((OutputCardHdmiOutBasicPort)(cecDevice.Owner)).InputOutput;
            CrestronConsole.PrintLine("CEC Device Info Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void Dmps3HdmiOutputConnectedDevice_DeviceInformationChange(ConnectedDeviceInformation connectedDevice, ConnectedDeviceEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((OutputCardHdmiOutBasicPort)(connectedDevice.Owner)).InputOutput;
            //CrestronConsole.PrintLine("Connected Device Info Change on {0} Number {1} [{2}], Event Id {3}", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString(), args.EventId);
        }
        private void Dmps3DmOutputConnectedDevice_DeviceInformationChange(ConnectedDeviceInformation connectedDevice, ConnectedDeviceEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((AdvDmOutputCardDmPort)(connectedDevice.Owner)).InputOutput;
            //CrestronConsole.PrintLine("Connected Device Info Change on {0} Number {1} [{2}], Event Id {3}", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString(), args.EventId);
        }
        #endregion
        #region fb functions
        private void CloseSubs(GenericBase currentDevice)
        {
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_LOGIN].BoolValue = false;
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = false;
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CONFIRM].BoolValue = false;
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_LIGHTS].BoolValue = false;
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_MIC].BoolValue = false;
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_RECORD].BoolValue = false;
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CAM].BoolValue = false;
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_DOCCAM].BoolValue = false;
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_PROJ].BoolValue = false;
        }
        private void ToggleCountdownSub(GenericBase currentDevice)
        {
            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue;
            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = "System Starting";
            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = "Please wait";
            ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_MAIN].Pulse();
            ((BasicTriList)currentDevice).UShortInput[1].UShortValue = 0;
            powerTimer = new CTimer(powerTimerExpired, currentDevice, 1, powerTimerInterval);

        }
        private void powerTimerExpired(object obj)
        {
            if (powerTimer != null)
            {
                UiWithLocation ui = uis.Find(x => x.Device == obj);
                ui.Device.UShortInput[1].UShortValue = (ushort)powerBar;
                if (powerBar > 65535 - 655)
                {
                    powerBar = 65535;
                    CloseSubs(ui.Device);
                    //CloseSubs((GenericBase)obj);
                    powerTimer.Stop();
                    powerTimer.Dispose();
                    powerBar = 0;
                }
                else
                    powerBar += 655;
            }
        }
        public void UpdateZoneMuteFb(Location loc)
        {
            try
            {
                CrestronConsole.PrintLine("     UpdateZoneMuteFb lvl {0}", loc.mute);
                foreach (UiWithLocation ui in uis)
                {
                    CrestronConsole.PrintLine("     loc.Owner {0}, ui.Location {1}", loc.Owner, ui.location.Id);
                    if (loc.Owner == ui.location.Owner)
                    {
                        CrestronConsole.PrintLine("     updating mute fb loc {0}", ui.location.Id);
                        ui.Device.BooleanInput[DIG_VOL_MUTE].BoolValue = loc.mute;
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("UpdateZoneMuteFb exception: {0}", e.ToString());
            }
        }
        private string FormatTextForUi(string text, ushort fontSize, string font, string colour)
        {
            if (font == null || font == "")
                font = "Arial";
            if (fontSize == 0)
                fontSize = FontSizeLine[0];
            if (colour == null || colour == "")
                colour = "black";
            switch(colour)
            {
                case "black": colour = "000000"; break;
                case "white": colour = "ffffff"; break;
                case "red"  : colour = "ff0000"; break;
            }
            string str = String.Format("<FONT size=\x22{0}\x22 face=\x22{1}\x22 color=\x22#{2}\x22>{3}</FONT>", fontSize, font, colour, text);
            //CrestronConsole.PrintLine("FormatTextForUi: {0}", str);
            return str;
        }
        private string FormatTextForUi(string text)
        {
            return FormatTextForUi(text, FontSizeLine[0], "Arial", "black");
        }
        public void UpdateFb(GenericBase currentDevice)
        {
            try
            {
                UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
                CrestronConsole.PrintLine("UpdateFb ui:{0}", ui.Id);
                UpdateUiLists(currentDevice);
                CrestronConsole.Print(".");
                ui.Device.BooleanInput[DIG_HAS_MIC].BoolValue = true;
                CrestronConsole.Print(",");
                ui.Device.BooleanInput[DIG_HAS_RECORD].BoolValue = (config.vidOutputs.Find(x => x.devType == (ushort)VidDev.REC_1) != null);
                CrestronConsole.Print(".");
                ui.Device.BooleanInput[DIG_HAS_CAM].BoolValue = (config.vidInputs.Find(x => x.devType == (ushort)VidDev.CAM_1) != null && config.vidInputs.Find(x => x.devType == (ushort)VidDev.CAM_2) != null);
                CrestronConsole.Print("+");
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("UpdateFb exception: {0}", e.ToString());
            }
        }
        public void DoVidOutFb(uint input, uint output)
        {
            try
            {
                byte destVidMatrix = remapVidMatrixOutputs[output];
                /*
                byte serIncJoin = DIG_INC_LAPTOP_LOCATION; // Serial Increment Join value in Subpage Reference List determines how many serials on each list item.
                int join = 2 + (int)(destVidMatrix - 1) * serIncJoin; // 2 for 2nd button
                foreach (KeyValuePair<ushort, UiWithLocation> ui in uis)
                {
                    if (join < 10)
                    {
                        ui.Device.SmartObjects[SMART_ID_LAPTOP_LOCATION].StringInput[String.Format("text-o{0}", join)].StringValue = vidSourceNames[(ushort)input];
                        ui.Device.SmartObjects[SMART_ID_PRESENTER_LOCATION].StringInput[String.Format("text-o{0}", join)].StringValue = vidSourceNames[(ushort)input];
                    }
                }
                 * */
                if (destVidMatrix <= config.locations.Count())
                {
                    //config.locations[destVidMatrix].CurrentVidSourceName = config.vidInputs[(ushort)input].devName; // todo: check for room join
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DoVidOutFb exception: {0}", e.ToString());
            }
        }
        public void UpdateZoneVolFb(Location loc)
        {
            CrestronConsole.PrintLine("     UpdateZoneVolFb lvl {0}", loc.Volume);
            foreach (UiWithLocation ui in uis)
            {
                if (loc.Owner == ui.location.Owner)
                {
                    CrestronConsole.PrintLine("     updating vol fb loc {0}, lvl {1}", ui.location, loc.Volume);
                    ui.Device.UShortInput[ANA_VOL_ACTUAL].UShortValue = (ushort)loc.Volume;
                    ui.Device.UShortInput[ANA_VOL_PERCENT].UShortValue = (ushort)(loc.Volume * 65535 / 100);
                }
            }
        }
        /*
        public void UpdateZoneMicVolFb(Location loc)
        {
            CrestronConsole.PrintLine("     UpdateZoneMicVolFb lvl {0}", loc.MicVolume);
            foreach (UiWithLocation ui in uis)
            {
                if (loc.Owner == ui.location.Owner)
                {
                    CrestronConsole.PrintLine("     updating micVol fb loc {0}", ui.location);
                    ui.Device.UShortInput[ANA_VOL_MIC_ACTUAL].UShortValue = (ushort)loc.MicVolume;
                    ui.Device.UShortInput[ANA_VOL_MIC_PERCENT].UShortValue = (ushort)(loc.MicVolume * 65535 / 100);
                }
            }
        }
         * */
        #endregion
        #region join functions
        public bool RoomIsJoined(Location loc)
        {
            //return (loc.Owned.Count > 1 || (loc.Owner > 0 && loc.Owner != loc.Id));
            return (loc.Owned.Count > 1 || loc.Owner != loc.Id);
        }
        public bool LocationsAreJoined(Location loc1, Location loc2)
        {
            return (loc1.Owner == loc2.Owner);
        }
        private void UpdateUiLists(GenericBase currentDevice) // show/hide buttons depending upon room join status
        {
            try
            {
                String LocationNameText = String.Empty;
                UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
                CrestronConsole.PrintLine("UpdateUiLists for ipid {0}, GetUiLocation {1}, owner {2}", ui.Id, ui.location.Id, ui.location.Owner);
                if (config.locations == null)
                    CrestronConsole.PrintLine("config.locations == null");
                else
                {
                    ui.Device.BooleanInput[DIG_JOIN].BoolValue = RoomIsJoined(ui.location);
                    // make names for locations
                    //CrestronConsole.PrintLine(" locations.Count {0}", config.locations.Count());
                    if (LocationNameText == "")
                        LocationNameText = ui.location.Name;
                    ui.Device.StringInput[SER_LOCATION_NAME].StringValue = LocationNameText;
                    CrestronConsole.PrintLine(" location name {0}", LocationNameText);

                    foreach (Location location in config.locations) // each location
                    {
                        //CrestronConsole.PrintLine(" location {0}, owner {1}", location.Id, location.Owner);
                        if (location.Id <= SER_LOCATION_NAMES.Count())
                            ui.Device.StringInput[SER_LOCATION_NAMES[location.Id-1]].StringValue = location.Name;
                        bool isJoinedWithThisLocation = ui.location.Owner == location.Owner;
                        //CrestronConsole.PrintLine(" {0} isJoinedWithThisLocation {1} {2}", location.Id, ui.location.Id, isJoinedWithThisLocation);
                        //device.SmartObjects[SMART_ID_LOCATION_JOIN].BooleanInput[String.Format("Item {0} Checked", location.Value.Id)].BoolValue = isJoinedWithThisLocation;
                        if (isJoinedWithThisLocation)
                        {
                            //CrestronConsole.PrintLine(" ...LocationNameText {0}", LocationNameText);
                            if (LocationNameText.Length > 0)
                                //LocationNameText = String.Format("{0} + {1}", LocationNameText, config.locations[location.Id].Name);
                                LocationNameText = String.Format("{0} + {1}", LocationNameText, location.Name);
                            else
                                LocationNameText = location.Name;
                            //CrestronConsole.PrintLine(" LocationNameText {0}", LocationNameText);
                        }
                    }

                    ushort micIndex = 0;
                    foreach (Level mic in config.mics)
                    {
                        micIndex++;
                        //CrestronConsole.PrintLine(" micIndex {0}, ui.location.Owner:{1} == config.locations[{2}].Owner:{3}", micIndex, ui.location.Owner, mic.room-1, config.locations[mic.room-1].Owner);
                        ui.Device.SmartObjects[SMART_ID_MICS].BooleanInput[String.Format("Item {0} Visible", micIndex)].BoolValue = ui.location.Owner == config.locations[mic.room-1].Owner;
                        ui.Device.SmartObjects[SMART_ID_MICS].StringInput[String.Format("text-o{0}", micIndex)].StringValue = mic.name;
                    }

                    ui.Device.BooleanInput[DIG_HAS_JOIN].BoolValue = (config.locations.Count > 1);
                    //if (config.locations.Exists(x => x.Id == loc)) // avoid exception
                    //CrestronConsole.PrintLine("ui_{0} id exists", ui.Id);
                    bool joined = RoomIsJoined(ui.location);
                    //CrestronConsole.PrintLine("ui_{0} joined rooms: {1}, inputs: {2}, outputs: {3}", ui.Id, joined, config.vidInputs.Count(), config.vidOutputs.Count());
                    ui.Device.BooleanInput[DIG_SUB_JOIN_0].BoolValue = !joined;
                    ui.Device.BooleanInput[DIG_SUB_JOIN_1].BoolValue = joined;

                    if (joined) // joined
                    {
                        for (int i = 0; i < config.vidInputs.Count(); i++)
                        {
                            if (config.vidInputs[i] != null)
                            {
                                if (config.vidInputs[i].room == 1)
                                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_0].StringInput[String.Format("Set Item {0} Text", (ushort)i + 1)].StringValue = FormatTextForUi(config.vidInputs[i].devName);
                                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_0].BooleanInput[String.Format("Item {0} Visible", (ushort)i + 1)].BoolValue = config.vidInputs[i].room == 1;
                                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_0].StringInput[String.Format("Set Item {0} Icon Serial", (ushort)i + 1)].StringValue = IconsLg[eIconsLgToKey_Inputs[config.vidInputs[i].devType]];
                                if (config.vidInputs[i].room == 2)
                                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_1].StringInput[String.Format("Set Item {0} Text", (ushort)i + 1)].StringValue = FormatTextForUi(config.vidInputs[i].devName);
                                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_1].BooleanInput[String.Format("Item {0} Visible", (ushort)i + 1)].BoolValue = config.vidInputs[i].room == 2;
                                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_1].StringInput[String.Format("Set Item {0} Icon Serial", (ushort)i + 1)].StringValue = IconsLg[eIconsLgToKey_Inputs[config.vidInputs[i].devType]];
                            }
                        }
                        for (int i = 0; i < config.vidOutputs.Count(); i++)
                        {
                            if (config.vidOutputs[i] == null)
                            {
                                CrestronConsole.PrintLine("VidOut {0}: null", i);
                            }
                            else
                            {
                                //CrestronConsole.PrintLine("VidOut {0}: {1}", i, config.vidOutputs[i].devName);
                                ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)i + 1)].StringValue = 
                                        FormatTextForUi(config.vidOutputs[i].devName) + "\r" + FormatTextForUi("Blank", 14, null, "black");
                                ui.Device.SmartObjects[SMART_ID_DEST_LIST].BooleanInput[String.Format("Item {0} Visible", (ushort)i + 1)].BoolValue =
                                        config.locations[config.vidOutputs[i].room-1].Owner == ui.location.Owner // exists
                                        && !(IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]] == "DVR" && !config.showRecordInMenu) // not (DVR && hidden) 
                                        &&   IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]] != "Blank" // not (blank) 
                                        &&  !IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]].Contains("Music Note"); // not (audio) 
                                ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Icon Serial", (ushort)i + 1)].StringValue = 
                                        IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]];
                            }
                        }
                    }
                    else // not joined
                    {
                        for (int i = 0; i < config.vidInputs.Count(); i++)
                        {
                            if (config.vidInputs[i] != null)
                            {
                                //CrestronConsole.PrintLine("VidIn: {0}, config.vidInputs[i].room {1} locations[0].Id {2}", i, config.vidInputs[i].room, config.locations[0].Id);
                                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].BooleanInput[String.Format("Item {0} Visible", (ushort)i + 1)].BoolValue =
                                        config.locations[config.vidInputs[i].room - 1].Owner == ui.location.Owner; // exists
                                        //config.vidInputs[i].room == config.locations[0].Id;
                                if (config.locations[config.vidInputs[i].room - 1].Owner == ui.location.Owner) // exists
                                {
                                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].StringInput[String.Format("Set Item {0} Text", (ushort)i + 1)].StringValue = 
                                                FormatTextForUi(config.vidInputs[i].devName);
                                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].StringInput[String.Format("Set Item {0} Icon Serial", (ushort)i + 1)].StringValue = 
                                                IconsLg[eIconsLgToKey_Inputs[config.vidInputs[i].devType]];
                                    //ui.SmartObjects[SMART_ID_SOURCE_LIST_0].UShortInput[String.Format("Set Item {0} Icon Analog", (ushort)i + 1)].UShortValue = (int)eIconsLg.LAPTOP;
                                }
                            }
                        }
                        for (int i = 0; i < config.vidOutputs.Count(); i++)
                        {
                            if (config.vidOutputs[i] == null)
                            {
                                CrestronConsole.PrintLine("VidOut {0}: null", i);
                            }
                            else
                            {
                                CrestronConsole.PrintLine("VidOut {0}: {1}", i, config.vidOutputs[i].devName);
                                //CrestronConsole.PrintLine("input: {0}, config.vidOutputs[i].room {1} locations[0].Id {2}", i, config.vidOutputs[i].room, config.locations[0].Id);

                                ui.Device.SmartObjects[SMART_ID_DEST_LIST].BooleanInput[String.Format("Item {0} Visible", (ushort)i + 1)].BoolValue =
                                        //config.vidOutputs[i].room == config.locations[0].Id // exists
                                        config.locations[config.vidOutputs[i].room - 1].Owner == ui.location.Owner // exists
                                        && !(IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]] == "DVR" && !config.showRecordInMenu) // not (DVR && hidden) 
                                        &&   IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]] != "Blank" // not (blank) 
                                        &&  !IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]].Contains("Music Note"); // not (audio) 
                                if (config.locations[config.vidOutputs[i].room - 1].Owner == ui.location.Owner)
                                {
                                    ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)i + 1)].StringValue = 
                                            FormatTextForUi(config.vidOutputs[i].devName) + "\r" + FormatTextForUi("Blank", 14, null, "black");
                                    ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Icon Serial", (ushort)i + 1)].StringValue = IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]];
                                    //uis[(ushort)loc].Device.SmartObjects[SMART_ID_DEST_LIST].UShortInput[String.Format("Set Item {0} Icon Analog", (ushort)i + 1)].UShortValue = (int)eIconsLg.PROJ_1;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("UpdateUiLists exception: {0}", e.ToString());
            }
        }
        private void doJoinFeedback()
        {
            CrestronConsole.PrintLine("doJoinFeedback");
            foreach (Location owner in config.locations) // keep track of owned locations
            {
                owner.Owned.Clear(); // clear list of owned locations, rebuild it next
                foreach (Location owned in config.locations) // look for locations with this Owner
                    if (owner.Id == owned.Owner) // not owned so not joined
                    {
                        CrestronConsole.PrintLine("owner {0}, adding owned {1}", owner.Id, owned.Id);
                        owner.Owned.Add(owned.Id);
                    }
                try
                {
                    if (owner.CurrentVidSourceName != null)
                    {
                        int currentSourceOwner = Utils.atoi(owner.CurrentVidSourceName);
                        if (currentSourceOwner > 0 && currentSourceOwner != owner.Id) // clear route for unjoined rooms
                        {
                            DoSwitch(0, owner.Id, SwitchType.VIDEO);
                        }
                    }
                    if (owner.CurrentAudSourceName != null)
                    {
                        int currentSourceOwner = Utils.atoi(owner.CurrentAudSourceName);
                        if (currentSourceOwner > 0 && currentSourceOwner != owner.Id)
                        {
                            DoSwitch(0, owner.Id, SwitchType.AUDIO);
                        }
                    }
                }
                catch (Exception e)
                {
                    CrestronConsole.PrintLine("doJoinFeedback exception {0}", e.ToString());
                }
            }

            foreach (UiWithLocation ui in uis)
                UpdateUiLists(ui.Device);
        }
        public void JoinRooms(Location loc, Location loc2)
        {
            if (loc2 == loc) // this location - separate all rooms from this room
            {
                CrestronConsole.PrintLine("separate all rooms from room {0}", loc);
                foreach (Location location in config.locations) // each location
                {
                    if (location.Id == loc.Id) // this location
                    {
                        location.Owner = location.Id; // become it's own owner 
                        CrestronConsole.PrintLine("local room {0} becomes it's own master", location.Id);
                    }
                    else if (location.Owner == loc.Id) // slave to this location
                    {
                        CrestronConsole.PrintLine("room {0} becomes it's own master", location.Id);
                        location.Owner = location.Id; // become it's own owner 
                    }
                }
            }
            else // any but this location, toggle their owner with this location
            {
                if (loc2.Owner == loc.Id ||
                    loc2.Owner == loc.Owner) // owned by this location OR the same location as this location
                {
                    CrestronConsole.PrintLine("owner {0} location {1}, owned by this location OR the same location", loc2.Owner, loc.Id);
                    if (loc.Owner == loc2.Id) // if this location is currently slaved to that room
                    {
                        CrestronConsole.PrintLine("this location is currently slaved to that location {0}", loc.Owner);
                        // this location must become the new owner of all joined locations and set that location to own itself
                        foreach (Location location in config.locations) // this location takes ownership of the join
                            if (location.Owner == loc2.Id)
                            {
                                location.Owner = loc.Id; // this room becomes it's new owner 
                                CrestronConsole.PrintLine("this location {0} takes ownership of the join", location.Owner);
                            }
                    }
                    loc2.Owner = loc2.Id; // set it to it's own owner
                    CrestronConsole.PrintLine("set it to it's own owner {0}", loc2.Id);
                }
                else // add it to this join
                {
                    loc2.Owner = loc.Owner;
                    CrestronConsole.PrintLine("add location {0} to this join {1}", loc2, loc.Owner);
                    CrestronConsole.PrintLine("location {0} new owner {1}", loc2, loc2.Owner);
                }
            }
            doJoinFeedback();
        }
        #endregion
        #region switching functions

        private void SystemPowerOff(GenericBase currentDevice)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            byte owner = ui.location.Owner;

            //((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = true;
            foreach(UiWithLocation ui_x in uis)
            {
                if(ui.location.Owner == ui.location.Owner)
                ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_SPLASH].Pulse();
            }
            //if (SystemControl != null && SystemControl.SystemControlType == eSystemControlType.Dmps3SystemControl) // TODO
            //    ((Dmps3SystemControl)SystemControl).SystemPowerOff();
            int i = 0;
            foreach (Display display in displays)
            {
                if (display != null)
                {
                    if(config.locations[config.vidOutputs[i].room-1].Owner == owner)
                        display.SetPower(PowerStates.OFF);
                }
                i++;
            }
        }
        private bool isAnyDestSelected(UiWithLocation ui)
        {
            for (int i = 0; i < config.vidOutputs.Count; i++)
                if (ui.AvDestSelected[i])
                    return true;
            return false;
        }
        public bool SourceIsRoutedToDest(uint source)
        {
            for (uint i = 0; i < config.vidOutputs.Count; i++)
                if (config.vidOutputs[(int)i].selectedForSwitching && (config.vidOutputs[(int)i].currentSourceVid == source))
                    return true;
            return false;
        }
        public void DoSwitch(byte source, byte dest, SwitchType type)
        {
            try
            {
                if (!IsDmps() && !vidMatrix.Registered)
                    RegisterVidMatrix();
                byte destVidMatrix = remapVidMatrixOutputs[dest];
                CrestronConsole.PrintLine("DoSwitch({0}, {1}), remap {2}, {3}", source, dest, destVidMatrix, vidMatrix.Name);
                if (vidMatrix.Outputs == null) // not a DMPS
                    CrestronConsole.PrintLine("{0} {1}line", vidMatrix.Type, vidMatrix.IsOnline ? "on" : "off");
                if (type == SwitchType.VIDEO)
                {
                    CrestronConsole.PrintLine("Routing video Input {0} to Output {1}", source, destVidMatrix+1);
                    config.vidOutputs[destVidMatrix-1].currentSourceVid = (ushort)source;
                    if (source > 0)
                    {
                        if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix)
                            vidMatrix.Outputs[destVidMatrix].VideoOut = vidMatrix.Inputs[source]; // dm or hd
                        if (SwitcherOutputs != null && SwitcherOutputs.Count >= destVidMatrix)
                            ((DMOutput)SwitcherOutputs[destVidMatrix]).VideoOut = ((DMInput)SwitcherInputs[source]); // dmps
                        if (displays != null && displays.Count >= destVidMatrix)
                        {
                            CrestronConsole.PrintLine("ui SetPower, {0}", displays[(byte)(destVidMatrix - 1)].GetName());
                            displays[(byte)(destVidMatrix - 1)].SetPower(PowerStates.ON);
                            displays[(byte)(destVidMatrix - 1)].SetSource(DisplaySources.HDBT_1);
                        }
                    }
                    else
                    {
                        if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix)
                            vidMatrix.Outputs[destVidMatrix].VideoOut = null;
                        if (SwitcherOutputs != null && SwitcherOutputs.Count >= destVidMatrix)
                            ((DMOutput)SwitcherOutputs[destVidMatrix]).VideoOut = null; // dmps
                        CrestronConsole.PrintLine("     source {0}", source);
                    }
                    if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix && !(vidMatrix is HdMd4x14kE))
                            vidMatrix.VideoEnter.Pulse();
                    DoVidOutFb(source, destVidMatrix);

                    foreach (UiWithLocation ui in uis)
                    {
                        if (source == 0)
                            ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)dest)].StringValue = FormatTextForUi(config.vidOutputs[(ushort)dest].devName) + "\r" + FormatTextForUi("No input", FontSizeLine[1], null, "black");
                        else
                            ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)dest)].StringValue = FormatTextForUi(config.vidOutputs[(ushort)dest - 1].devName) + "\r" + FormatTextForUi(config.vidInputs[(int)(source - 1)].devName, FontSizeLine[1], null, "black");
                    }
                }
                else // audio
                {
                    CrestronConsole.PrintLine("Routed audio Input {0} to Output {1}", source, dest);
                    if (source > 0)
                    {
                        if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix)
                            vidMatrix.Outputs[destVidMatrix].AudioOut = vidMatrix.Inputs[source];
                        if (SwitcherOutputs != null && SwitcherOutputs.Count >= destVidMatrix)
                            ((DMOutput)SwitcherOutputs[destVidMatrix]).AudioOut = ((DMInput)SwitcherInputs[source]);
                    }
                    else
                    {
                        if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix)
                            vidMatrix.Outputs[destVidMatrix].AudioOut = null;
                        if (SwitcherOutputs != null && SwitcherOutputs.Count >= destVidMatrix)
                            ((DMOutput)SwitcherOutputs[destVidMatrix]).AudioOut = null; // dmps
                    }
                    if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix && !(vidMatrix is HdMd4x14kE))
                        vidMatrix.VideoEnter.Pulse();
                    //eisc.UShortInput[EISC_ANA_SRC[dest - 1]].UShortValue = source;
                    CrestronConsole.PrintLine("     Input {0} to Output {1}", source, dest);
                    Thread.Sleep(10);
                    DoAudOutFb(source, dest);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DoSwitch exception: {0}", e.ToString());
            }
        }
        private void ClearAvSelectBtns(UiWithLocation ui)
        {
            CrestronConsole.PrintLine("ClearAvSelectBtns");
            if (clearSrcBtnsOnSwitch)
            {
                if (ui.SelectedAvSrc > 0)
                    SetAvSrcDigFb(ui, ui.SelectedAvSrc, false);
                //else
                    //; // todo- clear the none selected button
                ui.SelectedAvSrc = 0;
                ui.AvSrcOffSelected = false;
            }
            if (clearDestBtnsOnSwitch)
            {
                for (int i = 0; i < config.vidOutputs.Count; i++)
                {
                    ui.AvDestSelected[i] = false;
                    SetAvDestDigFb(ui, (uint)(i + 1), ui.AvDestSelected[i]);
                }
            }
            if (ui.SelectedAvSrc > 0)
                ui.Device.StringInput[SER_TOP_TXT].StringValue = String.Format("Select a destination to display the {0}", config.vidInputs[ui.SelectedAvSrc - 1].devName);
            else if (isAnyDestSelected(ui))
                ui.Device.StringInput[SER_TOP_TXT].StringValue = "Select a source to display on the selected destinations";
            else
                ui.Device.StringInput[SER_TOP_TXT].StringValue = "Select a source or destination";
        }
        private void sendSrcToSelectedDestinations(UiWithLocation ui, uint src)
        {
            CrestronConsole.PrintLine("sendSrcToSelectedDestinations, src:{0}", src);
            for (uint i = 0; i < config.vidOutputs.Count; i++)
                if (ui.AvDestSelected[(ushort)i])
                {
                    DoSwitch((byte)src, (byte)(i + 1), SwitchType.VIDEO);
                    DoSwitch((byte)src, (byte)(i + 1), SwitchType.AUDIO);
                }
            ClearAvSelectBtns(ui);
        }
        private void sendDestToSelectedSrc(UiWithLocation ui, uint dest)
        {
            CrestronConsole.PrintLine("sendDestToSelectedSrc, dest:{0}", dest);
            DoSwitch(ui.SelectedAvSrc, (byte)dest, SwitchType.VIDEO);
            DoSwitch(ui.SelectedAvSrc, (byte)dest, SwitchType.AUDIO);
            ClearAvSelectBtns(ui);
        }
        private void DoSourcePress(UiWithLocation ui, uint index)
        {
            if (SourceIsRoutedToDest(index))
                if (isAnyDestSelected(ui))
                    sendSrcToSelectedDestinations(ui, index);
        }
        private void DoSourceNoneRelease(UiWithLocation ui, uint index)
        {
            if (!isAnyDestSelected(ui))
            {
                ui.AvSrcOffSelected = !ui.AvSrcOffSelected;
                if (ui.AvSrcOffSelected) // toggle this src fb off
                    ui.SelectedAvSrc = 0;
                for (int i = 1; i <= config.vidInputs.Count; i++)
                    SetAvSrcDigFb(ui, (uint)i, ui.SelectedAvSrc == i);
                //todo- feedback for none selected
            }
            else
                sendSrcToSelectedDestinations(ui, 0);
        }
        private void DoSourceRelease(UiWithLocation ui, uint index)
        {
            //CrestronConsole.PrintLine("DoSourcePress, index:{0}", index);
            if (!isAnyDestSelected(ui))
            {
                if (ui.SelectedAvSrc == index) // toggle this src fb off
                {
                    ui.SelectedAvSrc = 0;
                    ui.Device.StringInput[SER_TOP_TXT].StringValue = "Select a source or destination";
                }
                else if (ui.SelectedAvSrc > 0) // replace old src fb with this one
                {
                    ui.SelectedAvSrc = (byte)index;
                    ui.Device.StringInput[SER_TOP_TXT].StringValue = String.Format("Select a destination to display the {0}", config.vidInputs[ui.SelectedAvSrc - 1].devName);
                }
                else if (ui.AvSrcOffSelected) // replace old src fb with this one
                {
                    ui.AvSrcOffSelected = false;
                    ui.SelectedAvSrc = (byte)index;
                    ui.Device.StringInput[SER_TOP_TXT].StringValue = String.Format("Select a destination to display the {0}", config.vidInputs[ui.SelectedAvSrc - 1].devName);
                }
                else // set this src fb
                {
                    ui.SelectedAvSrc = (byte)index;
                    ui.Device.StringInput[SER_TOP_TXT].StringValue = String.Format("Select a destination to display the {0}", config.vidInputs[ui.SelectedAvSrc - 1].devName);
                }
                for (int i = 0; i < config.vidInputs.Count; i++)
                    SetAvSrcDigFb(ui, (uint)(i+1), ui.SelectedAvSrc == i+1);
                //todo- feedback for none selected
            }
            else
                sendSrcToSelectedDestinations(ui, index);
        }
        private void DoDestPress(UiWithLocation ui, uint index)
        {
            CrestronConsole.PrintLine("DoDestPress, index:{0}", index);
            if (ui.SelectedAvSrc == 0 && !ui.AvSrcOffSelected)  // toggle dest fb
            {
                ui.AvDestSelected[(ushort)(index - 1)] = !ui.AvDestSelected[(ushort)(index - 1)];
                SetAvDestDigFb(ui, index, ui.AvDestSelected[(ushort)(index - 1)]);
                if (ui.AvDestSelected[(ushort)(index - 1)] || isAnyDestSelected(ui))
                    ui.Device.StringInput[SER_TOP_TXT].StringValue = "Select a source to display on the selected destinations";
                else
                    ui.Device.StringInput[SER_TOP_TXT].StringValue = "Select a source or destination";
            }
            else
                sendDestToSelectedSrc(ui, index);
        }
        private void SetAvDestDigFb(UiWithLocation ui, uint index, bool val)
        {
            ui.Device.SmartObjects[SMART_ID_DEST_LIST].BooleanInput[String.Format("Item {0} Selected", index)].BoolValue = val;
        }
        private void SetAvSrcDigFb(UiWithLocation ui, uint index, bool val)
        {
            int firstInputOfRoom2 = config.vidInputs.FindIndex(x => x.room == (byte)2);
            //CrestronConsole.PrintLine("SetAvSrcDigFb, index:{0}, val:{1}", index, val);
            //if (index <= firstInputOfRoom2)
            //{
                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].BooleanInput[String.Format("Item {0} Selected", index)].BoolValue = val;
                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_0].BooleanInput[String.Format("Item {0} Selected", index)].BoolValue = val;
            //}
            //else
                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_1].BooleanInput[String.Format("Item {0} Selected", index)].BoolValue = val;
        }

        #endregion
        #region general functions

        public void SendDocCamCommand(Location loc, uint cmd)
        {
            LumensDocCam docCam = docCams.Find(x => x != null);
            if(docCam != null)
            {
                switch(cmd)
                {
                    case DIG_DOCCAM_POWER    : docCam.PowerToggle(); break;
                    case DIG_DOCCAM_AUTOFOCUS:
                    case DIG_DOCCAM_LAMP:
                    case DIG_DOCCAM_BACKLIGHT:
                    case DIG_DOCCAM_ZOOM_IN:
                    case DIG_DOCCAM_ZOOM_OUT:
                    case DIG_DOCCAM_FOCUS_IN:
                    case DIG_DOCCAM_FOCUS_OUT:
                    case DIG_DOCCAM_BRIGHT_UP:
                    case DIG_DOCCAM_BRIGHT_DN:
                        break;
                }
            }
        }

        public void SendEiscDig(uint sig, bool val)
        {
            CrestronConsole.PrintLine("cs SendEiscDig {0} {1}", sig, val);
            eisc.BooleanInput[sig].BoolValue = val;
        }
        public void DoAudOutFb(uint input, uint output)
        {
/*
 * try
            {
                if (input != 0) // stupid thing is reporting input 0 falsely
                {
                    byte destVidMatrix = remapVidMatrixOutputs[output];
                    byte serIncJoin = SER_INC_LAPTOP_LOCATION; // Serial Increment Join value in Subpage Reference List determines how many serials on each list item.
                    int join = 3 + (int)(output - 1) * serIncJoin; // 3 for 3rd button
                    //CrestronConsole.PrintLine("DoAudOutFb input {0}, output {1}, destVidMatrix {2} join {3}", input, output, destVidMatrix, join);
                    //int room = 1 + (destVidMatrix - 1) / digIncJoin;
                    foreach (KeyValuePair<ushort, UiWithLocation> ui in uis)
                    {
                        if (join <= serIncJoin * locations.Count())
                        {
                            ui.Device.SmartObjects[SMART_ID_LAPTOP_LOCATION].StringInput[String.Format("text-o{0}", join)].StringValue = vidSourceNames[(ushort)input];
                            ui.Device.SmartObjects[SMART_ID_PRESENTER_LOCATION].StringInput[String.Format("text-o{0}", join)].StringValue = vidSourceNames[(ushort)input];
                        }
                    }

                    serIncJoin = SER_INC_AUD_LOCATION;
                    join = 2 + (int)(output - 1) * serIncJoin; // 2 for 2nd button
                    //CrestronConsole.PrintLine("     input {0}, output {1}, destVidMatrix {2} join {3}", input, output, destVidMatrix, join);
                    foreach (KeyValuePair<ushort, UiWithLocation> ui in uis)
                    {
                        if (join <= serIncJoin * locations.Count())
                        {
                            // wps[(ushort)room].AudioSource = DmTx200Base.eSourceSelection.Auto;
                            if (vidSourceNames[(ushort)input].Contains("Wall plate"))
                            {
                                int room = Utils.atoi(vidSourceNames[(ushort)input]);
                                CrestronConsole.PrintLine("     WP ui {0}, input {1}, output {2}, destVidMatrix {3}, join {4}, room {5}", ui.Device.ID, input, output, destVidMatrix, join, room);
                                // ui.Device.SmartObjects[SMART_ID_AUD_LOCATION].StringInput[String.Format("text-o{0}", join)].StringValue = String.Format("{0} {1}", vidSourceNames[(ushort)input], wps[(byte)room].VideoSourceFeedback.ToString());
                                ui.Device.SmartObjects[SMART_ID_AUD_LOCATION].StringInput[String.Format("text-o{0}", join)].StringValue = String.Format("{0} {1}", vidSourceNames[(ushort)input], locations[(byte)room].WpAudioType);
                                //CrestronConsole.PrintLine("     locations[{0}].WpAudioType {1}", room, locations[(byte)room].WpAudioType);
                            }
                            else
                                ui.Device.SmartObjects[SMART_ID_AUD_LOCATION].StringInput[String.Format("text-o{0}", join)].StringValue = GetVidSrcName((ushort)input);
                        }
                    }
                    if (destVidMatrix <= locations.Count())
                    {
                        locations[destVidMatrix].CurrentAudSourceName = GetVidSourceNames((ushort)input];
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DoAudOutFb exception: {0}", e.ToString());
            }
*/
        }
        public String GetVidSourceName(Location loc, ushort input)
        {
            if (RoomIsJoined(loc)) //
                return String.Format("Room {0} {1}", config.vidInputs[input].room, config.vidInputs[input].devName);
            else
                return config.vidInputs[input].devName;
        }
        public void RampVol(Location loc, Direction dir)
        {
            //CrestronConsole.PrintLine("cs RampVol {0}", loc);
            loc.RampVol(dir);
            foreach (Location location in config.locations)
            {
                if (location.Owner == loc.Owner)
                {
                    location.SetVolume(loc.Volume);
                    //audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, config.locations.FindIndex(x => x == loc) + 1, loc.Volume-90);
                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFaderIndex[location.Id - 1], loc.Volume - 90);
                }
            }
            UpdateZoneVolFb(loc);
        }
        public void NudgeVol(Location loc, Direction dir)
        {
            loc.NudgeVol(dir);
            foreach (Location location in config.locations)
                if (location.Owner == loc.Owner)
                {
                    location.SetVolume(loc.Volume);
                    //audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, config.locations.FindIndex(x => x == loc) + 1, loc.Volume-90);
                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFaderIndex[location.Id-1], loc.Volume-90);
                }
            UpdateZoneVolFb(loc);
        }
        public void DoMute(Location loc, PowerStates state)
        {
            bool mute = loc.SetMute(state);
            foreach (Location location in config.locations)
            {
                if (location.Owner == loc.Owner)
                {
                    location.SetMute(mute);
                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFaderIndex[location.Id-1]);
                }
            }
            UpdateZoneMuteFb(loc);
        }
        public void SetMicLevel(Level mic)
        {
            int index = config.mics.FindIndex(x => x == mic);
            CrestronConsole.PrintLine("SetMicLevel:{0}, {1}", index, mic.level);
            foreach (UiWithLocation ui in uis)
                ui.Device.SmartObjects[SMART_ID_MICS].UShortInput[String.Format("an_fb{0}", index + 1)].UShortValue = (ushort)(mic.level * 655);
        }
        public void DoLightRamp(Location loc, Direction dir)
        {
            CrestronConsole.PrintLine("cs DoLightRamp loc {0}, Direction {1}", loc.Id, dir);
            foreach (Location location in config.locations)
            {
                if (location.Owner == loc.Owner)
                    switch (dir)
                    {
                        case Direction.UP: eisc.BooleanInput[EISC_DIG_LIGHTS_UP[location.Id - 1]].BoolValue = true; break;
                        case Direction.DOWN: eisc.BooleanInput[EISC_DIG_LIGHTS_DOWN[location.Id - 1]].BoolValue = true; break;
                        case Direction.STOP:
                            eisc.BooleanInput[EISC_DIG_LIGHTS_UP[location.Id - 1]].BoolValue = false;
                            eisc.BooleanInput[EISC_DIG_LIGHTS_DOWN[location.Id - 1]].BoolValue = false;
                            break;
                    }
            }
        }
        public void ClearPassText(Location loc)
        {
            CrestronConsole.PrintLine("     ClearPassText ui_0{0}", loc.Id);
            foreach (UiWithLocation ui in uis)
            {
                if (loc.Owner == ui.location.Owner)
                {
                    CrestronConsole.PrintLine("     clearing timer {0} text", loc.Id);
                    ui.Device.StringInput[SER_PASSWORD_TEXT].StringValue = "";
                }
            }
        }
        public void SystemDetails()
        {
            try
            {
                CrestronConsole.PrintLine("ProgramIDTag: {0}", InitialParametersClass.ProgramIDTag);
                CrestronConsole.PrintLine("ProgramDirectory: {0}", InitialParametersClass.ProgramDirectory);
                CrestronConsole.PrintLine("ApplicationDirectory: {0}", Directory.GetApplicationDirectory());
                CrestronConsole.PrintLine("ControllerPromptName: {0}", InitialParametersClass.ControllerPromptName);
                CrestronConsole.PrintLine("RuntimeEnvironment: {0}", CrestronEnvironment.RuntimeEnvironment);
                CrestronConsole.PrintLine("OSVersion: {0}", CrestronEnvironment.OSVersion.Version);
                CrestronConsole.PrintLine("Firmware: {0}", CrestronEnvironment.OSVersion.Firmware);
                CrestronConsole.PrintLine("RamFree: {0}", CrestronEnvironment.SystemInfo.RamFree);
                CrestronConsole.PrintLine("TotalRamSize: {0}", CrestronEnvironment.SystemInfo.TotalRamSize);
                /*
                if(ControllerAudioDevice == null)
                foreach (Crestron.SimplSharpProInternal.DeviceBasis dev in ControllerAudioDevice.AttachedDevices)
                    CrestronConsole.PrintLine("AttachedDevice: {0}", dev);
                CrestronConsole.PrintLine("Name: {0}", ControllerAudioDevice.SlotName);
                */
                CrestronConsole.PrintLine("ControllerFrontPanelSlotDevice: {0}", ControllerFrontPanelSlotDevice); // Slot-10
                CrestronConsole.PrintLine("NumProgramsSupported: {0}", NumProgramsSupported);
                CrestronConsole.PrintLine("ComPorts: {0}", ComPorts.Count);
                CrestronConsole.PrintLine("IROutputPorts: {0}", IROutputPorts.Count);
                CrestronConsole.PrintLine("RelayPorts: {0}", RelayPorts.Count);
                CrestronConsole.PrintLine("ControllerAudioDevice: {0}", ControllerAudioDevice); // Slot-08

                CrestronConsole.PrintLine("SupportsMicrophones: {0}", SupportsMicrophones.ToString());
                CrestronConsole.PrintLine("NumberOfMicrophones: {0}", NumberOfMicrophones);
                CrestronConsole.PrintLine("SupportsSwitcherInputs: {0}", SupportsSwitcherInputs.ToString());
                CrestronConsole.PrintLine("NumberOfSwitcherInputs: {0}", NumberOfSwitcherInputs);
                CrestronConsole.PrintLine("SupportsSwitcherOutputs: {0}", SupportsSwitcherOutputs.ToString());
                CrestronConsole.PrintLine("NumberOfSwitcherOutputs: {0}", NumberOfSwitcherOutputs);
                CrestronConsole.PrintLine("SupportsConnectItDevices: {0}", SupportsConnectItDevices);
                CrestronConsole.PrintLine("MaximumSupportedConnectItDeviceID: {0}", MaximumSupportedConnectItDeviceID);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("SystemDetails Exception: {0}", e.ToString());
            }
        }
        private void CreateRs232Display(ushort num)
        {
        /*
            display.StateChangeEvent -= DisplayStateChangeEvent;
            display.StateChangeEvent += DisplayStateChangeEvent;
            display.RxOut += RxOutEvent;
            if (!ComPorts[num].Registered)
                ComPorts[num].Register();
            var serialTransport = new SerialTransport(ComPorts[num]);
            serialTransport.SetComPortSpec(((ISerialComport)display).ComSpec);
            ((ISerialComport)display).Initialize(serialTransport);
            display.EnableLogging = true;
            SetDisplaySupports();
         */ 
        }
        
        #endregion
        #region config functions

        public bool IsDmps()
        {
            return (SystemControl != null && SystemControl.SystemControlType == eSystemControlType.Dmps3SystemControl ? true : false);
        }
        private void AudioDspOnlineEvent(object o, BoolEventArgs args)
        {
            foreach (UiWithLocation ui in uis)
            {
                ui.Device.BooleanInput[DIG_AUD_DSP_ONLINE].BoolValue = args.val;
            }
        }
        private void CreateConfig()
        {
            try
            {
                CrestronConsole.PrintLine("CreateConfig");
                //if (config == null)
                //{
                    //config = new Config("Type xx", "Auto generated room", this.ControllerPrompt, config.locations);
                    config = new Config();
                    config.locations = new List<Location>();
                    config.locations.Add(new Location(1, "Location 1"));
                    config.locations.Add(new Location(2, "Location 2"));
                //}
                StoreConfig(config, configFilePath);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("CreateConfig exception {0}", e.ToString());
            }
        }
        private void StoreConfig(object o, String filePath)
        {
            try
            {
                CrestronConsole.PrintLine("StoreConfig: {0}", filePath);
                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite))
                {
                    fs.Write(JsonConvert.SerializeObject(o, Formatting.Indented), Encoding.ASCII);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("StoreConfig exception {0}", e.ToString());
            }
        }
        private Config RecallConfig(string filePath, string fileName)
        {
            CrestronConsole.PrintLine("RecallConfig");
            Config data;
            string[] result = Directory.GetFiles(filePath, fileName);
            foreach (string str in result)
                CrestronConsole.PrintLine("{0}", str);
            try
            {
                if (result != null && result.Count() > 0)
                {
                    using (FileStream fs = new FileStream(result[0], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        StreamReader sr = new StreamReader(fs);
                        String json = sr.ReadToEnd();
                        data = JsonConvert.DeserializeObject<Config>(json);
                        //CrestronConsole.PrintLine("{0}", json);
                    }
                    return data;
                }
                else
                {
                    CrestronConsole.PrintLine("No files found at {0}/{1}", filePath, fileName);
                    return new Config();
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("RecallConfig ERROR: {0}", e);
                return new Config();
            }
        }
        private void configAudioDsp()
        {
            try
            {
                if (config.audioDsp.address != null && config.audioDsp.address.Length > 0)
                {
                    CrestronConsole.PrintLine("configAudioDsp at {0}", config.audioDsp.address);
                    audDspComms = new AudioDspComm(new IPClient(config.audioDsp.address, config.audioDsp.port, String.Format("Audio DSP")));
                }
                if (audDspComms == null)
                {
                    CrestronConsole.PrintLine("audDspComms == null");
                }
                //else
                //    audDspComms.OnlineFb += new AudioDspComm.OnlineEventHandler(AudioDspOnlineEvent);

            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("configAudioDsp ERROR: {0}", e);
            }
        }
        private void configDocCams()
        {
            CrestronConsole.PrintLine("*** configDocCams");
            try
            {
                if (config == null)
                    CrestronConsole.PrintLine("configDocCams config == null");
                else
                {
                    for (int i = 0; i < config.vidInputs.Count; i++)
                    {
                        RoomPlusDev input = config.vidInputs[i];
                        LumensDocCam docCam = null;
                        if (input == null)
                        {
                            docCam = null;
                            CrestronConsole.Print(" null");
                        }
                        else if (input.brand == "Lumens")
                        {
                            docCam = new LumensDocCam(String.Format("DocCam {0}", i));
                            CrestronConsole.PrintLine("Adding {0} DocCam to vidInputs[{1}]", input.brand, i);
                            if (DmTransmitters == null)
                                CrestronConsole.PrintLine("DmTransmitters == null");
                            else
                            {
                                CrestronConsole.PrintLine("DmTransmitters != null, [{0}], DmTransmitters.Count: {1}", i, DmTransmitters.Count);
                                if (DmTransmitters[i] == null || i >= DmTransmitters.Count)
                                    CrestronConsole.PrintLine("DmTransmitters[{0}] doesn't exist", i);
                                else
                                {
                                    CrestronConsole.PrintLine("DmTransmitters[{0}] != null, registered:{1}, online:{2}", i, DmTransmitters[i].Registered, DmTransmitters[i].IsOnline);
                                    if (DmTransmitters[i].Registered)
                                    { 
                                        CrestronConsole.PrintLine("adding tx[{0}].ComPorts[1] to 0x{1:X2} {2}", i, DmTransmitters[i].ID, input.brand);
                                        SerialPort comms = new SerialPort(DmTransmitters[i].ComPorts[1], String.Format("{0} DocCam {1}", input.brand, i));
                                        docCam.SetComms(comms);
                                    }
                                    else
                                        CrestronConsole.PrintLine("DmTransmitters[{0}]: 0x{0:X2}, not registered", i, DmTransmitters[i].ID);
                                }
                            }
                        }
                        docCams.Add(docCam);
                    }
               }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("configDocCams ERROR: {0}", e);
            }
        }
        private void configDisplays()
        {
            CrestronConsole.PrintLine("*** configDisplays");
            //Thread.Sleep(1000);
            try
            {
                if(config == null)
                    CrestronConsole.PrintLine("configDisplays config == null");
                else
                {
                    for(int i = 0; i < config.vidOutputs.Count; i++)
                    {
                        RoomPlusDev output = config.vidOutputs[i];
                        //CrestronConsole.PrintLine("  output {0}", i);
                        Display display;
                        if (output == null)
                        {
                            display = null;
                            CrestronConsole.Print(" null");
                        }
                        else if (output.brand == "Panasonic")
                        {
                            display = new PanasonicProj(String.Format("Panasonic Projector {0}", i));
                            CrestronConsole.PrintLine("Adding {0} projector at {1} to output {2}", output.brand, output.address, i);
                            IPClient comms = new IPClient(output.address, 1024, String.Format("{0} Projector {1}", output.brand, i));
                            display.SetComms(comms); // Panasonic port 1024
                        }
                        else if (output.brand == "Epson")
                        {
                            display = new EpsonProj(String.Format("Epson Projector {0}", i));
                            CrestronConsole.PrintLine("Adding {0} projector to output {1}", output.brand, i);
                            if (DmReceivers == null)
                                CrestronConsole.PrintLine("DmReceivers == null");
                            else
                            {
                                CrestronConsole.PrintLine("DmReceivers != null, [{0}], DmReceivers.Count: {1}", i, DmReceivers.Count);
                                if (DmReceivers[i] == null || i >= DmReceivers.Count)
                                    CrestronConsole.PrintLine("DmReceivers[{0}] doesn't exist", i);
                                else
                                {
                                    CrestronConsole.PrintLine("DmReceivers[{0}] != null, registered:{1}, online:{2}", i, DmReceivers[i].Registered, DmReceivers[i].IsOnline);
                                    //EndpointReceiverBase rx = (DmRmc4k100C)DmReceivers[i];
                                    DmRmc4k100C rx = (DmRmc4k100C)DmReceivers[i];
                                    if (rx.Registered)
                                    {
                                        CrestronConsole.PrintLine("adding rx[{0}].ComPorts[1] to 0x{1:X2} {2}", i, rx.ID, output.brand);
                                        SerialPort comms = new SerialPort(rx.ComPorts[1], String.Format("{0} Projector {1}", output.brand, i));
                                        display.SetComms(comms); // Panasonic port 1024
                                    }
                                    else
                                        CrestronConsole.PrintLine("DmReceivers[{0}]: 0x{0:X2}, not registered", i, rx.ID);
                                }
                            }
                        }
                        else
                        {
                            display = new Display(String.Format("{0} {1}", output.brand, i));
                        }
                        displays.Add(display);
                    }
                }
                //displays.Add()
                //displays = new Dictionary<ushort, Display>();
                //display = new EpsonProjComport();
                //CreateRs232Display(ComPortProj);
                CrestronConsole.PrintLine("* configDisplays done");
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("configDisplays ERROR: {0}", e);
            }
        }
        private void ConfigSwitcher()
        {
            try
            {
                CrestronConsole.PrintLine("*** ConfigSwitcher");
                if (IsDmps()) // this is a placeholder for when there is a DMPS so the switch is part of the controller
                {
                    CrestronConsole.PrintLine(" type DMPS");
                    vidMatrix = new DmpsSwitch(InitialParametersClass.ControllerPromptName, this); // 
                    DmpsInputInit();
                    DmpsOutputInit();
                    DmEndPointsInit();
                    RegisterVidMatrixEventHandlers();
                    //if (SupportsMicrophones)
                    //    MicrophoneChange += new MicrophoneChangeEventHandler(MicrophoneChange);
                }
                else if (InitialParametersClass.ControllerPromptName.Contains("RMC3")) // has a Hd-Md4x1-4k-e
                {
                    CrestronConsole.PrintLine(" type HdMd4x14kE");
                    vidMatrix = new HdMd4x14kE(IPID_VIDMATRIX, config.vidSwitcherIpAddress, this);
                    RegisterVidMatrix();
                    RegisterVidMatrixEventHandlers();
                }
                else// if (InitialParametersClass.ControllerPromptName.Contains("CP3"))
                {
                    CrestronConsole.PrintLine(" type DM");
                    if (config.vidInputs.Count() > 8 || config.vidOutputs.Count() > 8)
                        vidMatrix = new DmMd16x16(IPID_VIDMATRIX, this);
                    else
                        vidMatrix = new DmMd8x8(IPID_VIDMATRIX, this);
                    DmInputInit();
                    DmOutputInit();
                    CrestronConsole.Print(".");
                    RegisterVidMatrix();
                    CrestronConsole.Print(".");
                    RegisterVidMatrixEventHandlers();
                    CrestronConsole.Print(".");
                    if (vidMatrix.Registered)
                        DmEndPointsInit();
                    else
                        CrestronConsole.PrintLine("Registering endpoints FAILED, vidMatrix not registered");
                    CrestronConsole.Print(".");
                }
                CrestronConsole.PrintLine("vidMatrix is SwitchType: {0}", vidMatrix.SwitchType);
                CrestronConsole.PrintLine("* ConfigSwitcher done");
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("ConfigSwitcher exception: {0}", e.ToString());
            }
        }
        private void RegisterVidMatrix()
        {
            CrestronConsole.PrintLine("RegisterVidMatrix {0} at {1}", vidMatrix.SwitchType, config.vidSwitcherIpAddress);
            if (vidMatrix.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    CrestronConsole.PrintLine("vidMatrix failed registration. Cause: {0}", vidMatrix.RegistrationFailureReason);
            else
                vidMatrix.OnlineStatusChange += new OnlineStatusChangeEventHandler(CommonOnlineHandler);
        }
        private void RegisterVidMatrixEventHandlers()
        {
            try
            {
                CrestronConsole.PrintLine("RegisterVidMatrixEventHandlers");
                if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count > 0)
                {
                    vidMatrix.DMInputChange += new DMInputEventHandler(vidMatrixInputChange);
                    vidMatrix.DMOutputChange += new DMOutputEventHandler(vidMatrixOutputChange);
                    vidMatrix.DMSystemChange += new DMSystemEventHandler(vidMatrixSystemChange);
                }
                if (SystemControl != null) // DMPS
                {
                    CrestronConsole.PrintLine("DMInputEventHandler");
                    DMInputChange += new DMInputEventHandler(vidMatrixInputChange);
                    CrestronConsole.PrintLine("DMOutputEventHandler");
                    DMOutputChange += new DMOutputEventHandler(vidMatrixOutputChange);
                    CrestronConsole.PrintLine("DMSystemEventHandler");
                    DMSystemChange += new DMSystemEventHandler(vidMatrixSystemChange);
                    CrestronConsole.PrintLine("done");
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("RegisterVidMatrixEventHandlers exception: {0}", e.ToString());
            }
        }
        private void DmInputInit()
        {
            try
            {
                List<CardDevice> inputCards = new List<CardDevice>();
                DmTransmitters = new List<DmHDBasedTEndPoint>();
                int maxInputs = config.vidInputs.Count < vidMatrix.Inputs.Count ? config.vidInputs.Count : vidMatrix.Inputs.Count;
                for (byte b = 0; b < maxInputs; b++)
                {
                    DmHDBasedTEndPoint tx = null;
                    if (config.vidInputs[b] != null)
                    {
                        inputCards.Add(new Dmc4kC((uint)b + 1, vidMatrix));
                        vidMatrix.Inputs[(uint)(b + 1)].Name.StringValue = config.vidInputs[b].devName;
                        if (config.vidInputs[b].devType == (ushort)VidDev.DOCCAM)
                            tx = new DmTx4K100C1G((uint)(IPID_DMTX_BASE + b), vidMatrix.Inputs[(uint)b + 1]);
                    }
                    DmTransmitters.Add(tx);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DmInputInit exception: {0}\n{1}", e.Message, e.ToString());
            }
        }
        private void DmpsInputInit()
        {
            CrestronConsole.PrintLine("DmpsInputInit");
            DmTransmitters = new List<DmHDBasedTEndPoint>();
            for (uint i = 1; i <= NumberOfSwitcherInputs; i++)
            {
                DmHDBasedTEndPoint tx = null;
                ICardInputOutputType input = SwitcherInputs[i];
                switch (input.CardInputOutputType)
                {
                    case eCardInputOutputType.Dmps3HdmiInput:
                    case (eCardInputOutputType.Dmps3HdmiInputWithoutAnalogAudio):
                        Card.Dmps3HdmiInputWithoutAnalogAudio Dmps3HdmiInput = (Card.Dmps3HdmiInputWithoutAnalogAudio)input;
                        Dmps3HdmiInput.HdmiInputPort.StreamCec.CecChange += new CecChangeEventHandler(Dmps3HdmiInputStreamCec_CecChange);
                        break;
                    case eCardInputOutputType.Dmps3HdmiVgaInput:
                        Card.Dmps3HdmiVgaInput Dmps3HdmiVgaInput = (Card.Dmps3HdmiVgaInput)input;
                        Dmps3HdmiVgaInput.HdmiInputPort.StreamCec.CecChange += new CecChangeEventHandler(Dmps3HdmiInputStreamCec_CecChange);
                        Dmps3HdmiVgaInput.VgaInputPort.VideoAttributes.AttributeChange += new GenericEventHandler(VgaDviInputPortVideoAttributesBasic_AttributeChange);
                        Dmps3HdmiVgaInput.VgaInputPort.VideoControls.ControlChange += new GenericEventHandler(VgaDviInputPortVideoControlsBasic_ControlChange);
                        break;
                    case eCardInputOutputType.Dmps3HdmiVgaBncInput:
                        Card.Dmps3HdmiVgaBncInput Dmps3HdmiVgaBncInput = (Card.Dmps3HdmiVgaBncInput)input;
                        Dmps3HdmiVgaBncInput.HdmiInputPort.StreamCec.CecChange += new CecChangeEventHandler(Dmps3HdmiInputStreamCec_CecChange);
                        Dmps3HdmiVgaBncInput.VgaInputPort.VideoAttributes.AttributeChange += new GenericEventHandler(VgaDviInputPortVideoAttributesBasic_AttributeChange);
                        Dmps3HdmiVgaBncInput.VgaInputPort.VideoControls.ControlChange += new GenericEventHandler(VgaDviInputPortVideoControlsBasic_ControlChange);
                        Dmps3HdmiVgaBncInput.BncInputPort.VideoAttributes.AttributeChange += new GenericEventHandler(BncInputPortVideoAttributes_AttributeChange);
                        Dmps3HdmiVgaBncInput.BncInputPort.VideoControls.ControlChange += new GenericEventHandler(BncInputPortVideoControls_ControlChange);
                        break;
                    case eCardInputOutputType.Dmps3DmInput:
                        Card.Dmps3DmInput Dmps3DmInput = (Card.Dmps3DmInput)input;
                        Dmps3DmInput.DmInputPort.VideoAttributes.AttributeChange += new GenericEventHandler(DmInputPortVideoAttributes_AttributeChange);
                        tx = new DmTx4K100C1G((uint)IPID_DMTX_BASE + i - 1, (DMInput)SwitcherInputs[i]);
                        CrestronConsole.PrintLine("Dmps3DmInput {0} DmTx init id 0x{1:X2}", i, tx.ID);
                        break;

                    default:
                        CrestronConsole.PrintLine("SwitcherInput[{0}] is unknown type {1} [{2}]", i, input.CardInputOutputType.ToString(), input.ToString());
                        break;

                }
                DmTransmitters.Add(tx);
            }
        }
        private void DmOutputInit()
        {
            List<DmOutputModuleBase> outputCards = new List<DmOutputModuleBase>();
            DmReceivers = new List<EndpointReceiverBase>();
            CrestronConsole.PrintLine("\nvidMatrix.Outputs.count {0}", vidMatrix.Inputs.Count);
            for (byte b = 0; b < vidMatrix.Outputs.Count / 2; b++) // output cards (2 slots each)
            {
                DmOutputModuleBase outputCard = new Dmc4kCoHdSingle((uint)b + 1, vidMatrix); // 3:HD,5:DM,8:DM4K
                outputCards.Add(outputCard);
            }
            for (byte b = 0; b < vidMatrix.Outputs.Count; b++) // output slots
            {
                EndpointReceiverBase rx = null;
                if (config.vidOutputs[b] != null)
                {
                    vidMatrix.Outputs[(uint)(b + 1)].Name.StringValue = config.vidOutputs[b].devName;
                    if (config.vidOutputs[b].brand == "Epson")
                        rx = new DmRmc4k100C((uint)IPID_DMRX_BASE + b, vidMatrix.Outputs[(uint)b + 1]);
                }
                DmReceivers.Add(rx);
            }
        }
        private void DmpsOutputInit()
        {
            CrestronConsole.PrintLine("DmpsOutputInit");
            try
            {
                DmReceivers = new List<EndpointReceiverBase>();
                for (uint i = 1; i <= NumberOfSwitcherOutputs; i++)
                {
                    DmRmc4k100C rx = null;
                    ICardInputOutputType output = SwitcherOutputs[i];
                    switch (output.CardInputOutputType)
                    {
                        case eCardInputOutputType.Dmps3HdmiOutput:
                            Card.Dmps3HdmiOutput Dmps3HdmiOutput = (Card.Dmps3HdmiOutput)output;
                            Dmps3HdmiOutput.HdmiOutputPort.StreamCec.CecChange += new CecChangeEventHandler(Dmps3HdmiOutputStreamCec_CecChange);
                            Dmps3HdmiOutput.HdmiOutputPort.ConnectedDevice.DeviceInformationChange += new ConnectedDeviceChangeEventHandler(Dmps3HdmiOutputConnectedDevice_DeviceInformationChange);
                            break;
                        case eCardInputOutputType.Dmps3DmOutput:
                            Card.Dmps3DmOutput Dmps3DmOutput = (Card.Dmps3DmOutput)output;
                            rx = new DmRmc4k100C((uint)IPID_DMRX_BASE + i - 1, (DMOutput)SwitcherOutputs[i]);
                            Dmps3DmOutput.DmOutputPort.ConnectedDevice.DeviceInformationChange += new ConnectedDeviceChangeEventHandler(Dmps3DmOutputConnectedDevice_DeviceInformationChange);
                            CrestronConsole.PrintLine("Dmps3DmOutput {0} DmRx init id 0x{1:X2}", i, rx.ID);
                            break;
                        case eCardInputOutputType.Dmps3ProgramOutput:
                            Card.Dmps3ProgramOutput Dmps3ProgramOutput = (Card.Dmps3ProgramOutput)output;
                            break;
                        case eCardInputOutputType.Dmps3Aux1Output:
                            Card.Dmps3Aux1Output Dmps3Aux1Output = (Card.Dmps3Aux1Output)output;
                            break;
                        case eCardInputOutputType.Dmps3Aux2Output:
                            Card.Dmps3Aux2Output Dmps3Aux2Output = (Card.Dmps3Aux2Output)output;
                            break;
                        case eCardInputOutputType.Dmps3CodecOutput:
                            Card.Dmps3CodecOutput Dmps3CodecOutput = (Card.Dmps3CodecOutput)output;
                            break;
                        default:
                            CrestronConsole.PrintLine("SwitcherOutput[{0}] is unknown type {1} [{2}]", i, output.CardInputOutputType.ToString(), output.ToString());
                            break;
                    }
                    DmReceivers.Add(rx);
                }
                CrestronConsole.PrintLine("DmReceivers {0}", DmReceivers.Count);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DmpsOutputInit ERROR: {0}", e);
            }
        }
        private void DmEndPointsInit()
        {
            try
            {
                CrestronConsole.PrintLine("DmEndPointsInit");
                if (DmReceivers != null)
                {
                    CrestronConsole.PrintLine("Registering receivers");
                    for(int i = 0; i < DmReceivers.Count; i++)
                    //foreach (EndpointReceiverBase rx in DmReceivers)
                    {
                        EndpointReceiverBase rx = DmReceivers[i];
                        if (rx != null)
                        {
                            if (rx.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                                CrestronConsole.PrintLine("Could not register rx[{0}] 0x{1:X2} for {2}: {3}", i, rx.ID, config.vidOutputs[i].devName, rx.RegistrationFailureReason);
                            else
                                CrestronConsole.PrintLine("registered rx[{0}] 0x{1:X2} for {2}", i, rx.ID, config.vidOutputs[i].devName);
                            rx.OnlineStatusChange += CommonOnlineHandler;
                            rx.BaseEvent += new BaseEventHandler(dmRxBaseEventHandler);
                        }
                    }
                }
                if (DmTransmitters != null)
                {
                    CrestronConsole.PrintLine("registering transmitters ({0})", DmTransmitters.Count);
                    for (int i = 0; i < DmTransmitters.Count; i++)
                    {
                        //CrestronConsole.Print(",{0}", i);
                        DmTx4K100C1G tx = (DmTx4K100C1G)DmTransmitters[i];
                        //CrestronConsole.Print(" {0} null", i, tx != null ? "not" : "");
                        if (tx != null)
                        {
                            if (tx.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                                CrestronConsole.PrintLine("Could not register tx[{0}] 0x{1:X2} for {2}: {2}", i, tx.ID, config.vidInputs[i].devName, tx.RegistrationFailureReason);
                            else
                                CrestronConsole.PrintLine("registered tx[{0}] 0x{1:X2} for {2}", i, tx.ID, config.vidInputs[i].devName);
                            tx.OnlineStatusChange += CommonOnlineHandler;
                            tx.BaseEvent += new BaseEventHandler(dmTxBaseEventHandler);
                            //tx.HdmiInput.InputStreamChange += dmTxHdmiInputStreamChangeEventHandler;
                            //tx.HdmiInput.VideoAttributes.AttributeChange += dmTxHdmiAttributeChangeEventHandler;
                            //tx.VgaInput.InputStreamChange += dmTxVgaInputStreamChangeEventHandler;
                            //tx.VgaInput.VideoAttributes.AttributeChange += dmTxVgaAttributeChangeEventHandler;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DmEndPointsInit ERROR: {0}", e);
            }
        }

        #endregion
    }
}
