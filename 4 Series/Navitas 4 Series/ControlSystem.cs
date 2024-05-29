// 20210413 RRD
// Recompiled the 3 series Navitas program (20180305) for 4 series
// Hide all mute buttons except for lectern.

using Crestron.SimplSharp;                          // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.Scheduler;
using Crestron.SimplSharpPro;                       // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;        // For Threading
using Crestron.SimplSharpPro.DeviceSupport;         // For Generic Device Support
using Crestron.SimplSharpPro.DM;                    // DM
using Crestron.SimplSharpPro.DM.Cards;
using Crestron.SimplSharpPro.DM.Endpoints;
using Crestron.SimplSharpPro.DM.Endpoints.Receivers;
using Crestron.SimplSharpPro.DM.Endpoints.Transmitters;
using Crestron.SimplSharpPro.EthernetCommunication; // EISC
using Crestron.SimplSharpPro.Keypads;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Switch = Crestron.SimplSharpPro.DM.Switch;

namespace Navitas
{
	//public delegate void OnlineEventHandler(object sender, BoolEventArgs e);
	//public delegate void StringEventHandler(object sender, StringEventArgs e);

	public class ControlSystem : CrestronControlSystem
    {
        private ushort debug = 0;
        private bool enableRemap = false;  
      
        #region constants
        // IPIDS
        private const byte IPID_UIS_BASE = 0x03;
        private const ushort IPID_EISC = 0x0A;
        private const ushort IPID_VIDMATRIX = 0x30; // 0x30 = DM Matrix
        private const byte IPID_DMTX_BASE = 0x31; // 0x31,0x32 = DmTx
        private const byte IPID_DMRX_BASE = 0x41; // 0x41,0x42 = DmRx
        private const byte CRESNET_KEYPAD_BASE = 0x21; // 0x21,0x22
        private string configFilePath = String.Empty;
        private string configFileName = String.Empty;
        private Config config;
        private int IP_PORT_LIGHTS = 10001;

        public ThreeSeriesTcpIpEthernetIntersystemCommunications eisc;
        public Switch vidMatrix;
        public List<DmHDBasedTEndPoint> DmTransmitters;
        public List<EndpointReceiverBase> DmReceivers;
        public List<CardDevice> vidMatrixInputCards;
        public List<DmOutputModuleBase> vidMatrixOutputCards;
        public List<UiWithLocation> uis;
        public List<CnxB2> kps;

        private CTimer CommonTimer;
        private CTimer powerTimer;
        private long powerTimerInterval = 50;
        private int powerBar;

        private const byte LIGHT_PRESET_FULL = 1;
        private const byte LIGHT_PRESET_LONG_THROW = 2;
        private const byte LIGHT_PRESET_SHORT_THROW = 3;
        private const byte LIGHT_PRESET_OFF = 4;
        private const byte LIGHT_PRESET_TWO_DISPLAYS = 5;
        private const byte LIGHT_PRESET_GRACE = 8;
        private const byte LIGHT_PRESET_VACANT = 12;

        private const byte PRESET_COMBINE_NONE       = 0;
        private const byte PRESET_COMBINE_1_2_OFF    = 0; // 12.03_05, 2.18_19 uncombine
        private const byte PRESET_COMBINE_1_2_ON     = 1; // 12.03_05, 2.18_19 combine
        private const byte PRESET_COMBINE_ALL_ZONE_1 = 2;
        private const byte PRESET_COMBINE_3_4_OFF    = 3; // 12.06_08, 2.20_21 uncombine
        private const byte PRESET_COMBINE_3_4_ON     = 4; // 12.06_08, 2.20_21 combine
        private const byte PRESET_COMBINE_ALL_ZONE_2 = 5;

        private ushort[] EISC_ANA_VOL = { 1, 2 };
        private ushort[] EISC_ANA_MIC = { 11, 12 };
        private ushort[] EISC_ANA_SRC = { 21, 22 };
        private const ushort EISC_ANA_MIC_INPUT = 31;

        private const ushort EISC_DIG_SYS_JOIN = 200;
        
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
        const ushort SMART_ID_PROJ_POWER = 10;
        const ushort SMART_ID_PROJ_IMAGE_MUTE = 11;

        const byte DIG_INC_MICS = 4;   //digIncJoin for SMART_ID_MICS, number of digitals in the subpage reference
        const byte DIG_IDX_MIC_MUTE = 3;
        const byte DIG_IDX_MIC_MUTE_VIS = 4;
        const byte DIG_SG_MICS_MAX = 5; // number of subpage references

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
        const ushort DIG_PICFREEZE = 16;

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
        const ushort DIG_JOIN_ALL = 41;
        const ushort DIG_HAS_JOIN_ALL = 42;

        const ushort DIG_MIC_VOL_UP = 74;
        const ushort DIG_MIC_VOL_DN = 75;
        const ushort DIG_MIC_VOL_MUTE = 76;

        const ushort DIG_LIGHTS_UP = 81;
        const ushort DIG_LIGHTS_DOWN = 82;
        const ushort DIG_LIGHTS_PRESET_1 = 83;
        const ushort DIG_LIGHTS_PRESET_2 = 84;
        const ushort DIG_LIGHTS_PRESET_3 = 85;

        const ushort DIG_DOCCAM_ZOOM_STOP = 89;
        const ushort DIG_DOCCAM_FOCUS_STOP = 90;
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

        const ushort DIG_CAM_POWER = 101;
        const ushort DIG_CAM_CANVAS = 102;
        const ushort DIG_CAM_ZOOM_IN = 105;
        const ushort DIG_CAM_ZOOM_OUT = 106;
        const ushort DIG_CAM_ZOOM_STOP = 107;

        const ushort DIG_RECORD_PAUSE = 111;
        const ushort DIG_RECORD_EXTEND = 112;

        const ushort DIG_PROJ_PICFREEZE = 121;

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
        const ushort ANA_CAM_X = 11;
        const ushort ANA_CAM_Y = 12;
        // serials
        const ushort SER_PASSWORD_TEXT = 1;
        const ushort SER_LOCATION_NAME = 2;
        const ushort SER_CUR_SEL_TITLE = 3;
        const ushort SER_CUR_SEL_BODY = 4;
        const ushort SER_CUR_SEL_TEXT = 5;
        const ushort SER_MESSAGES = 6;
        const ushort SER_TOP_TXT = 7;
        private ushort[] SER_LOCATION_NAMES = { 8, 9 };
        const ushort SER_CUR_DEVICE_TXT = 10;
        const ushort SER_RECORD_DURATION = 11;
        const ushort SER_RECORD_TITLE = 12;
        const ushort SER_RECORD_PRESENTER = 13;
        const ushort SER_RECORD_REMAINING = 14;
        const ushort SER_RECORD_STATUS = 15;
        const ushort SER_JOIN_STATUS_LABEL = 16;

        Dictionary<uint, string> _dmSystemEventIdsMapping;
        Dictionary<uint, string> _dmInputEventIdsMapping;
        Dictionary<uint, string> _dmOutputEventIdsMapping;
        Dictionary<uint, string> _connectedDeviceEventIdsMapping;
        Dictionary<uint, string> _cecEventIdsMapping;
        Dictionary<uint, string> _videoAttributeIdsMapping;
        Dictionary<uint, string> _videoControlsIdsMapping;
        Dictionary<uint, string> _microphoneEventIdsMapping;
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
            20, // CAM_2
            49, // UNUSED
            46  // WiP
        };

        ushort[] eIconsLgToKey_Outputs =
        {
            102, // Blank
            31, // VidDev.PROJ_1 = 1, IconsLg[50] = "Document Camera Alt"
            31, // PROJ_2
            31, // PROJ_3
            5,  // REC_1
            5,  // REC_2
            14, // AUDIO
            3,  // LCD
            65  // VC
        };
        enum VidDev
        {
            DOCCAM = 1,
            PC = 2,
            LAPTOP = 3,
            CAM_1 = 4,
            CAM_2 = 5,
            UNUSED = 6,
            WiP = 7,

            PROJ_1 = 1,
            PROJ_2 = 2,
            PROJ_3 = 3,
            REC_1 = 4,
            REC_2 = 5,
            AUDIO = 6,
            LCD = 7,
            VC = 8,
        };
        #endregion
        #region variables
        private ushort confirmState; // what are you confirming in the confirm sub
        public List<ushort> destListRemapButtons = new List<ushort>();
        public byte[] remapVidMatrixOutputs = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
        private byte[] progAudioFadersStereoSingleRoomBeforeMics = { 1, 2 };
        private byte[] progAudioFadersStereoSingleRoomAfterMics = { 4, 5 };
        private byte[] progAudioFadersStereoSingleRoomAfter2Mics = { 3, 4 };
        private byte[] progAudioFadersMonoSingleRoomBeforeMics = { 1 };
        private byte[] progAudioFadersStereoDualRoomBeforeMics = { 1, 2, 3, 4 };
        private byte[] progAudioFadersStereoDualRoomAfterMics = { 3, 4, 5, 6 };
        private byte[] micFadersBeforeMics = { 1, 2 };
        private byte[] micFadersSingleRoomAfterMonoProgram = { 2, 3 };
        private byte[] micFadersSingleRoomAfterStereoProgram = { 3, 4 };
        private byte[] micFadersDualRoomAfterStereoProgram = { 5, 6 };
        private byte[] micFaderDualRoomBeforeAndAfterMonoProgram = { 1, 2, 3, 6, 7, 8 };
        private StringBuilder keypadString = new StringBuilder();
        public String PasswordBackDoor = "0428989608";
        public String RecordUser = "navitaspro@gmail.com";
        public String RecordPassword = "navitaspro1";
        public bool recordPaused;
        private bool clearSrcBtnsOnSwitch = true;
        private bool clearDestBtnsOnSwitch = true;
        private bool[] enableConsultSource = { false, false, false, false };
        private bool[] enableConsultDest = { false, false, false, false };
        private List<LevelRamp> mics = new List<LevelRamp>();
        private byte[] FontSizeLine = { 14, 12 };
        private List<Display> displays = new List<Display>();
        private List<PanasonicCam> cams = new List<PanasonicCam>();
        private List<LumensDocCam> docCams = new List<LumensDocCam>();
        private List<Echo360> recording = new List<Echo360>();
        private List<IPClient> ipClients = new List<IPClient>();
        private AudioDspComm audDspComms;
        private ScheduledEvent endOfDayEvent;
        private Dynalite lights;
        private DynaliteIPServerToSerialPortRelay LightsRelayServer;
        //private IPServerToSerialPortRelay LightsRelayServer;
        private ushort joinState = 0;

        #endregion
        #region constructor
        public ControlSystem()
            : base()
        {
            CrestronConsole.PrintLine("Program {0} starting", InitialParametersClass.ApplicationNumber);
            //Thread.Sleep(10000); // to give time to catch with debugger
            Thread.MaxNumberOfUserThreads = 100;
            CrestronConsole.PrintLine("Program {0} waking from sleep", InitialParametersClass.ApplicationNumber);
            CreateMappingDictionaries();
            if(debug > 0)
                SystemDetails();

            string nvramFolder = "nvram"; // 4 series
            if (CrestronEnvironment.ProgramCompatibility == eCrestronSeries.Series3)
                nvramFolder = "Nvram"; // 3 series
            var dirSeparator = System.IO.Path.DirectorySeparatorChar;
            configFilePath = Directory.GetApplicationRootDirectory()
                + dirSeparator + nvramFolder
                + dirSeparator + "Navitas";//InitialParametersClass.ProgramIDTag;
            if (Directory.Exists(configFilePath))
                CrestronConsole.PrintLine("{0} found", configFilePath);
            config = RecallConfig(configFilePath, "* System config.json");
            if(config == null)
                CrestronConsole.PrintLine("RecallConfig done, null returned");
            else
                CrestronConsole.PrintLine("RecallConfig done, name: {0}", config.name);

            if (config.locations == null)
                CrestronConsole.PrintLine("locations == null");
            //Subscribe to the controller events (System, Program, and Etherent)
            CrestronConsole.PrintLine("Subscribing to controller events");
            CrestronEnvironment.SystemEventHandler += new SystemEventHandler(cs_ControllerSystemEventHandler);
            CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(cs_ControllerProgramEventHandler);
            CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(cs_ControllerEthernetEventHandler);

            CrestronConsole.AddNewConsoleCommand(ReconnectIp, "RECONNECT_IP", "Restarts IP clients", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(RecallConfig, "RECALL_CONFIG", "Re-load the config file", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(QueryEcho, "QUERY_ECHO", "Query Echo", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(SendBssString, "SEND_BSS", "SEND_BSS \x8C\x00\x00\x00\x01", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(RemapDestList, "REMAP", "", ConsoleAccessLevelEnum.AccessOperator);
            CrestronConsole.AddNewConsoleCommand(DmpsOutputInit, "DMPS_OUTPUT_INIT", "", ConsoleAccessLevelEnum.AccessOperator); 
            
            ConfigSwitcher();

            CrestronConsole.PrintLine("adding {0} mics", config.mics.Count);
            foreach (Level mic in config.mics)
                mics.Add(new LevelRamp(this, mic));

            CrestronConsole.PrintLine("Creating {0} relay ports", RelayPorts.Count);
            foreach (Relay device in RelayPorts)
            {
                device.StateChange += new RelayEventHandler(RelayChangeHandler);
                device.Register();
            }

            if (VersiPorts != null)
            {
                CrestronConsole.PrintLine("Creating {0} VersiPorts", VersiPorts.Count);
                foreach (Versiport device in VersiPorts)
                {
                    device.VersiportChange += new VersiportEventHandler(VersiportChangeHandler);
                    device.Register();
                    device.SetVersiportConfiguration(eVersiportConfiguration.DigitalInput);
                }
            }
            if (DigitalInputPorts != null)
            {
                CrestronConsole.PrintLine("Creating {0} DigitalInput ports", DigitalInputPorts.Count);
                foreach (DigitalInput device in DigitalInputPorts)
                {
                    device.StateChange += new DigitalInputEventHandler(DigitalInputChangeHandler);
                    device.Register();
                }
            }
            configUis();
            configEisc();
            configKeypads();
        }
        public override void InitializeSystem()
        {
            CrestronConsole.PrintLine("InitializeSystem");
            configDisplays();
            configAudioDsp();
            configDocCams();
            configCams();
            configLights();
            configSchedules();
            configRecording();
        }
        private void DisposeObjects()
        {
            try
            {
                //CrestronConsole.PrintLine("cs: Dispose starting");
                if (audDspComms != null)
                    audDspComms.Dispose();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose audDspComms Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose dsp done");
            try
            {
                foreach (PanasonicCam dev in cams)
                {
                    if (dev != null)
                        dev.Dispose();
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose cam Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose cams done");
            try
            {
                foreach (Echo360 dev in recording)
                {
                    if (dev != null)
                        dev.Dispose();
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose recording Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose recording done");
            try
            {
                foreach (DmHDBasedTEndPoint tx in DmTransmitters)
                    if(tx != null)
                        tx.Dispose();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose tx Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose tx done");
            try
            {
                if (CommonTimer != null)
                {
                    CommonTimer.Stop();
                    CommonTimer.Dispose();
                }
                if (powerTimer != null)
                {
                    powerTimer.Stop();
                    powerTimer.Dispose();
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose timers Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose timers done");
            try
            {
                foreach (EndpointReceiverBase rx in DmReceivers)
                    if(rx != null)
                        rx.Dispose();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose rx Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose rx done");
            try
            {
                if (vidMatrix.Outputs != null)
                    vidMatrix.Dispose();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose vidSwitch Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose vidSwitch done");
            try
            {
                foreach (Display dev in displays)
                {
                    if (dev != null)
                        dev.Dispose();
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose displays Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose displays done");
            try
            {
                foreach (UiWithLocation ui in uis)
                    ui.Dispose();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Dispose uis Exception {0}", e.Message);
            }
            CrestronConsole.PrintLine("cs: Dispose uis done");
            CrestronConsole.PrintLine("cs: Dispose done");
        }

        #endregion
        #region event handlers

        private void cs_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case (eProgramStatusEventType.Paused):
                    CrestronConsole.PrintLine("eSystemEventType.Paused");
                    break;
                case (eProgramStatusEventType.Resumed):
                    CrestronConsole.PrintLine("eSystemEventType.Resumed");
                    break;
                case (eProgramStatusEventType.Stopping):
                    CrestronConsole.PrintLine("eSystemEventType.Stopping");
                    DisposeObjects();
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
                    CrestronConsole.PrintLine("eSystemEventType.Rebooting");
                    DisposeObjects();
                    //The system is rebooting. 
                    //Very limited time to perform clean up and save any settings to disk.
                    break;
            }
        }
        private void cs_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {//Determine the event type Link Up or Link Down
                case (eEthernetEventType.LinkDown):
                    //Next need to determine which adapter the event is for. 
                    //LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        foreach (Display display in displays)
                        {
                            if(display != null && display.IpComms != null)
                                display.IpComms.HandleLinkLoss();
                        }
                        if (audDspComms != null && audDspComms.comms != null)
                            audDspComms.comms.HandleLinkLoss();
                        if (lights != null && lights.IpComms != null)
                            lights.IpComms.HandleLinkLoss();
                    }
                    break;
                case (eEthernetEventType.LinkUp):
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                        foreach (Display display in displays)
                        {
                            if(display != null && display.IpComms != null)
                                display.IpComms.HandleLinkUp();
                        }
                        if (audDspComms != null && audDspComms.comms != null)
                            audDspComms.comms.HandleLinkUp();
                        if (lights != null && lights.IpComms != null)
                            lights.IpComms.HandleLinkUp();
                    }
                    break;
            }
        }
        private void EiscSigChangeHandler(GenericBase device, SigEventArgs args)
        {
            CrestronConsole.PrintLine("EiscSigChangeHandler, {0} args {1}", device.ToString(), args.Event);
            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    CrestronConsole.PrintLine("bool {0}: {1}", args.Sig.Number, args.Sig.BoolValue);
                    switch (args.Sig.Number)
                    {
                        //case EISC_??: doStuff(args.Sig.BoolValue); break;
                        case EISC_DIG_SYS_JOIN:
                            if (args.Sig.BoolValue) // join
                                JoinRoomsAll(config.locations[0], config.locations[1]);
                            else // unjoin if joined
                            {
                                CrestronConsole.PrintLine("joinstate {0}", joinState);
                                if (joinState == 4)
                                {
                                    config.locations[0].Owner = config.locations[0].Id;
                                    config.locations[1].Owner = config.locations[0].Id; // make them appear the same so they will disconnect
                                    JoinRoomsToggle(config.locations[0], config.locations[1]);
                                }
                            }
                            break;
                        default: ; break;
                    }
                    break;
                case eSigType.UShort:
                    CrestronConsole.PrintLine("ushort {0}:{1}", args.Sig.Number, args.Sig.UShortValue);
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
        public ThreadCallbackFunction PulseRelDone(object obj)
        {
            try
            {
                Relay rel = (Relay)obj;
                rel.Close();
                Thread.Sleep(200);// This will affect only this thread
                rel.Open();
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("cs PulseRelDone: {1}", e.ToString());
            }
            return null;
        }
        private void VersiportChangeHandler(Versiport device, VersiportEventArgs args)
        {
            CrestronConsole.PrintLine("VersiportChangeHandler, {0} args {1}", device.ToString(), args.Event);
            switch (args.Event)
            {
                case eVersiportEvent.NA: break;                             // Not available.
                case eVersiportEvent.DigitalInChange:                       // The current state of the digital input has changed.
                    if (device.DigitalIn == RoomIsJoined(config.locations[0]))
                        JoinRoomsToggle(config.locations[0], config.locations[1]);
                    //JoinRooms(config.locations[0], config.locations[0], !device.DigitalIn);
                    break;
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
            if (args.State == RoomIsJoined(config.locations[0]))
                JoinRoomsToggle(config.locations[0], config.locations[1]);
        }
        private void UiDigSigHandler(GenericBase currentDevice, uint number, bool value)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            switch (number)
            {
                // momentary functions
                // ..
                // presses or releases handled separately
                default:
                    if (value) // presses
                    {
                        CrestronConsole.PrintLine("press {0}", number);
                        switch (number)
                        {
                            case DIG_START:
                                if (displays == null)
                                    configDisplays();
                                if (config.loginRequired)
                                    ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_LOGIN].BoolValue = true;
                                else
                                    SystemPowerOn(currentDevice);
                                break;
                            case DIG_CANCEL:
                                CloseSubs(currentDevice);
                                break;
                            case DIG_POWER:
                                confirmState = (ushort)number;
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
                                        JoinRoomsToggle(config.locations[0], config.locations[1]);
                                        break;
                                }
                                break;
                            case DIG_JOIN_ALL:
                                ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CONFIRM].BoolValue = false;
                                JoinRoomsAll(config.locations[0], config.locations[1]);
                                eisc.BooleanInput[EISC_DIG_SYS_JOIN].BoolValue = true;
                                break;
                            case DIG_HOME:
                                ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_SPLASH].Pulse();
                                break;
                            case DIG_LIGHTS:
                                ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_LIGHTS].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_LIGHTS].BoolValue;
                                break;
                            case DIG_MIC:
                                ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_MIC].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_MIC].BoolValue;
                                break;
                            case DIG_PICFREEZE:
                                CrestronConsole.PrintLine("DIG_PICFREEZE");
                                bool val = !((BasicTriList)currentDevice).BooleanInput[number].BoolValue;
                                ((BasicTriList)currentDevice).BooleanInput[number].BoolValue = val;
                                CrestronConsole.PrintLine("ui.location.Owner {0}", ui.location.Owner);
                                if (config.locations.Find(x => x.Name == "10-11") != null)
                                {
                                    Display display = displays.Find(x => x.GetName().Contains("Hitachi"));
                                    if (display != null)
                                        display.SetPicFreeze(val ? PowerStates.ON : PowerStates.OFF);
                                }
                                else
                                {
                                    for (int i = 0; i < displays.Count; i++)
                                    {
                                        if (displays[i] != null)
                                        {
                                            CrestronConsole.PrintLine("config.vidOutputs[{0}].room: {1}", i, config.vidOutputs[i].room);
                                            if (config.vidOutputs[i].room <= config.locations.Count)
                                            {
                                                CrestronConsole.PrintLine("config.locations[{0}].Owner: {1}", config.vidOutputs[i].room - 1, config.locations[config.vidOutputs[i].room - 1].Owner);
                                                if (displays[i] != null && ui.location.Owner == config.locations[config.vidOutputs[i].room - 1].Owner)
                                                //if (displays[i] != null)// && ui.location.Owner == 1)
                                                {
                                                    CrestronConsole.PrintLine("displays[{0}].GetName. {1}", i, displays[i].GetName());
                                                    displays[i].SetPicFreeze(val ? PowerStates.ON : PowerStates.OFF);
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                            case DIG_JOIN:
                                confirmState = (ushort)number;
                                ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_CONFIRM].BoolValue = true;
                                if (RoomIsJoined(ui.location))
                                {
                                    ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = "Separate rooms";
                                    ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = "Press confirm to separate the rooms or back to cancel";
                                    ((BasicTriList)currentDevice).BooleanInput[DIG_HAS_JOIN_ALL].BoolValue = false;
                                }
                                else
                                {
                                    ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = "Join rooms";
                                    ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = "Press confirm to join the rooms or back to cancel";
                                    ((BasicTriList)currentDevice).BooleanInput[DIG_HAS_JOIN_ALL].BoolValue =
                                          ((config.locations.Find(x => x.Name.Contains("12.03")) != null)
                                        || (config.locations.Find(x => x.Name.Contains("12.05")) != null)
                                        || (config.locations.Find(x => x.Name.Contains("12.06")) != null)
                                        || (config.locations.Find(x => x.Name.Contains("12.08")) != null)
                                        || (config.locations.Find(x => x.Name.Contains( "2.18")) != null)
                                        || (config.locations.Find(x => x.Name.Contains( "2.19")) != null)
                                        || (config.locations.Find(x => x.Name.Contains( "2.20")) != null)
                                        || (config.locations.Find(x => x.Name.Contains( "2.21")) != null));
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
                            case DIG_RECORD:
                                ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_RECORD].BoolValue = !((BasicTriList)currentDevice).BooleanInput[DIG_SUB_RECORD].BoolValue;
                                //((BasicTriList)currentDevice).BooleanInput[number].BoolValue = !((BasicTriList)currentDevice).BooleanInput[number].BoolValue;
                                break;
                            case DIG_RECORD_PAUSE:
                            case DIG_RECORD_EXTEND:
                                SendRecordingCommand(ui, number);
                                break;
                            case DIG_PROJ_PICFREEZE:
                                if (displays != null && displays[ui.CurrentDeviceControl] != null)
                                {
                                    ((BasicTriList)currentDevice).BooleanInput[number].BoolValue = !((BasicTriList)currentDevice).BooleanInput[number].BoolValue;
                                    displays[ui.CurrentDeviceControl].SetPicFreeze(((BasicTriList)currentDevice).BooleanInput[number].BoolValue ? PowerStates.ON : PowerStates.OFF);
                                }
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
                                SendDocCamCommand(ui, number);
                                break;
                            case DIG_CAM_POWER:
                            case DIG_CAM_ZOOM_IN:
                            case DIG_CAM_ZOOM_OUT:
                            case DIG_CAM_CANVAS:
                                SendCamCommand(ui, number);
                                break;
                            default:
                                {
                                    if (number > 149 && number < 200)
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
                        switch (number)
                        {
                            case DIG_VOL_UP:
                            case DIG_VOL_DN:
                                ui.DoVol(Direction.STOP);
                                break;
                            case DIG_CAM_ZOOM_IN:
                            case DIG_CAM_ZOOM_OUT:
                            case DIG_CAM_CANVAS:
                                SendCamCommand(ui, DIG_CAM_ZOOM_STOP);
                                break;
                            case DIG_DOCCAM_ZOOM_IN:
                            case DIG_DOCCAM_ZOOM_OUT:
                                SendDocCamCommand(ui, DIG_DOCCAM_ZOOM_STOP);
                                break;
                            case DIG_DOCCAM_FOCUS_IN:
                            case DIG_DOCCAM_FOCUS_OUT:
                                SendDocCamCommand(ui, DIG_DOCCAM_FOCUS_STOP);
                                break;
                            
                            case DIG_LIGHTS_UP:
                            case DIG_LIGHTS_DOWN:
                                DoLightRamp(ui.location, Direction.STOP);
                                break;
                            default:
                                if (number > 149 && number < 200)
                                    SendEiscDig(number, value);
                                break;
                        }
                    }
                    break;
            }
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
                        UiDigSigHandler(currentDevice, args.Sig.Number, args.Sig.BoolValue);
                        break;
                    case eSigType.UShort:
                        switch (args.Sig.Number)
                        {
                            case ANA_CAM_X:
                                PanasonicCam cam1 = cams.Find(x => x != null); // todo - which one?
                                cam1.PanPad(args.Sig.UShortValue);
                                break;
                            case ANA_CAM_Y:
                                PanasonicCam cam2 = cams.Find(x => x != null); // todo - which one?
                                cam2.TiltPad(args.Sig.UShortValue);
                                break;
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
        private void KpSigChangeHandler(GenericBase currentDevice, ButtonEventArgs args)
        {
            try
            {
                int i = 0;
                CnxB2 kp = kps.Find(x => x == currentDevice);
                for(i = 0; i < kps.Count; i++)
                {
                    if(kps[i] != null && kps[i] == currentDevice)
                    {
                        kp = kps[i];
                        break;
                    }
                }
                CrestronConsole.PrintLine("KpSigChangeHandler kp {0} {1} {2}, {3}", kp.ID, args.Button.Number, args.Button.VerticalLocation, args.NewButtonState);
                if (args.NewButtonState == eButtonState.Pressed)
                {
                    switch (args.Button.Number)
                    {
                        case 1: // allow selection of the room from the touch panels in the main rooms
                            enableConsultSource[i] = !enableConsultSource[i];
                            foreach (UiWithLocation ui in uis)
                            {
                                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].BooleanInput[String.Format("Item {0} Visible", (ushort)i + 1)].BoolValue = enableConsultSource[i];
                                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].StringInput[String.Format("Set Item {0} Text", (ushort)i + 1)].StringValue =
                                            FormatTextForUi(config.vidInputs[i].devName);
                                ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].StringInput[String.Format("Set Item {0} Icon Serial", (ushort)i + 1)].StringValue =
                                            IconsLg[eIconsLgToKey_Inputs[config.vidInputs[i].devType]];
                                kp.Feedbacks[args.Button.Number].State = enableConsultSource[i];
                            }
                            if (!enableConsultSource[i] && vidMatrix.Outputs.Contains((uint)(i + 1)))
                            {
                                uint input = 0;
                                for (byte j = 1; j <= vidMatrix.NumberOfOutputs; j++)
                                {
                                    DMOutput output = vidMatrix.Outputs[j];
                                    input = output.VideoOutFeedback == null ? 0 : output.VideoOutFeedback.Number;
                                    if (input == i + 1)
                                    {
                                        DoSwitch(0, j, SwitchType.VIDEO);
                                        DoSwitch(0, j, SwitchType.AUDIO);
                                    }
                                }
                            }
                            break;

/*
                                    //CrestronConsole.PrintLine("   device.Outputs[args.Number]: {0}", device.Outputs[args.Number]);
                            }
*/



                        case 2: // route the camera to the USB connectors to allow recording
                            enableConsultDest[i] = !enableConsultDest[i];
                            foreach (UiWithLocation ui in uis)
                            {
                                kp.Feedbacks[args.Button.Number].State = enableConsultDest[i];
                                if(enableConsultDest[i])
                                {
                                    DoSwitch((byte)(1 + i), (byte)(5 + i), SwitchType.VIDEO);
                                    DoSwitch((byte)(1 + i), (byte)(5 + i), SwitchType.AUDIO);
                                }
                                else
                                {
                                    DoSwitch(0, (byte)(5+i), SwitchType.VIDEO);
                                    DoSwitch(0, (byte)(5+i), SwitchType.AUDIO);
                                }
                            }
                            break;
                     }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("KpSigChangeHandler exception: {0}", e.ToString());
            }
        }
        private void CamDPadEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            CrestronConsole.PrintLine("CamDPadEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
            PanasonicCam dev = cams.Find(x => x != null); // todo - which one?
            if (cams[ui.CurrentDeviceControl] != null)
                dev = cams[ui.CurrentDeviceControl];
            if (args.Sig.BoolValue)
            {
                switch (args.Sig.Name)
                {
                    case "Up"   : dev.TiltUp(); break; // up
                    case "Down" : dev.TiltDown(); break; // dn
                    case "Left" : dev.PanLeft(); break; // le
                    case "Right": dev.PanRight(); break; // ri
                }
            }
            else // release
                dev.PanTiltStop();
        }
        private void CamPresetEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            CrestronConsole.PrintLine("CamPresetEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
            PanasonicCam dev = cams.Find(x => x != null); // todo - which one?
            if (cams[ui.CurrentDeviceControl] != null)
                dev = cams[ui.CurrentDeviceControl];
            if (args.Sig.Type == eSigType.Bool)
            {
                if (args.Sig.BoolValue) // presses only
                    dev.PresetRecall((ushort)join);
            }
            else if (args.Sig.Type == eSigType.UShort)
            {
                CrestronConsole.PrintLine("  ushort {0} {1}, value:{2}", args.Sig.Number, args.Sig.Name, args.Sig.UShortValue);
                if (args.Sig.Name.Contains("Held"))
                {
                    CrestronConsole.PrintLine("  held cam preset:{0}", args.Sig.UShortValue);
                    dev.PresetStore((ushort)args.Sig.UShortValue);
                }
            }
            else
                CrestronConsole.PrintLine("         args.Sig.Type {0}", args.Sig.Type);
        }
        private void ProjPicMuteButtonEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            //CrestronConsole.PrintLine("ProjPicMuteButtonEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
            if (args.Sig.Type == eSigType.Bool)
            {
                if (args.Sig.BoolValue) // presses
                {
                    if(displays != null && displays[ui.CurrentDeviceControl] != null)
                    {
                        Display dev = displays[ui.CurrentDeviceControl];
                        ui.Device.SmartObjects[SMART_ID_PROJ_IMAGE_MUTE].BooleanInput[1].BoolValue = join == 1;
                        ui.Device.SmartObjects[SMART_ID_PROJ_IMAGE_MUTE].BooleanInput[2].BoolValue = join == 2;
                        switch (join)
                        {
                            case 1: dev.SetPicMute(PowerStates.ON ); break;
                            case 2: dev.SetPicMute(PowerStates.OFF); break;
                        }
                    }
                    else
                        CrestronConsole.PrintLine("Display[{0}] == null", ui.CurrentDeviceControl);
                }
            }
        }
        private void ProjPowerButtonEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            //CrestronConsole.PrintLine("ProjPowerButtonEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
            if (args.Sig.Type == eSigType.Bool)
            {
                if (args.Sig.BoolValue) // presses
                {
                    if (displays != null && displays[ui.CurrentDeviceControl] != null)
                    {
                        Display dev = displays[ui.CurrentDeviceControl];
                        ui.Device.SmartObjects[SMART_ID_PROJ_POWER].BooleanInput[1].BoolValue = join == 1;
                        ui.Device.SmartObjects[SMART_ID_PROJ_POWER].BooleanInput[2].BoolValue = join == 2;
                        switch (join)
                        {
                            case 1: 
                                dev.SetPower(PowerStates.ON); 
                              break;
                            case 2: 
                                dev.SetPower(PowerStates.OFF); 
                                break;
                        }
                    }
                    else
                        CrestronConsole.PrintLine("Display[{0}] == null", ui.CurrentDeviceControl);
                }
            }
        }
        private void ProjPowerEventHandler(object sender, PowerEventArgs e)
        {
            for (uint i = 0; i < displays.Count; i++)
            {
                if (displays[(int)i] != null && displays[(int)i] == (Display)sender)
                {
                    switch (e.val)
                    {
                        case PowerStates.WARMING:
                        case PowerStates.ON:
                            if (e.mSecsRemaining > 0)
                                SetDestFb(i + 1, String.Format("Warming {0}", e.mSecsRemaining / 1000));
                            else
                            {
                                if(config.vidOutputs[(int)i].currentSourceVid == 0)
                                    SetDestFb(i+1, "Blank"); 
                                else
                                    SetDestFb(i+1, config.vidInputs[config.vidOutputs[(int)i].currentSourceVid-1].devName); 
                            }
                            break;
                        case PowerStates.COOLING: 
                        case PowerStates.OFF    : 
                            //CrestronConsole.PrintLine("ProjPowerEventHandler, display_{0}: {1}, {2}", i, e.val, e.mSecsRemaining);
                            if (e.mSecsRemaining > 0)
                                SetDestFb(i + 1, String.Format("Cooling {0}", e.mSecsRemaining / 1000));
                            else
                                SetDestFb(i+1, "OFF"); 
                            break;
                    }
                }
            }
        }
        private void ProjSourceEventHandler(object sender, DisplaySourceEventArgs e)
        {
            for (int i = 0; i < displays.Count; i++)
            {
                if (displays[i] != null && displays[i] == (Display)sender)
                {
                    CrestronConsole.PrintLine("ProjSourceEventHandler, display_{0}: {1}", i, e.val);
                }
            }
        }
        private void LightPresetEventHandler(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            int join = Utils.atoi(args.Sig.Name);
            //CrestronConsole.PrintLine("LightPresetEventHandler ui_{0} dig:{1}, join: {2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
            if (args.Sig.Type == eSigType.Bool)
            {
                if (args.Sig.BoolValue) // presses
                {
                    foreach (Location loc in config.locations)
                    {
                        if(ui.location.Owner == loc.Owner)
                            lights.RecallPreset((byte)loc.LightArea, (byte)join);
                    }
                }
            }
        }
        private void ReconnectIp(string args) // from console
        {
            if (lights != null && lights.IpComms != null)
            {
                lights.IpComms.DisConnect();
                Thread.Sleep(100);
                lights.IpComms.Connect();
            }
        }
        private void SendBssString(string args)
        {
            if (audDspComms != null)
            {
                CrestronConsole.PrintLine("SendBSS: {0}", Utils.CreatePrintableString(args, false));
                audDspComms.SendString(Utils.GetString(Utils.GetBytesFromAsciiHexString(args)));
            }
        }
        private void QueryEcho(string args)
        {
            if (recording != null)
            {
                Echo360 echo = recording.Find(x => x != null && x.IPAddress != null && x.IPAddress.Length > 0);
                if(echo != null)
                    echo.GetMonitoringStatus();
                //echo.GetCurrent();
                //echo.GetSystemStatus();
           }
        }
        private void EchoHardwareEventHandler(object sender, EchoHardwareDetailsEventArgs args)
        {
            var dev = sender as Echo360;
            //CrestronConsole.PrintLine("Echo apiVersion: {0}", args.val.apiVersion);
            //CrestronConsole.PrintLine("Echo hostAddress: {0}", args.val.hostAddress);
            //CrestronConsole.PrintLine("Echo location: {0}", args.val.location);
        }
        private void EchoStatusEventHandler(object sender, EchoStatusEventArgs args)
        {
            var dev = sender as Echo360;
            //CrestronConsole.PrintLine("Echo state: {0}", args.val.state);
            if (debug > 0)
            {
                CrestronConsole.PrintLine("Echo title: {0}", args.val.title);
                CrestronConsole.PrintLine("Echo presenter: {0}", args.val.presenter);
            }
            foreach (UiWithLocation ui in uis)
            {
                ui.device.BooleanInput[DIG_RECORD].BoolValue = true;
                ui.device.StringInput[SER_RECORD_PRESENTER].StringValue = args.val.presenter;//String.Format("Presenter: {0}", args.val.presenter);
                ui.device.StringInput[SER_RECORD_TITLE].StringValue = args.val.title;//String.Format("Title: {0}", args.val.title);
            }
        }
        private void EchoMonitorEventHandler(object sender, EchoMonitoringEventArgs args)
        {
            try
            {
                var dev = sender as Echo360;
                if (debug > 0)
                    CrestronConsole.PrintLine("Echo state: {0}", args.val.state);
                RoomRels rels = GetRels(config.locations[0].Name);
                //uint rel = (uint)(rels.rec1 == 0 ? 1 : rels.rec1);
                foreach (UiWithLocation ui in uis)
                {
                    ui.device.StringInput[SER_RECORD_STATUS].StringValue = args.val.state;//String.Format("Status: {0}", args.val.state);
                    recordPaused = args.val.state == "paused";
                    ui.device.BooleanInput[DIG_RECORD_PAUSE].BoolValue = recordPaused;
                }
                switch (args.val.state)
                {
                    case "active":
                        if (rels.rec1 != 0)
                        {
                            if (!RelayPorts[rels.rec1].State)
                            {
                                PanasonicCam cam = cams.Find(x => x != null);
                                if (cam == null)
                                    CrestronConsole.PrintLine("No cam found for recording");
                                else
                                {
                                    CrestronConsole.PrintLine("Record started");
                                    cam.PowerOn();
                                    cam.PresetRecall((ushort)1);
                                    //// switch camera to record 2
                                    //for (int i = 0; i < config.vidInputs.Count; i++)
                                    //{
                                    //    RoomPlusDev input = config.vidInputs[i];
                                    //    if (input != null && input.devType == (ushort)VidDev.CAM_1 || input.devType == (ushort)VidDev.CAM_2)
                                    //    {
                                    //        CrestronConsole.PrintLine("cam on input {0}", i);
                                    //        for (byte j = 0; j < config.vidOutputs.Count; j++) // route camera to second feed to recording
                                    //        {
                                    //            if (config.vidOutputs[j] != null && config.vidOutputs[j].devType == (ushort)VidDev.REC_2 && config.vidInputs[i].room == config.vidOutputs[j].room)
                                    //            {
                                    //                CrestronConsole.PrintLine("routing cam[{0}] to record 2: room {1}", i, config.vidOutputs[j].room);
                                    //                DoSwitch((byte)(i + 1), (byte)(j + 1), SwitchType.VIDEO);
                                    //            }
                                    //        }
                                    //    }
                                    //}
                                }
                            }
                            RelayPorts[rels.rec1].Close();
                        }
                        if(rels.rec2 != 0 && RoomIsJoined(config.locations[0]))
                            RelayPorts[rels.rec2].Close();
                        CrestronConsole.PrintLine("Echo remaining: {0}", args.val.remaining);
                        //CrestronConsole.PrintLine("rels.rec1: {0}, rels.rec2: {1}", rels.rec1, rels.rec2);
                        foreach (UiWithLocation ui in uis)
                        {
                            ui.device.BooleanInput[DIG_RECORD].BoolValue = true;
                            ui.device.StringInput[SER_RECORD_DURATION].StringValue = args.val.duration;//String.Format("Duration: {0}", args.val.duration);
                            ui.device.StringInput[SER_RECORD_REMAINING].StringValue = args.val.remaining;
                        }
                        break;
                    case "waiting": break;
                    case "paused":
                        if (rels.rec2 != 0)
                        {
                            if(RoomIsJoined(config.locations[0]))
                                RelayPorts[rels.rec2].State = !RelayPorts[rels.rec1].State;
                            else
                                RelayPorts[rels.rec2].Open();
                        }
                        if (rels.rec1 != 0)
                            RelayPorts[rels.rec1].State = !RelayPorts[rels.rec1].State;
                        foreach (UiWithLocation ui in uis)
                        {
                            ui.device.BooleanInput[DIG_RECORD].BoolValue = RelayPorts[rels.rec1].State;
                            ui.device.StringInput[SER_RECORD_DURATION].StringValue = args.val.duration;//String.Format("Duration: {0}", args.val.duration);
                            ui.device.StringInput[SER_RECORD_REMAINING].StringValue = args.val.remaining;
                        }
                        break;
                    case "complete": break;
                    case "inactive":
                        if (rels.rec1 != 0)
                            RelayPorts[rels.rec1].Open();
                        if(rels.rec2 != 0)
                            RelayPorts[rels.rec2].Open();
                        foreach (UiWithLocation ui in uis)
                        {
                            ui.device.BooleanInput[DIG_RECORD].BoolValue = false;
                            ui.device.StringInput[SER_RECORD_DURATION].StringValue = "";
                            ui.device.StringInput[SER_RECORD_PRESENTER].StringValue = "";
                            ui.device.StringInput[SER_RECORD_TITLE].StringValue = "";
                            ui.device.StringInput[SER_RECORD_REMAINING].StringValue = "";
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("EchoMonitorEventHandler: {0}", e.ToString());
            }
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
                    CrestronConsole.PrintLine("MicsEventHandler ui_{0} dig:{1}, join:{2}, {3}, {4}", ui.Id, args.Sig.Number, join, args.Sig.Name, args.Sig.BoolValue);
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
                            case 3: // mute
                                CrestronConsole.PrintLine(" mic {0} mute", index);
                                mics[index - 1].DoMute(PowerStates.TOGGLE);
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
                                    SystemPowerOff(currentDevice);
                                    break;
                                case DIG_JOIN:
                                    JoinRoomsToggle(config.locations[0], config.locations[1]);
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
                    ui.CurrentDeviceControl = (ushort)(args.Sig.UShortValue - 1);
                    ((BasicTriList)currentDevice).StringInput[SER_CUR_DEVICE_TXT].StringValue = config.vidOutputs[args.Sig.UShortValue - 1].devName;
                    switch (config.vidOutputs[args.Sig.UShortValue - 1].devType)
                    {
                        case (ushort)VidDev.PROJ_1:
                        case (ushort)VidDev.PROJ_2:
                        case (ushort)VidDev.PROJ_3:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_PROJ].BoolValue = true;
                            if(displays[ui.CurrentDeviceControl] != null)
                            {
                                ui.Device.SmartObjects[SMART_ID_PROJ_POWER].BooleanInput[1].BoolValue = displays[ui.CurrentDeviceControl].PowerCurrent == PowerStates.ON;
                                ui.Device.SmartObjects[SMART_ID_PROJ_POWER].BooleanInput[2].BoolValue = displays[ui.CurrentDeviceControl].PowerCurrent == PowerStates.OFF;
                                ui.Device.SmartObjects[SMART_ID_PROJ_IMAGE_MUTE].BooleanInput[1].BoolValue = displays[ui.CurrentDeviceControl].PicMuteCurrent == PowerStates.ON;
                                ui.Device.SmartObjects[SMART_ID_PROJ_IMAGE_MUTE].BooleanInput[2].BoolValue = displays[ui.CurrentDeviceControl].PicMuteCurrent == PowerStates.OFF;
                            }
                            break;
                        default:
                            ((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = true;
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_TITLE].StringValue = String.Format("Button held");
                            ((BasicTriList)currentDevice).StringInput[SER_CUR_SEL_BODY].StringValue = String.Format("There are no controls for {0}", config.vidOutputs[args.Sig.UShortValue - 1].devName);
                            if (CommonTimer == null)
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
                    ui.CurrentDeviceControl = (ushort)(args.Sig.UShortValue - 1);
                    ((BasicTriList)currentDevice).StringInput[SER_CUR_DEVICE_TXT].StringValue = config.vidInputs[args.Sig.UShortValue - 1].devName;
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
                            if (CommonTimer == null)
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
            UiWithLocation ui = uis.Find(x => x != null && x.Device == currentDevice);
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
                    ui.CurrentDeviceControl = (ushort)(args.Sig.UShortValue - 1);
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
                            if (CommonTimer == null)
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
            try
            {
                UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
                int firstInput = config.vidInputs.FindIndex(x => x != null && x.room == (byte)2);
                uint join = (uint)Utils.atoi(args.Sig.Name);
                if (args.Sig.Type == eSigType.Bool)
                {
                    CrestronConsole.PrintLine("SourceSelectLoc01EventHandler ui_{0} dig:{1}, {2}, {3}", ui.Id, args.Sig.Number, args.Sig.Name, args.Sig.BoolValue);
                    if (args.Sig.BoolValue) // presses only
                        DoSourcePress(ui, join);
                    else // release
                        DoSourceRelease(ui, join);
                }
                else if (args.Sig.Type == eSigType.UShort)
                {
                    CrestronConsole.PrintLine("  ushort {0} {1}, value:{2}", args.Sig.Number, args.Sig.Name, args.Sig.UShortValue);
                    ui.CurrentDeviceControl = (ushort)(args.Sig.UShortValue - 1);
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
                                if (CommonTimer == null)
                                    CommonTimer = new CTimer(delegate(object obj) { CloseSubs((GenericBase)obj); CommonTimer.Dispose(); }, currentDevice, 2000, 0);
                                break;
                        }
                    }
                }
                else
                    CrestronConsole.PrintLine("         args.Sig.Type {0}", args.Sig.Type);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("SourceSelectLoc01EventHandler exception: {0}", e.ToString());
            }
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
                    CrestronConsole.PrintLine("~");
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("UiOnlineHandler exception: {0}", e.ToString());
            }
        }
        private void KpOnlineHandler(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            try
            {
                CnxB2 kp = kps.Find(x => x == currentDevice);
                CrestronConsole.PrintLine("KpOnlineHandler kp ID:{0} {1}", kp.ID, args.DeviceOnLine.ToString());
                if (args.DeviceOnLine)
                {
                    UpdateKpFb(currentDevice);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("KpOnlineHandler exception: {0}", e.ToString());
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
                switch(args.EventId)
                {
                    case DMSystemEventIds.FrontPanelLockOffEventId: break;
                }
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
                CrestronConsole.PrintLine("vidMatrixOutputChange: out {0}", args.Number);
                int numberOfInputs;
                int numberOfOutputs;
                uint input = 0;
                if (device == null) // DMPS - inputs and outputs are just indexes
                {
                    //if (args.EventId != DMOutputEventIds.OutputVuFeedBackEventId) // this event is probably why the DMPS3 is so sluggish.
                    //    Dmps3ProcessChange(_dmOutputEventIdsMapping, "Output", "Output Num", (uint)args.EventId, args.Number, args.Stream);
                    device = vidMatrix;
                    numberOfInputs = SwitcherInputs.Count;
                    numberOfOutputs = SwitcherOutputs.Count;                    
                    var stream = args.Stream;
                    var dmHdmiAudio = SwitcherOutputs[args.Number] as Card.Dmps3DmHdmiAudioOutput;
                    if (dmHdmiAudio == null)
                        return;
                    IDmps3OutputMixer outputMix = null;
                    switch (args.EventId)
                    {
                        case (DMOutputEventIds.OutputVuFeedBackEventId): //Since Vufeedback is valid for both the DM/Hdmi and Analog audio output mixers, need to see which stream sent the information.
                            switch (args.Stream.Type)
                            {
                                case eDmStreamType.DmpsAudio:
                                    var audioStream = args.Stream as Card.Dmps3DmHdmiAudioOutput.Dmps3AudioOutputStream;
                                    if (audioStream != null)
                                        outputMix = audioStream.OutputMixer;
                                    break;
                                case eDmStreamType.DmpsDmHdmi:
                                    var dmHdmiStream = args.Stream as Card.Dmps3DmHdmiAudioOutput.Dmps3DmHdmiOutputStream;
                                    if (dmHdmiStream != null)
                                        outputMix = dmHdmiStream.OutputMixer;
                                    break;
                            }
                            if (outputMix != null) //If the correct output mixer is found print the current VU value.
                                CrestronConsole.PrintLine("{0} VU: {1}dB", args.Stream, (short)outputMix.OutputVuFeedback.UShortValue);
                            break;
                        case (DMOutputEventIds.VideoOutEventId)://Print what input is routed to the output.
                            CrestronConsole.PrintLine("{0} - Viewing Video: {1}", SwitcherOutputs[args.Number], dmHdmiAudio.VideoOutFeedback);
                            if (args.Number <= numberOfOutputs)
                            {
                                uint output = args.Number;
                                //uint output = SwitcherOutputs[args.Number];
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
                                }
                                CrestronConsole.PrintLine("DMPS Video Input {0} switched to Output {1}", input, output);
                            }                            
                            break;
                        case (DMOutputEventIds.AudioOutEventId)://Print what audio is routed to the output.
                            CrestronConsole.PrintLine("{0} - Hearing Audio: {1}", SwitcherOutputs[args.Number], dmHdmiAudio.VideoOutFeedback);
                            break;
                        case (DMOutputEventIds.MasterVolumeFeedBackEventId): //Print the current master volume of the streams.
                            if (args.Stream == dmHdmiAudio.DmHdmiOutputStream)
                                CrestronConsole.PrintLine("Stream: {0} - MasterVolume: {1}", args.Stream, (short)dmHdmiAudio.DmHdmiOutputStream.MasterVolumeFeedBack.UShortValue);
                            else if (args.Stream == dmHdmiAudio.AudioOutputStream)
                                CrestronConsole.PrintLine("Stream: {0} - MasterVolume: {1}", args.Stream, (short)dmHdmiAudio.AudioOutputStream.MasterVolumeFeedBack.UShortValue);
                            break;
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
                    //CrestronConsole.PrintLine("{0} dmTxBaseEventHandler, VideoSourceFeedbackEventId {1}", device == null ? "" : device.ToString(), args.EventId.ToString());
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
                    //CrestronConsole.PrintLine("{0} dmRxBaseEventHandler, VideoSourceFeedbackEventId {1}", device == null ? "" : device.ToString(), args.EventId.ToString());
                    break;

                default:
                    break;
            }
        }
        private void dmTxDisplayPortInputStreamChangeEventHandler(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} DisplayPortInputStreamChange {1}", inputStream.ToString(), args.ToString());
            switch (args.EventId)
            {
                case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId:
                    break;
            }
        }
        private void dmTxHdmiInputStreamChangeEventHandler(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} HdmiInputStreamChange {1}", inputStream.ToString(), args.ToString());
            switch (args.EventId)
            {
                case EndpointInputStreamEventIds.SyncDetectedFeedbackEventId: break;
            }
        }
        private void dmTxVgaInputStreamChangeEventHandler(EndpointInputStream inputStream, EndpointInputStreamEventArgs args)
        {
            //CrestronConsole.PrintLine("{0} VgaInputStreamChange {1}", inputStream.ToString(), args.ToString());
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
            //CrestronConsole.PrintLine("{0} DisplayPortAttributeChange {1}", sender.ToString(), args.ToString());
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
        private void Dmps3HdmiInputStreamCec_CecChange(Cec cecDevice, CecEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((HdmiInputWithCEC)(cecDevice.Owner)).InputOutput;
            //CrestronConsole.PrintLine("CEC Device Info Change on {0} Number {1} [{2}], Event Id {3}", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString(), args.EventId);
        }
        private void VgaDviInputPortVideoControlsBasic_ControlChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((VgaDviInputPort)(((VideoControlsBasic)sender).Owner)).InputOutput;
            //CrestronConsole.PrintLine("VgaDviInputPortVideoControlsBasic_ControlChange Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void VgaDviInputPortVideoAttributesBasic_AttributeChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((VgaDviInputPort)(((VideoAttributesBasic)sender).Owner)).InputOutput;
            //CrestronConsole.PrintLine("VideoAttributesBasic Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void BncInputPortVideoAttributes_AttributeChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((Component)(((VideoControlsBasic)sender).Owner)).InputOutput;
            //CrestronConsole.PrintLine("VideoControlsBasic Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void BncInputPortVideoControls_ControlChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((Component)(((VideoAttributesBasic)sender).Owner)).InputOutput;
            //CrestronConsole.PrintLine("VideoAttributesBasic Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void DmInputPortVideoAttributes_AttributeChange(object sender, GenericEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((Dmps3DmInputPort)(((VideoAttributesEnhanced)sender).Owner)).InputOutput;
            //CrestronConsole.PrintLine("VideoControlsEnhanced Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
        }
        private void Dmps3HdmiOutputStreamCec_CecChange(Cec cecDevice, CecEventArgs args)
        {
            DMInputOutputBase SwitcherInputOutput = ((OutputCardHdmiOutBasicPort)(cecDevice.Owner)).InputOutput;
            //CrestronConsole.PrintLine("CEC Device Info Change on {0} Number {1} [{2}]", SwitcherInputOutput.IoType.ToString(), SwitcherInputOutput.Number, SwitcherInputOutput.ToString());
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
        private void Dmps3ProcessChange(Dictionary<uint, string> EventIdsMapping, string label, string IndexLabel, uint EventId, uint EventIndex)
        {
            string EventIdName;
            if (EventIdsMapping.TryGetValue(EventId, out EventIdName))
                CrestronConsole.PrintLine("Dmps3 {0} Event {1} {2} {3} ({4})", label, EventIdName, IndexLabel, EventIndex, EventId);
            else
                CrestronConsole.PrintLine("Dmps3 {0} Event UNKNOWN {1} {2} ({3})", label, IndexLabel, EventIndex, EventId);
        }
        private void Dmps3ProcessChange(Dictionary<uint, string> EventIdsMapping, string label, string IndexLabel, uint EventId, uint EventIndex, IDmCardStreamBase Stream)
        {
            string EventIdName;
            if (EventIdsMapping.TryGetValue(EventId, out EventIdName))
            {
                if (Stream == null)
                    CrestronConsole.PrintLine("Dmps3 {0} Event {1} {2} {3} ({4})", label, EventIdName, IndexLabel, EventIndex, EventId);
                else
                    CrestronConsole.PrintLine("Dmps3 {0} Event {1} {2} {3} Stream {4} ({5})", label, EventIdName, IndexLabel, EventIndex, Stream.ToString(), EventId);
            }
            else
            {
                if (Stream == null)
                    CrestronConsole.PrintLine("Dmps3 {0} Event UNKNOWN {1} {2} ({4})", label, IndexLabel, EventIndex, EventId);
                else
                    CrestronConsole.PrintLine("Dmps3 {0} Event UNKNOWN {1} {2} Stream {3} ({4})", label, IndexLabel, EventIndex, Stream.ToString(), EventId);
            }
        }
        private void Dmps3ProcessChange(Dictionary<uint, string> EventIdsMapping, string label, uint EventId)
        {
            string EventIdName;
            if (EventIdsMapping.TryGetValue(EventId, out EventIdName))
                CrestronConsole.PrintLine("Dmps3 {0} Event {1} ({2})", label, EventIdName, EventId);
            else
                CrestronConsole.PrintLine("Dmps3 {0} Event UNKNOWN ({3})", label, EventId);
        }
        #endregion
        #region fb functions
        private void CloseSubs(GenericBase currentDevice)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            CrestronConsole.PrintLine("ui:{0} closing subs", ui.Id);
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
            ((BasicTriList)currentDevice).UShortInput[ANA_TIMER_PERCENT].UShortValue = 0;
            ((BasicTriList)currentDevice).UShortInput[ANA_TIMER_VAL].UShortValue = 0;
            //if (powerTimer == null)
            powerTimer = new CTimer(powerTimerExpired, currentDevice, 1, powerTimerInterval);
            //else
            //    powerTimer.Reset();
        }
        private void powerTimerExpired(object obj)
        {
            try
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
            catch (Exception e)
            {
                CrestronConsole.PrintLine("cs powerTimerExpired: {1}", e.ToString());
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
        private void UpdateKpFb(GenericBase currentDevice)
        {
            CnxB2 kp = kps.Find(x => x != null && x == currentDevice);
            int roomIdx = (int)(kp.ID - CRESNET_KEYPAD_BASE - 1);
            //kp.Button[1].State = displ
        }
        public void UpdateFb(GenericBase currentDevice)
        {
            try
            {
                UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
                CrestronConsole.PrintLine("UpdateFb ui:{0}", ui.Id);
                UpdateUiLists(currentDevice);
                ui.Device.BooleanInput[DIG_HAS_MIC].BoolValue = (config.mics != null && config.mics.Count > 0);
                bool cam1 = config.vidInputs.Find(x => x != null && x.devType == (ushort)VidDev.CAM_1) != null;
                bool cam2 = config.vidInputs.Find(x => x != null && x.devType == (ushort)VidDev.CAM_2) != null;
                //ui.Device.BooleanInput[DIG_HAS_CAM].BoolValue = (cam1 || cam2);
                //ui.Device.BooleanInput[DIG_HAS_CAM].BoolValue = (config.vidInputs.Find(x => x != null && x.devType == (ushort)VidDev.CAM_1) != null || config.vidInputs.Find(x => x.devType == (ushort)VidDev.CAM_2) != null);
                ui.Device.BooleanInput[DIG_HAS_JOIN].BoolValue = (config.locations.Count > 1 && config.locations.Find(x => x.Name.Contains("11.20")) == null);
                bool showEchoButton = false;
                for(int i = 0; i < config.vidOutputs.Count; i++)
                {
                    if(config.vidOutputs[i] != null && config.vidOutputs[i].devType == (ushort)VidDev.REC_1 && config.vidOutputs[i].room <= config.locations.Count)
                        showEchoButton = config.vidOutputs[i].room == ui.location.Id || config.locations[ui.location.Id - 1].Owner == config.locations[config.vidOutputs[i].room - 1].Owner;
                }
                ui.Device.BooleanInput[DIG_HAS_RECORD].BoolValue = showEchoButton;
                CrestronConsole.PrintLine("has record done");

                UpdateZoneVolFb(ui.location);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("UpdateFb exception {0}", e.ToString());
            }
        }
        public void DoVidOutFb(uint input, uint output)
        {
            try
            {
                if (input == 0 || input > config.vidInputs.Count())
                    SetDestFb(output, "No input");
                else
                    SetDestFb(output, config.vidInputs[(int)(input - 1)].devName);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DoVidOutFb exception: {0}", e.ToString());
            }
        }
        public void SetDestFb(uint output, string str)
        {
            try
            {
                //if (output <= device.NumberOfOutputs;
                CrestronConsole.PrintLine("SetDestFb, output: {0}, {1}", output, str);
                byte destVidMatrix = remapVidMatrixOutputs[output];
                if (config.vidOutputs[(int)(destVidMatrix - 1)] == null)
                    CrestronConsole.PrintLine("config.vidOutputs[{0}] == null", destVidMatrix - 1);
                else
                {
                    byte loc = config.vidOutputs[(int)(destVidMatrix - 1)].room;
                    foreach (UiWithLocation ui in uis)
                    {
                        string destStr;
                        if (RoomIsJoined(ui.location) && config.vidOutputs[(int)(destVidMatrix - 1)].room <= config.locations.Count)
                        {

                            destStr = String.Format("{0}{1}", config.vidOutputs[(ushort)destVidMatrix - 1].devName,
                                config.vidOutputs[(ushort)destVidMatrix - 1].devType == (ushort)VidDev.PROJ_1 ||
                                config.vidOutputs[(ushort)destVidMatrix - 1].devType == (ushort)VidDev.PROJ_2 ||
                                config.vidOutputs[(ushort)destVidMatrix - 1].devType == (ushort)VidDev.PROJ_3 ||
                                config.vidOutputs[(ushort)destVidMatrix - 1].devType == (ushort)VidDev.LCD ? " " + config.locations[config.vidOutputs[(ushort)destVidMatrix - 1].room - 1].Name : "");
                        }
                        else
                            destStr = config.vidOutputs[(ushort)destVidMatrix - 1].devName;
                        if(enableRemap)
                            ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)destListRemapButtons[destVidMatrix])].StringValue = FormatTextForUi(destStr) + "\r" + FormatTextForUi(str, FontSizeLine[1], null, "black");
                        else
                            ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)destVidMatrix)].StringValue = FormatTextForUi(destStr) + "\r" + FormatTextForUi(str, FontSizeLine[1], null, "black");
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("SetDestFb exception: {0}", e.ToString());
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
        public void RemapDestList(string str)
        {
            RemapDestList();
        }
        public void RemapDestList()
        {
            // if   vidOutput[0]=Rm2 Proj, [1]=Rm2 IWB, [2]=Rm1 Proj, [3]=Rm1 IWB
            // then SmartObjects[SMART_ID_DEST_LIST][0]=Rm1 Proj, [1]=Rm1 IWB, [2]=Rm2 Proj, [3]=Rm2 IWB
            // PRESSES - destListRemapButtons = { 2,3,0,1 } // presing 1 (Rm1 Proj) ->  destListRemapButtons[Utils.atoi(args.Sig.Name)-1] = 2 where vidOutputs[2] = Rm1Proj
            // send feedback to buttons without offset vidOutput[0] -> button index 2
            // get presses with offset of -1 SG press join 1-1 -> vidOutput[0] = 2
            int roomsFound = 0;
            for (ushort roomCurrent = 1; roomsFound < config.locations.Count; roomCurrent++)
            {
                Location loc = config.locations.Find(x => x.Id == roomCurrent);
                if (loc != null)
                {
                    for (ushort i = 0; i < config.vidOutputs.Count; i++)
                        if (config.vidOutputs[i] != null && config.vidOutputs[i].room == roomCurrent)
                            destListRemapButtons.Add(i);
                    roomsFound++;
                }
            }

        }
        public void GetDestListIndex(int join)
        {
            // if I press the first button in the SG 
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
                    if(RoomIsJoined(ui.location))
                    {
                        if(joinState == 4)
                            ui.Device.StringInput[SER_JOIN_STATUS_LABEL].StringValue = "4 Rooms";
                        else
                            ui.Device.StringInput[SER_JOIN_STATUS_LABEL].StringValue = "Rooms";
                    }
                    
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

                    bool joined = RoomIsJoined(ui.location);
                    CrestronConsole.PrintLine(" updateMics");
                    ushort micIndex = 0;
                    foreach (Level mic in config.mics)
                    {
                        micIndex++;
                        //CrestronConsole.PrintLine(" micIndex {0}, ui.location.Owner:{1} == config.locations[{2}].Owner:{3}", micIndex, ui.location.Owner, mic.room-1, config.locations[mic.room-1].Owner);
                        ui.Device.SmartObjects[SMART_ID_MICS].BooleanInput[String.Format("Item {0} Visible", micIndex)].BoolValue = ui.location.Owner == config.locations[mic.room - 1].Owner;
                        if (ui.location.Owner == config.locations[mic.room - 1].Owner)
                        {
                            ui.Device.SmartObjects[SMART_ID_MICS].StringInput[String.Format("text-o{0}", micIndex)].StringValue = joined ? String.Format("Room {0} {1}", mic.room, mic.name) : "" + mic.name;
                            //ui.Device.SmartObjects[SMART_ID_MICS].StringInput[String.Format("text-o{0}", micIndex)].StringValue = mic.name;
                            ui.Device.SmartObjects[SMART_ID_MICS].UShortInput[String.Format("an_fb{0}", micIndex)].UShortValue = (ushort)(mic.level * 655);
                            ui.Device.SmartObjects[SMART_ID_MICS].BooleanInput[String.Format("fb{0}", ((micIndex - 1) * DIG_INC_MICS) + DIG_IDX_MIC_MUTE    )].BoolValue = mic.mute;
                            bool visible = mic.name.Equals("Lectern") || mic.name.Equals("Gooseneck");
                            ui.Device.SmartObjects[SMART_ID_MICS].BooleanInput[String.Format("fb{0}", ((micIndex - 1) * DIG_INC_MICS) + DIG_IDX_MIC_MUTE_VIS)].BoolValue = visible;
                        }
                    }

                    //if (config.locations.Exists(x => x.Id == loc)) // avoid exception
                    //CrestronConsole.PrintLine("ui_{0} id exists", ui.Id);
                    //CrestronConsole.PrintLine("ui_{0} joined rooms: {1}, inputs: {2}, outputs: {3}", ui.Id, joined, config.vidInputs.Count(), config.vidOutputs.Count());
                    ui.Device.BooleanInput[DIG_SUB_JOIN_0].BoolValue = !joined;
                    ui.Device.BooleanInput[DIG_SUB_JOIN_1].BoolValue = joined;

                    if (joined) // joined
                    {
                        CrestronConsole.PrintLine(" update source lists - joined");
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
                        CrestronConsole.PrintLine(" update dest lists - joined");
                        for (int i = 0; i < config.vidOutputs.Count(); i++)
                        {
                            if (config.vidOutputs[i] == null)
                            {
                                CrestronConsole.PrintLine("VidOut {0}: null", i);
                            }
                            else
                            {
                                //CrestronConsole.PrintLine("VidOut {0}: {1}", i, config.vidOutputs[i].devName);
                                string source = config.vidOutputs[i].currentSourceVid == 0 ? "Blank" : config.vidInputs[config.vidOutputs[i].currentSourceVid-1].devName;
                                string dest = String.Format("{0}", config.vidOutputs[i].devName); 
                                if (config.vidOutputs[i].room <= config.locations.Count)
                                {
                                    dest = String.Format("{0}{1}", config.vidOutputs[i].devName, 
                                            config.vidOutputs[i].devType == (ushort)VidDev.PROJ_1 ||
                                            config.vidOutputs[i].devType == (ushort)VidDev.PROJ_2 ||
                                            config.vidOutputs[i].devType == (ushort)VidDev.PROJ_3 ||
                                            config.vidOutputs[i].devType == (ushort)VidDev.LCD ? " " + config.locations[config.vidOutputs[i].room - 1].Name : "");
                                }

                                if (enableRemap)
                                {
                                    ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)destListRemapButtons[i + 1])].StringValue = 
                                            FormatTextForUi(dest) + "\r" + FormatTextForUi(source, 14, null, "black");
                                    ui.Device.SmartObjects[SMART_ID_DEST_LIST].BooleanInput[String.Format("Item {0} Visible", (ushort)destListRemapButtons[i + 1])].BoolValue =
                                            config.locations[config.vidOutputs[i].room-1].Owner == ui.location.Owner // exists
                                            && !(IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]] == "DVR" && !config.showRecordInMenu) // not (DVR && hidden) 
                                            &&   IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]] != "Blank" // not (blank) 
                                            &&  !IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]].Contains("Music Note"); // not (audio) 
                                    ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Icon Serial", (ushort)destListRemapButtons[i + 1])].StringValue = 
                                            IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]];
                                }
                                else
                                {
                                    ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)i + 1)].StringValue = 
                                            FormatTextForUi(dest) + "\r" + FormatTextForUi(source, 14, null, "black");
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
                    }
                    else // not joined
                    {
                        CrestronConsole.PrintLine(" update source lists");
                        for (int i = 0; i < config.vidInputs.Count(); i++)
                        {
                            if (config.vidInputs[i] != null)
                            {
                                if (config.vidInputs[i].room <= config.locations.Count)
                                {
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
                                else
                                {
                                    // colaboration rooms
                                    CrestronConsole.PrintLine("Colaboration room camera [{0}] {1}", i, config.vidInputs[i].room);
                                }
                            }
                        }
                        CrestronConsole.PrintLine(" update dest lists");
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

                                if (config.vidOutputs[i].room <= config.locations.Count)
                                {
                                    if (enableRemap)
                                    {
                                    }
                                    else
                                    {
                                        ui.Device.SmartObjects[SMART_ID_DEST_LIST].BooleanInput[String.Format("Item {0} Visible", (ushort)i + 1)].BoolValue =
                                            //config.vidOutputs[i].room == config.locations[0].Id // exists
                                                config.locations[config.vidOutputs[i].room - 1].Owner == ui.location.Owner // exists
                                                && !(IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]] == "DVR" && !config.showRecordInMenu) // not (DVR && hidden) 
                                                && IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]] != "Blank" // not (blank) 
                                                && !IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]].Contains("Music Note"); // not (audio) 
                                        if (config.locations[config.vidOutputs[i].room - 1].Owner == ui.location.Owner)
                                        {
                                            string source = config.vidOutputs[i].currentSourceVid == 0 ? "Blank" : config.vidInputs[config.vidOutputs[i].currentSourceVid - 1].devName;
                                            ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)i + 1)].StringValue =
                                                    FormatTextForUi(config.vidOutputs[i].devName) + "\r" + FormatTextForUi(source, 14, null, "black");
                                            ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Icon Serial", (ushort)i + 1)].StringValue = IconsLg[eIconsLgToKey_Outputs[config.vidOutputs[i].devType]];
                                            //uis[(ushort)loc].Device.SmartObjects[SMART_ID_DEST_LIST].UShortInput[String.Format("Set Item {0} Icon Analog", (ushort)i + 1)].UShortValue = (int)eIconsLg.PROJ_1;
                                        }
                                    }
                                }
                                else
                                {
                                    CrestronConsole.PrintLine("Colaboration room streamer [{0}] {1}", i, config.vidOutputs[i].room);
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
        public void SetAudioPreset(int val)
        {
            CrestronConsole.PrintLine("SetAudioPreset: {0}", val);
            if (audDspComms != null)
                audDspComms.SetBSSParamPreset(val);
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
            }
            for(int i = 0; i < config.vidOutputs.Count; i++)
            {
                if (config.vidOutputs[i] != null)
                {
                    int outLoc = config.vidOutputs[i].room;
                    int currentSource = config.vidOutputs[i].currentSourceVid;
                    if (currentSource > 0)
                    {
                        int inLoc = config.vidInputs[currentSource-1].room;
                        //if (currentSource < config.vidInputs.Count && config.vidInputs[currentSource - 1] != null) // 20170501
                        if (currentSource <= config.vidInputs.Count && config.vidInputs[currentSource - 1] != null)
                        {

                            CrestronConsole.PrintLine("vidOutputs[{0}] loc:{1} {2}, currentSourceVid: {3} {4} loc:{5}",
                                i, config.vidOutputs[i].room, config.vidOutputs[i].devName,
                                currentSource, config.vidInputs[currentSource-1].devName, config.vidInputs[currentSource-1].room);

                            if (config.locations[outLoc-1].Owner != config.locations[inLoc-1].Owner)
                            {
                                DoSwitch(0, (byte)(i + 1), SwitchType.VIDEO);
                                //DoSwitch(0, (byte)(i + 1), SwitchType.AUDIO);
                            }
                        }
                        if (config.vidOutputs[i].devType == (ushort)VidDev.AUDIO ||
                            (config.vidOutputs[i].devType == (ushort)VidDev.VC && config.locations.Find(x => x.Name.Contains("12.03")) != null))
                        {
                            CrestronConsole.PrintLine("AudOutputs[{0}] loc:{1} {2}",
                                i, config.vidOutputs[i].room, config.vidOutputs[i].devName);
                            if (config.locations[outLoc - 1].Owner != config.locations[inLoc - 1].Owner)
                            {
                                CrestronConsole.PrintLine("Clearing audio output");
                                DoSwitch(0, (byte)(i + 1), SwitchType.AUDIO);
                            }
                        }
                    }
                }
            }

            foreach (UiWithLocation ui in uis)
            {
                UpdateUiLists(ui.Device);
                UpdateFb(ui.Device);
            }
        }
        public void JoinRooms(Location loc, Location loc2, bool state)
        { // this doesn't quite work so I just toggle FTM :(
            if (state) // join
            {
                if(loc.Owner != loc2.Owner)
                {
                    loc2.Owner = loc.Owner;
                }
            }
            else  // separate
            {
                if(loc.Owner == loc2.Owner)
                {
                    loc2.Owner = loc2.Id;
                    loc.Owner = loc.Id;
                }
            }
            if (state) // un
            {
                if ((config.locations.Find(x => x.Name.Contains("12.03")) != null)
                 || (config.locations.Find(x => x.Name.Contains("12.05")) != null)
                 || (config.locations.Find(x => x.Name.Contains( "2.20")) != null)
                 || (config.locations.Find(x => x.Name.Contains( "2.21")) != null))
                    SetAudioPreset(PRESET_COMBINE_3_4_OFF);
                else if ((config.locations.Find(x => x.Name.Contains("12.06")) != null)
                      || (config.locations.Find(x => x.Name.Contains("12.08")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.18")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.19")) != null))
                    SetAudioPreset(PRESET_COMBINE_1_2_OFF);
                else
                    SetAudioPreset(PRESET_COMBINE_NONE);
            }
            else // comb
            {
                if ((config.locations.Find(x => x.Name.Contains("12.03")) != null)
                 || (config.locations.Find(x => x.Name.Contains("12.05")) != null)
                 || (config.locations.Find(x => x.Name.Contains( "2.20")) != null)
                 || (config.locations.Find(x => x.Name.Contains( "2.21")) != null))
                    SetAudioPreset(PRESET_COMBINE_3_4_ON);
                else if ((config.locations.Find(x => x.Name.Contains("12.06")) != null)
                      || (config.locations.Find(x => x.Name.Contains("12.08")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.18")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.19")) != null))
                    SetAudioPreset(PRESET_COMBINE_1_2_ON);
                else
                    SetAudioPreset(1);
            } 
            doJoinFeedback();
        }

        public void JoinRoomsToggle(Location loc, Location loc2)
        {
            if (loc2 == loc) // this location - separate all rooms from this room
            {
                CrestronConsole.PrintLine("separate all rooms from room {0}", loc);
                joinState = 0;
                eisc.BooleanInput[EISC_DIG_SYS_JOIN].BoolValue = false;
                eisc.UShortInput[1].UShortValue = 0;
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
                if (joinState == 0)
                    joinState = 2;
                if (loc2.Owner == loc.Id ||
                    loc2.Owner == loc.Owner) // owned by this location OR the same location as this location
                {
                    CrestronConsole.PrintLine("loc2 owner {0} location {1}, owned by this location OR the same location", loc2.Owner, loc.Id);
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
                    else
                    {
                        joinState = 0;
                        eisc.BooleanInput[EISC_DIG_SYS_JOIN].BoolValue = false;
                        eisc.UShortInput[1].UShortValue = 0;
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
            //bool state = RoomIsJoined(loc);
            CrestronConsole.PrintLine("joinState: {0}, RoomIsJoined(loc): {1}", joinState, RoomIsJoined(loc));
            if (RoomIsJoined(loc)) // unjoin
            {
                if (joinState == 4)
                {
                    SetAudioPreset(PRESET_COMBINE_1_2_OFF);
                    SetAudioPreset(PRESET_COMBINE_3_4_OFF);
                    SetAudioPreset(PRESET_COMBINE_NONE);
                }
                else if ((config.locations.Find(x => x.Name.Contains("12.03")) != null)
                      || (config.locations.Find(x => x.Name.Contains("12.05")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.20")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.21")) != null))
                    SetAudioPreset(PRESET_COMBINE_3_4_OFF);
                else if ((config.locations.Find(x => x.Name.Contains("12.06")) != null)
                      || (config.locations.Find(x => x.Name.Contains("12.08")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.18")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.19")) != null))
                    SetAudioPreset(PRESET_COMBINE_1_2_OFF);
                else
                    SetAudioPreset(PRESET_COMBINE_NONE);
               joinState = 0;
            }
            else
            {
                if ((config.locations.Find(x => x.Name.Contains("12.03")) != null)
                 || (config.locations.Find(x => x.Name.Contains("12.05")) != null)
                 || (config.locations.Find(x => x.Name.Contains( "2.20")) != null)
                 || (config.locations.Find(x => x.Name.Contains( "2.21")) != null))
                    SetAudioPreset(PRESET_COMBINE_3_4_ON);
                else if ((config.locations.Find(x => x.Name.Contains("12.06")) != null)
                      || (config.locations.Find(x => x.Name.Contains("12.08")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.18")) != null)
                      || (config.locations.Find(x => x.Name.Contains( "2.19")) != null))
                    SetAudioPreset(PRESET_COMBINE_1_2_ON);
                else
                    SetAudioPreset(1);
            }
            doJoinFeedback();
        }

        public void JoinRoomsAll(Location loc, Location loc2)
        {
            joinState = 4;
            loc.Owner  = loc.Id;
            loc2.Owner = loc.Id;
             //JoinRoomsToggle(loc, loc2); // cheat...
            CrestronConsole.PrintLine("Combining all rooms {0} : {1}", loc.Name, loc.Name);
            SetAudioPreset(PRESET_COMBINE_ALL_ZONE_1);
            SetAudioPreset(PRESET_COMBINE_ALL_ZONE_2);
            doJoinFeedback();
        }
        #endregion
        #region switching functions

        struct RoomRels
        {
            public ushort up;
            public ushort down;
            public ushort rec1;
            public ushort rec2;

            public RoomRels(ushort up, ushort down, ushort rec1, ushort rec2)
            {
                this.up = up;
                this.down = down;
                this.rec1 = rec1;
                this.rec2 = rec2;
            }
        }
        private RoomRels GetRels(string name)
        {
            switch (name)
            {
                case "2.18":
                case "2.20": 
                case "12.06":
                    return new RoomRels(1, 2, 5, 6);
                case "2.19":
                case "2.21":
                case "12.08":
                    return new RoomRels(3, 4, 6, 0);
                case "12.03":
                    return new RoomRels(0, 0, 3, 0);
                case "12.05":
                    return new RoomRels(1, 2, 4, 0);
                case "Theatre":
                case "1.09":
                case "1.12":
                case "1.13":
                case "1.16":
                case "1.17":
                case "1.19":
                case "4.02":
                case "4.06":
                case "4.11":
                case "8.03":
                    return new RoomRels(0, 0, 1, 2);
                case "4.01":
                case "8.02":
                    return new RoomRels(0, 0, 2, 1);
                default:
                    return new RoomRels(0, 0, 0, 0);

            }
            // 2.18-19, 2.20-21 and 12.06-08 (old 2.09-10, 2.11-12 and 12.03-04)
            // rel 1 rm 1 up
            // rel 2 rm 1 dn
            // rel 3 rm 2 up
            // rel 4 rm 2 dn
            // rel 5 rm 1 rec
            // rel 6 rm 2 rec

            // 12.03-05 (old 12.01-02)
            // rel 1 rm 1 up
            // rel 2 rm 1 dn
            // rel 3 rm 1 rec
            // rel 4 rm 2 rec
            //return config.locations[0].Name.Contains("2.18") || config.locations[0].Name.Contains("2.20");
        }
        private bool AllRoomsAreOff()
        {
            if (config.locations.Count < 2)
                return true;
            else
            {
                Location loc = config.locations.Find(x => x.Power == PowerStates.ON);
                return loc == null;
            }
        }
        private void SystemPowerOn(GenericBase currentDevice)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            if (config.locations[ui.location.Id - 1].Power != PowerStates.ON)
                ToggleCountdownSub(currentDevice);
            else
                ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_MAIN].Pulse();
            foreach (Location loc in config.locations)
            {
                if (ui.location.Owner == loc.Owner)
                    config.locations[loc.Id - 1].Power = PowerStates.ON;
            }
            if (IsDmps())
                ((Dmps3SystemControl)SystemControl).SystemPowerOn();
            if (audDspComms != null)
                audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, false);
        }
        private void SystemPowerOff(GenericBase currentDevice)
        {
            UiWithLocation ui = uis.Find(x => x.Device == currentDevice);
            CrestronConsole.PrintLine("SystemPowerOff {0}", ui.location.Id);
            byte owner = ui.location.Owner;
            foreach (Location loc in config.locations)
            {
                if(ui.location.Owner == loc.Owner)
                    config.locations[loc.Id-1].Power = PowerStates.OFF;
            }
            if(ui.location.Id == 1)
                StoreConfig(config, configFileName);
            //((BasicTriList)currentDevice).BooleanInput[DIG_SUB_COUNTDOWN].BoolValue = true;
            foreach(UiWithLocation ui_x in uis)
            {
                if (ui.location.Owner == ui.location.Owner)
                {
                    ((BasicTriList)currentDevice).BooleanInput[DIG_PAGE_SPLASH].Pulse();
                    ClearAvSelectBtns(ui_x);
                }
            }
            //if (SystemControl != null && SystemControl.SystemControlType == eSystemControlType.Dmps3SystemControl) // TODO
            //    ((Dmps3SystemControl)SystemControl).SystemPowerOff();
            int i = 0;
            foreach (Display display in displays)
            {
                if (display != null)
                {
                    //CrestronConsole.PrintLine("display[{0}] != null", i);
                    if (i < config.vidOutputs.Count && config.vidOutputs[i] != null && config.vidOutputs[i].room <= config.locations.Count)
                    {
                        //CrestronConsole.PrintLine("room {0}", config.vidOutputs[i].room);
                        if (config.locations[config.vidOutputs[i].room - 1].Owner == owner)
                        {
                            //CrestronConsole.PrintLine("power off display");
                            display.SetPower(PowerStates.OFF);
                            // relays
                            RoomRels rels = GetRels(config.locations[config.vidOutputs[i].room - 1].Name);
                            if (rels.up > 0 && rels.down > 0)
                            {
                                RelayPorts[rels.down].Open();
                                Thread pulseRel = new Thread(PulseRelDone, RelayPorts[rels.up]);
                            }
                            // switch
                            DoSwitch((byte)0, (byte)(i+1), SwitchType.VIDEO);
                            //DoSwitch((byte)0, (byte)(i+1), SwitchType.AUDIO);
                        }
                    }
                    else
                    {
                    }
                }
                i++;
            }

            //RoomPlusDev rd = config.vidInputs.Find(x => x != null && x.devType == (ushort)VidDev.DOCCAM && x.room);
            i = 0;
            foreach (RoomPlusDev x in config.vidInputs)
            {
                if (x != null && config.locations[x.room - 1].Owner == owner)
                {
                    if (x.devType == (ushort)VidDev.CAM_1 || x.devType == (ushort)VidDev.CAM_2)
                    {
                        if (cams[i] != null)
                            cams[i].PowerOff();
                    }
                    else if(x.devType == (ushort)VidDev.DOCCAM)
                        if (docCams[i] != null)
                            docCams[i].PowerOff(false);
                }
                i++;
            }
            if (AllRoomsAreOff())
            {
                if (IsDmps())
                    ((Dmps3SystemControl)SystemControl).SystemPowerOff();
                if (audDspComms != null)
                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, true);
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
            try
            {
                CrestronConsole.PrintLine("SourceIsRoutedToDest");
                for (uint i = 0; i < config.vidOutputs.Count; i++)
                {
                    if (config.vidOutputs[(int)i] != null && config.vidOutputs[(int)i].selectedForSwitching && (config.vidOutputs[(int)i].currentSourceVid == source))
                        return true;
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("SourceIsRoutedToDest exception: {0}", e.ToString());
            }
            return false;
        }
        public void DoSwitch(byte source, byte dest, SwitchType type)
        {
            try
            {
                if (!IsDmps() && !vidMatrix.Registered && config.locations.Find(x => x.Name == "10-11") == null)
                    RegisterVidMatrix();
                byte destVidMatrix = remapVidMatrixOutputs[dest];
                //CrestronConsole.PrintLine("DoSwitch({0}, {1}, Type {2}), remap {3}, {4}", source, dest, type, destVidMatrix, vidMatrix.Name);
                if (vidMatrix.Outputs == null) // not a DMPS
                    CrestronConsole.PrintLine("{0} {1}line", vidMatrix.Type, vidMatrix.IsOnline ? "on" : "off");
                if (type == SwitchType.VIDEO)
                {
                    config.vidOutputs[destVidMatrix-1].currentSourceVid = (ushort)source;
                    if (source > 0)
                    {
                        if (dest <= config.vidOutputs.Count && config.vidOutputs[dest - 1] != null && source <= config.vidInputs.Count && config.vidInputs[source - 1] != null)
                            CrestronConsole.PrintLine("Routing video Input:{0}({1}) to Output:{2}({3})", source, config.vidInputs[source - 1].devName, destVidMatrix, config.vidOutputs[dest - 1].devName);
                        else
                            CrestronConsole.PrintLine("Routing video Input:{0} to Output:{1}", source, destVidMatrix);
                        if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix) // dm or hd
                        {
                            if (vidMatrix.Outputs[destVidMatrix].VideoOutFeedback == null || vidMatrix.Outputs[destVidMatrix].VideoOutFeedback.Number != source)
                            {
                                CrestronConsole.PrintLine("Sending video route command");
                                vidMatrix.Outputs[destVidMatrix].VideoOut = vidMatrix.Inputs[source]; // don't toggle it
                            }
                            else
                                CrestronConsole.PrintLine("Not sending video route command because device is already on the required input");
                        }
                        if (SwitcherOutputs != null && SwitcherOutputs.Count >= destVidMatrix)
                            ((DMOutput)SwitcherOutputs[destVidMatrix]).VideoOut = ((DMInput)SwitcherInputs[source]); // dmps

                        // switch displays on
                        //CrestronConsole.PrintLine("switch displays[{0}] on", destVidMatrix - 1);
                        if (displays != null && displays[(byte)(destVidMatrix - 1)] != null && displays.Count >= destVidMatrix)
                        {
                            CrestronConsole.PrintLine("{0} display[{1}] SetPower ON", displays[(byte)(destVidMatrix - 1)].GetName(), destVidMatrix - 1);
                            displays[(byte)(destVidMatrix - 1)].SetPower(PowerStates.ON);
                            // source select
                            if ((config.vidOutputs[destVidMatrix - 1].brand == "Panasonic" && !InitialParametersClass.ControllerPromptName.Contains("RMC")) ||
                                 config.vidOutputs[destVidMatrix - 1].brand == "Hitachi" && config.vidOutputs[destVidMatrix - 1].address != null && config.vidOutputs[destVidMatrix - 1].address.Length > 0)
                                displays[(byte)(destVidMatrix - 1)].SetSource(DisplaySources.HDBT_1);
                            else
                                displays[(byte)(destVidMatrix - 1)].SetSource(DisplaySources.HDMI_1);
                            //Lights
                            if (config.vidOutputs[destVidMatrix - 1].room <= config.locations.Count)
                            {
                                switch (config.vidOutputs[destVidMatrix - 1].brand)
                                {
                                    case "Panasonic":
                                    case "Hitachi":
                                        lights.RecallPreset((byte)config.locations[config.vidOutputs[destVidMatrix - 1].room-1].LightArea, (byte)LIGHT_PRESET_LONG_THROW);
                                        break;
                                    case "Epson":
                                        lights.RecallPreset((byte)config.locations[config.vidOutputs[destVidMatrix - 1].room-1].LightArea, (byte)LIGHT_PRESET_SHORT_THROW);
                                        break;
                                }
                                // relays
                                RoomRels rels = GetRels(config.locations[config.vidOutputs[destVidMatrix - 1].room - 1].Name);
                                if (rels.up > 0 && rels.down > 0)
                                {
                                    RelayPorts[rels.up].Open();
                                    Thread pulseRel = new Thread(PulseRelDone, RelayPorts[rels.down]);
                                }
                            }
                        }

                        // turn on associated source device
                        //CrestronConsole.PrintLine("turn on associated source device");
                        if (config.vidInputs[source - 1] != null)
                        {
                            CrestronConsole.PrintLine("config.vidInputs[{0}].devType {1}", source - 1, config.vidInputs[source - 1].devType);
                            switch (config.vidInputs[source - 1].devType)
                            {
                                case (ushort)VidDev.DOCCAM:
                                    //CrestronConsole.PrintLine("docCams[{0}]", source - 1);
                                    if (docCams != null && docCams[source-1] != null)
                                        docCams[source-1].PowerOn(false);
                                    break;
                                case (ushort)VidDev.CAM_1:
                                case (ushort)VidDev.CAM_2:
                                    CrestronConsole.PrintLine("cams[{0}] count {1}", source - 1, cams.Count);
                                    if (cams == null)
                                    { }//CrestronConsole.PrintLine("cams == null");
                                    else if (cams.Count <= source - 1)
                                    { }//CrestronConsole.PrintLine("cams.Count <= source-1");
                                    else if (cams[source - 1] == null)
                                    { }//CrestronConsole.PrintLine("cams[{0}] == null", source - 1);
                                    else
                                    {
                                        //CrestronConsole.PrintLine("cams[{0}].PowerOn()", source - 1);
                                        cams[source - 1].PowerOn();
                                    }
                                    break;
                            }
                        }
                        else
                            CrestronConsole.PrintLine("config.vidInputs[{0}].devType == null", source - 1);
                    }
                    else // clear
                    {
                        if (dest <= config.vidOutputs.Count && config.vidOutputs[dest - 1] != null)
                            CrestronConsole.PrintLine("Clearing video Output:{0}({1})", destVidMatrix, config.vidOutputs[dest - 1].devName);
                        else
                            CrestronConsole.PrintLine("Clearing video Output {0}", destVidMatrix);
                        if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix) // dm or hd
                        {
                            if (vidMatrix.SwitchType.ToString().Contains("HdMd") && vidMatrix.Outputs[destVidMatrix].VideoOutFeedback != null)
                            {
                                //vidMatrix.Outputs[destVidMatrix].VideoOut = vidMatrix.Inputs[source]; // don't toggle it
                                if (vidMatrix.Outputs[destVidMatrix].VideoOutFeedback == null || vidMatrix.Outputs[destVidMatrix].VideoOutFeedback.Number != 4)
                                    vidMatrix.Outputs[destVidMatrix].VideoOut = vidMatrix.Inputs[4]; // go to unused input
                            }
                            else
                                vidMatrix.Outputs[destVidMatrix].VideoOut = null;
                        }
                        if (SwitcherOutputs != null && SwitcherOutputs.Count >= destVidMatrix)
                            ((DMOutput)SwitcherOutputs[destVidMatrix]).VideoOut = null; // dmps
                        CrestronConsole.PrintLine("     source {0}", source);
                    }
                    //CrestronConsole.PrintLine("VideoEnter.Pulse()");
                    if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count >= destVidMatrix && !(vidMatrix is HdMd4x14kE))
                            vidMatrix.VideoEnter.Pulse();
                    //CrestronConsole.PrintLine("DoVidOutFb");
                    DoVidOutFb(source, destVidMatrix);

                    // feedback to buttons, should really be done on the feedback event
                    //CrestronConsole.PrintLine("feedback to buttons");
                    foreach (UiWithLocation ui in uis)
                    {
                        string destStr = String.Empty;
                        if (RoomIsJoined(ui.location) && config.vidOutputs[(ushort)dest - 1].room <= config.locations.Count)
                        {
                            destStr = String.Format("{0}{1}", config.vidOutputs[(ushort)dest - 1].devName,
                                config.vidOutputs[(ushort)dest - 1].devType == (ushort)VidDev.PROJ_1 ||
                                config.vidOutputs[(ushort)dest - 1].devType == (ushort)VidDev.PROJ_2 ||
                                config.vidOutputs[(ushort)dest - 1].devType == (ushort)VidDev.PROJ_3 ||
                                config.vidOutputs[(ushort)dest - 1].devType == (ushort)VidDev.LCD ? " " + config.locations[config.vidOutputs[(ushort)dest - 1].room - 1].Name : "");
                        }
                        else destStr = config.vidOutputs[(ushort)dest - 1].devName;
                        if (source == 0)
                            ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)dest)].StringValue = FormatTextForUi(destStr) + "\r" + FormatTextForUi("No input", FontSizeLine[1], null, "black");
                        else
                            ui.Device.SmartObjects[SMART_ID_DEST_LIST].StringInput[String.Format("Set Item {0} Text", (ushort)dest)].StringValue = FormatTextForUi(destStr) + "\r" + FormatTextForUi(config.vidInputs[(int)(source - 1)].devName, FontSizeLine[1], null, "black");
                    }

                    // if switching to a proj need to switch audio and recording
                    // CrestronConsole.PrintLine("switch audio and recording");
                    switch (config.vidOutputs[destVidMatrix - 1].devType)
                    {
                        case (ushort)VidDev.PROJ_1:
                        case (ushort)VidDev.PROJ_2:
                        case (ushort)VidDev.PROJ_3:
                            byte room = config.vidOutputs[destVidMatrix - 1].room; // get room 
                            //CrestronConsole.PrintLine(" room {0}, proj type {1}", room, config.vidOutputs[destVidMatrix - 1].devType);
                            if (IsDmps())
                            {
                                if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-3"))
                                {
                                    if (room == 1)
                                        DoSwitch(source, (byte)(eDmps300cOutputs.Program), SwitchType.AUDIO);
                                    else if (room == 2)
                                        DoSwitch(source, (byte)(eDmps300cOutputs.Aux1), SwitchType.AUDIO);
                                }
                                else if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-2"))
                                {
                                    if (room == 1)
                                        DoSwitch(source, (byte)(eDmps3200cOutputs.Program), SwitchType.AUDIO);
                                    else if (room == 2)
                                        DoSwitch(source, (byte)(eDmps3200cOutputs.Aux1), SwitchType.AUDIO);
                                }
                                // added 20170524 - DMPS recording wasnt auto switching
                                for (int i = 0; i < config.vidOutputs.Count; i++)
                                { // get audio out for the room - TODO - check joins
                                    if (config.vidOutputs[i] != null && config.vidOutputs[i].room <= config.locations.Count && config.locations[config.vidOutputs[i].room - 1].Owner == config.locations[room - 1].Owner)
                                    {
                                        if (config.vidOutputs[i].devType == (ushort)VidDev.REC_1)
                                        {
                                            CrestronConsole.PrintLine("Record follow projector: source {0}, dest {1}", source, i + 1);
                                            DoSwitch(source, (byte)(i + 1), SwitchType.VIDEO);
                                            DoSwitch(source, (byte)(i + 1), SwitchType.AUDIO);
                                        }
                                    }
                                }
                            }                               
                            else
                            {
                                for(int i = 0; i < config.vidOutputs.Count; i++)
                                { // get audio out for the room - TODO - check joins
                                    if (config.vidOutputs[i] != null && config.vidOutputs[i].room <= config.locations.Count && config.locations[config.vidOutputs[i].room - 1].Owner == config.locations[room - 1].Owner)
                                    {
                                        //CrestronConsole.PrintLine(" config.vidOutputs[i].devType {0}", config.vidOutputs[i].devType);
                                        if (config.vidOutputs[i].devType == (ushort)VidDev.AUDIO ||
                                            (config.vidOutputs[i].devType == (ushort)VidDev.VC && config.locations.Find(x => x.Name.Contains("12.03")) != null))
                                        {
                                            CrestronConsole.PrintLine("Audio follow video: source {0}, dest {1}", source, i+1);
                                            DoSwitch(source, (byte)(i + 1), SwitchType.AUDIO);
                                        }
                                        if (config.vidOutputs[i].devType == (ushort)VidDev.REC_1)
                                        {
                                            CrestronConsole.PrintLine("Record follow projector: source {0}, dest {1}", source, i+1);
                                            DoSwitch(source, (byte)(i + 1), SwitchType.VIDEO);
                                            DoSwitch(source, (byte)(i + 1), SwitchType.AUDIO);
                                        }
                                    }
                                }
                            }
                            break;
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
                    {
                        //CrestronConsole.PrintLine(" vidMatrix switchType {0}", vidMatrix.SwitchType);
                        // avoid InvalidCastException
                        if(vidMatrix.NumberOfInputs == 8)
                            ((DmMd8x8)vidMatrix).AudioEnter.Pulse();
                        else if (vidMatrix.NumberOfInputs == 16)
                            ((DmMd16x16)vidMatrix).AudioEnter.Pulse();
                        else if (vidMatrix.NumberOfInputs == 32)
                            ((DmMd32x32)vidMatrix).AudioEnter.Pulse();
                    }

                    //eisc.UShortInput[EISC_ANA_SRC[dest - 1]].UShortValue = source;
                    //CrestronConsole.PrintLine("     audio Input {0} to Output {1}", source, dest);
                    Thread.Sleep(10);
                    DoAudOutFb(source, dest);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DoSwitch exception: {0}", e.ToString());
                //CrestronConsole.PrintLine("*** source: {0}, dest {1}", source, dest);
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
                    //DoSwitch((byte)src, (byte)(i + 1), SwitchType.AUDIO);
                }
            ClearAvSelectBtns(ui);
        }
        private void sendDestToSelectedSrc(UiWithLocation ui, uint dest)
        {
            CrestronConsole.PrintLine("sendDestToSelectedSrc, dest:{0}", dest);
            DoSwitch(ui.SelectedAvSrc, (byte)dest, SwitchType.VIDEO);
            //DoSwitch(ui.SelectedAvSrc, (byte)dest, SwitchType.AUDIO);
            ClearAvSelectBtns(ui);
        }
        private void DoSourcePress(UiWithLocation ui, uint index)
        {
            CrestronConsole.PrintLine("DoSourcePress, index:{0}", index);
            if (SourceIsRoutedToDest(index))
            {
                if (isAnyDestSelected(ui))
                {
                    sendSrcToSelectedDestinations(ui, index);
                }
            }
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
            //int firstInputOfRoom2 = config.vidInputs.FindIndex(x => x.room == (byte)2);
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

        public void SendDocCamCommand(UiWithLocation ui, uint cmd)
        {
            LumensDocCam dev = docCams.Find(x => x != null);
            if(docCams[ui.CurrentDeviceControl] != null)
                dev = docCams[ui.CurrentDeviceControl];
            if (dev != null)
            {
                switch(cmd)
                {
                    case DIG_DOCCAM_POWER     : dev.PowerToggle(); break;
                    case DIG_DOCCAM_LAMP      : dev.ArmToggle(); break;
                    case DIG_DOCCAM_ZOOM_IN   : dev.ZoomIn(); break;
                    case DIG_DOCCAM_ZOOM_OUT  : dev.ZoomOut(); break;
                    case DIG_DOCCAM_ZOOM_STOP : dev.ZoomStop(); break;
                    case DIG_DOCCAM_FOCUS_IN  : dev.FocusIn(); break;
                    case DIG_DOCCAM_FOCUS_OUT : dev.FocusOut(); break;
                    case DIG_DOCCAM_FOCUS_STOP: dev.FocusStop(); break;
                    case DIG_DOCCAM_AUTOFOCUS :
                    case DIG_DOCCAM_BACKLIGHT :
                    case DIG_DOCCAM_BRIGHT_UP:
                    case DIG_DOCCAM_BRIGHT_DN:
                        break;
                }
            }
        }
        public void SendRecordingCommand(UiWithLocation ui, uint cmd)
        {
            CrestronConsole.PrintLine("SendRecordingCommand {0}", cmd);
            Echo360 dev = recording.Find(x => x != null && !String.IsNullOrEmpty(x.IPAddress));
            int i = 0;
            foreach (Echo360 echo in recording)
            {
                i++;
                if(echo != null)
                {
                    CrestronConsole.PrintLine("echo != null [{0}]", i);
                    if (!String.IsNullOrEmpty(echo.IPAddress))
                    {
                        dev = echo;
                        CrestronConsole.PrintLine("echo IP [{0}]", echo.IPAddress);
                    }
                }
            }
            //if (recording[ui.CurrentDeviceControl] != null)
            //    dev = recording[ui.CurrentDeviceControl];
            if (dev != null)
            {
                CrestronConsole.PrintLine("dev != null");
                switch(cmd)
                {
                    case DIG_RECORD_PAUSE : dev.Pause(); break;
                    case DIG_RECORD_EXTEND: dev.Extend(300); break;
                }
            }
            else
                CrestronConsole.PrintLine("dev == null");
        }
        public void SendCamCommand(UiWithLocation ui, uint cmd)
        {
            CrestronConsole.PrintLine("SendCamCommand [{0}] {1}", ui.CurrentDeviceControl, cmd);
            PanasonicCam dev = cams.Find(x => x != null); // todo - which one?
            if (cams[ui.CurrentDeviceControl] != null)
                dev = cams[ui.CurrentDeviceControl];
            if (dev != null)
            {
                switch(cmd)
                {
                    case DIG_CAM_POWER    : dev.PowerToggle(); break;
                    case DIG_CAM_ZOOM_IN  : dev.ZoomIn();      break;
                    case DIG_CAM_ZOOM_OUT : dev.ZoomOut();     break;
                    case DIG_CAM_ZOOM_STOP: dev.ZoomStop();    break;
                    case DIG_CAM_CANVAS   : dev.PanTiltPad();  break;
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
            // todo
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
            CrestronConsole.PrintLine("cs RampVol {0}", loc);
            loc.RampVol(dir);
            if (joinState == 4)                
                eisc.UShortInput[EISC_ANA_VOL[0]].UShortValue = loc.Volume;
            if (config.locations.Find(x => x.Name == "10-11") != null)
            {
                Display display = displays.Find(x => x != null && x.GetName().Contains("Hitachi"));
                if (display != null)
                    display.SetVolume(loc.Volume);
            }
            else
            {
                foreach (Location location in config.locations)
                    if (location.Owner == loc.Owner)
                    {
                        //CrestronConsole.PrintLine("Ramp location.Owner:{0}, loc.Owner:{1}, location.Id: {2}, loc.Id: {3}", location.Owner, loc.Owner, location.Id, loc.Id);
                        location.SetVolume(loc.Volume);
                        if (audDspComms != null)
                        {
                            int vol = Utils.ConvertRanges(loc.Volume, 0, 100, -40, 10);
                            if (config.locations.Find(x => x.Name.Contains("Theatre")) != null)
                                for (int i = 0; i < progAudioFadersMonoSingleRoomBeforeMics.Length; i++)
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersMonoSingleRoomBeforeMics[i], vol);
                            else if (config.locations.Count < 2)
                            {
                                if (config.locations.Find(x => x.Name.Contains("4.06")) != null)
                                    for (int i = 0; i < progAudioFadersStereoSingleRoomAfter2Mics.Length; i++)
                                        audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomAfter2Mics[i], vol);
                                else
                                    for (int i = 0; i < progAudioFadersStereoSingleRoomBeforeMics.Length; i++)
                                        audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomBeforeMics[i], vol);
                            }
                            else if (config.locations.Find(x => x.Name.Contains("2.12")) != null
                                  || config.locations.Find(x => x.Name.Contains("3.02")) != null
                                  || config.locations.Find(x => x.Name.Contains("3.05")) != null
                                  || config.locations.Find(x => x.Name.Contains("3.13")) != null
                                  || config.locations.Find(x => x.Name.Contains("3.16")) != null
                                  || config.locations.Find(x => x.Name.Contains("8.01")) != null
                                  || config.locations.Find(x => x.Name.Contains("12.03")) != null)
                            {
                                if (location.Id == 1)
                                {
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[0], vol);
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[1], vol);
                                }
                                else
                                {
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[2], vol);
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[3], vol);
                                }
                            }
                            else if (config.locations.Find(x => x.Name.Contains("4.11")) != null
                                || config.locations.Find(x => x.Name.Contains("4.01")) != null
                                || config.locations.Find(x => x.Name.Contains("3.18")) != null)
                            {
                                if (location.Id == 1)
                      
          {
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[0], vol);
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[1], vol);
                                }
                                else
                                {
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[2], vol);
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[3], vol);
                                }
                            }
                            else
                                audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomAfterMics[location.Id - 1], vol);
                        }
                        else if (IsDmps())
                        {
                            uint audOutput = 0;
                            if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-3"))
                            {
                                if (location.Id == 1)
                                    audOutput = (uint)eDmps300cOutputs.Program; // 3
                                else
                                    audOutput = (uint)eDmps300cOutputs.Aux1; // 4
                                CrestronConsole.PrintLine("rampVol: {0}", SwitcherOutputs[(int)eDmps300cOutputs.Program].CardInputOutputType.ToString());
                            }
                            else if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-2"))
                            {
                                if (location.Id == 1)
                                    audOutput = (uint)eDmps3200cOutputs.Program; //  5
                                else
                                    audOutput = (uint)eDmps3200cOutputs.Aux1; // 6
                                CrestronConsole.PrintLine("rampVol: {0}", SwitcherOutputs[(int)eDmps3200cOutputs.Program].CardInputOutputType.ToString());
                            }
                            var output = SwitcherOutputs[audOutput] as Crestron.SimplSharpPro.DM.Cards.Card.Dmps3ProgramOutput;
                            if (output == null)
                                CrestronConsole.PrintLine("output == null");
                            else
                                output.SourceLevel.UShortValue = (ushort)Utils.ConvertRanges(loc.Volume, 0, 100, -500, 0);
                        }
                        else
                        {
                            for (int i = 0; i < config.vidOutputs.Count; i++)
                            {
                                if (displays[i] != null && config.vidOutputs[i] != null && config.vidOutputs[i].room <= config.locations.Count && config.locations[config.vidOutputs[i].room - 1].Owner == location.Owner)
                                {
                                    //CrestronConsole.PrintLine("displays[{0}].SetVolumePercent({1})", i, loc.Volume);
                                    displays[i].SetVolumePercent(loc.Volume);
                                }
                            }
                        }
                    }
            }
            UpdateZoneVolFb(loc);
        }
        public void NudgeVol(Location loc, Direction dir)
        {
            CrestronConsole.PrintLine("NudgeVol loc {0}, dir {1}", loc.Id, dir);
            loc.NudgeVol(dir);
            if (joinState == 4)
                eisc.UShortInput[EISC_ANA_VOL[0]].UShortValue = loc.Volume;
            if (config.locations.Find(x => x.Name == "10-11") != null)
            {
                Display display = displays.Find(x => x != null && x.GetName().Contains("Hitachi"));
                if (display != null)
                    display.SetVolume(loc.Volume);
            }
            else
            {
                foreach (Location location in config.locations)
                    if (location.Owner == loc.Owner)
                    {
                        //CrestronConsole.PrintLine("Nudge location.Owner:{0}, loc.Owner:{1}, location.Id: {2}, loc.Id: {3}", location.Owner, loc.Owner, location.Id, loc.Id);
                        location.SetVolume(loc.Volume);
                        if (audDspComms != null)
                        {
                            int vol = Utils.ConvertRanges(loc.Volume, 0, 100, -40, 10);
                            if (config.locations.Find(x => x.Name.Contains("Theatre")) != null)
                                for (int i = 0; i < progAudioFadersMonoSingleRoomBeforeMics.Length; i++)
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersMonoSingleRoomBeforeMics[i], vol);
                            else if (config.locations.Count < 2)
                            {
                                if (config.locations.Find(x => x.Name.Contains("4.06")) != null)
                                    for (int i = 0; i < progAudioFadersStereoSingleRoomAfter2Mics.Length; i++)
                                        audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomAfter2Mics[i], vol);
                                else
                                    for (int i = 0; i < progAudioFadersStereoSingleRoomBeforeMics.Length; i++)
                                        audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomBeforeMics[i], vol);
                            }
                            else if (config.locations.Find(x => x.Name.Contains("2.12")) != null
                                || config.locations.Find(x => x.Name.Contains("3.02")) != null
                                || config.locations.Find(x => x.Name.Contains("3.05")) != null
                                || config.locations.Find(x => x.Name.Contains("3.13")) != null
                                || config.locations.Find(x => x.Name.Contains("3.16")) != null
                                || config.locations.Find(x => x.Name.Contains("8.01")) != null
                                || config.locations.Find(x => x.Name.Contains("12.03")) != null)
                            {
                                if (location.Id == 1)
                                {
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[0], vol);
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[1], vol);
                                }
                                else
                                {
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[2], vol);
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[3], vol);
                                }
                            }
                            else if (config.locations.Find(x => x.Name.Contains("4.11")) != null
                                || config.locations.Find(x => x.Name.Contains("4.01")) != null
                                || config.locations.Find(x => x.Name.Contains("3.18")) != null)
                            {
                                if (location.Id == 1)
                                {
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[0], vol);
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[1], vol);
                                }
                                else
                                {
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[2], vol);
                                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[3], vol);
                                }
                            }
                            else
                                audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomAfterMics[location.Id - 1], vol);
                        }
                        else if (IsDmps())
                        {
                            uint audOutput = 0;
                            if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-3"))
                            {
                                if (location.Id == 1)
                                    audOutput = (uint)eDmps300cOutputs.Program; // 3
                                else
                                    audOutput = (uint)eDmps300cOutputs.Aux1; // 4
                                CrestronConsole.PrintLine("rampVol: {0}", SwitcherOutputs[(int)eDmps300cOutputs.Program].CardInputOutputType.ToString());
                            }
                            else if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-2"))
                            {
                                if (location.Id == 1)
                                    audOutput = (uint)eDmps3200cOutputs.Program; //  5
                                else
                                    audOutput = (uint)eDmps3200cOutputs.Aux1; // 6
                                CrestronConsole.PrintLine("rampVol: {0}", SwitcherOutputs[(int)eDmps3200cOutputs.Program].CardInputOutputType.ToString());
                            }
                            var output = SwitcherOutputs[audOutput] as Crestron.SimplSharpPro.DM.Cards.Card.Dmps3ProgramOutput;
                            if (output == null)
                                CrestronConsole.PrintLine("output == null");
                            else
                                output.SourceLevel.UShortValue = (ushort)Utils.ConvertRanges(loc.Volume, 0, 100, -500, 0);
                        }
                        else
                        {
                            for (int i = 0; i < config.vidOutputs.Count; i++)
                            {
                                if (displays[i] != null && config.vidOutputs[i] != null && config.vidOutputs[i].room <= config.locations.Count && config.locations[config.vidOutputs[i].room - 1].Owner == location.Owner)
                                {
                                    //CrestronConsole.PrintLine("displays[{0}].SetVolumePercent({1})", i, loc.Volume);
                                    displays[i].SetVolumePercent(loc.Volume);
                                }
                            }
                        }
                    }
            }
            UpdateZoneVolFb(loc);
        }
        public void DoMute(Location loc, PowerStates state)
        {
            CrestronConsole.PrintLine("DoMute loc {0}, state {1}", loc.Id, state);
            bool mute = loc.SetMute(state);
            if (config.locations.Find(x => x.Name == "10-11") != null)
            {
                CrestronConsole.Print("    Hitachi ");
                Display display = displays.Find(x => x.GetName().Contains("Hitachi"));
                if (display != null)
                    display.SetVolMute(state);
            }
            else
            {
                foreach (Location location in config.locations)
                {
                    if (location.Owner == loc.Owner)
                    {
                        CrestronConsole.PrintLine("Mute location.Owner:{0}, loc.Owner:{1}, location.Id: {2}, loc.Id: {3}", location.Owner, loc.Owner, location.Id, loc.Id);
                        location.SetMute(mute);
                        CrestronConsole.PrintLine(" progAudioFadersStereoSingleRoomAfterMics[location.Id({0})-1]: {1}", location.Id, progAudioFadersStereoSingleRoomAfterMics[location.Id - 1]);
                        if (audDspComms != null)
                        {
                            if (config.locations.Find(x => x.Name.Contains("Theatre")) != null)
                                for (int i = 0; i < progAudioFadersMonoSingleRoomBeforeMics.Length; i++)
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersMonoSingleRoomBeforeMics[i], mute);
                            else if (config.locations.Count < 2) 
                            {
                                if (config.locations.Find(x => x.Name.Contains("4.06")) != null)
                                    for (int i = 0; i < progAudioFadersStereoSingleRoomAfter2Mics.Length; i++)
                                        audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomAfter2Mics[i], mute);
                                else
                                    for (int i = 0; i < progAudioFadersStereoSingleRoomBeforeMics.Length; i++)
                                        audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomBeforeMics[i], mute);
                            }
                            else if (config.locations.Find(x => x.Name.Contains("2.12")) != null
                                || config.locations.Find(x => x.Name.Contains("3.02")) != null
                                || config.locations.Find(x => x.Name.Contains("3.05")) != null
                                || config.locations.Find(x => x.Name.Contains("3.13")) != null
                                || config.locations.Find(x => x.Name.Contains("3.16")) != null
                                || config.locations.Find(x => x.Name.Contains("8.01")) != null
                                || config.locations.Find(x => x.Name.Contains("12.03")) != null)
                            {
                                if (location.Id == 1)
                                {
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[0], mute);
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[1], mute);
                                }
                                else
                                {
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[2], mute);
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomBeforeMics[3], mute);
                                }
                            }
                            else if (config.locations.Find(x => x.Name.Contains("4.11")) != null
                                || config.locations.Find(x => x.Name.Contains("4.01")) != null
                                || config.locations.Find(x => x.Name.Contains("3.18")) != null)
                            {
                                if (location.Id == 1)
                                {
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[0], mute);
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[1], mute);
                                }
                                else
                                {
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[2], mute);
                                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoDualRoomAfterMics[3], mute);
                                }
                            }
                            else
                                audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, progAudioFadersStereoSingleRoomAfterMics[location.Id - 1], mute);
                        }
                        else if (IsDmps())
                        {
                            uint audOutput = 0;
                            if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-3"))
                            {
                                for (uint i = 1; i <= NumberOfSwitcherOutputs; i++)
                                {
                                    ICardInputOutputType card = SwitcherOutputs[i];
                                    switch (card.CardInputOutputType)
                                    {
                                        case eCardInputOutputType.Dmps3ProgramOutput:
                                            Card.Dmps3ProgramOutput Dmps3ProgramOutput = (Card.Dmps3ProgramOutput)card;
                                            CrestronConsole.PrintLine("output[{0}] is Dmps3ProgramOutput", i);
                                            break;
                                        case eCardInputOutputType.Dmps3Aux1Output:
                                            Card.Dmps3Aux1Output Dmps3Aux1Output = (Card.Dmps3Aux1Output)card;
                                            CrestronConsole.PrintLine("output[{0}] is Dmps3Aux1Output", i);
                                            break;
                                    }
                                }
                                
                                if (location.Id == 1)
                                    audOutput = (uint)eDmps300cOutputs.Program; // 5
                                else
                                    audOutput = (uint)eDmps300cOutputs.Aux1; // 6
                            }
                            else if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-2"))
                            {
                                if (location.Id == 1)
                                    audOutput = (uint)eDmps3200cOutputs.Program; //  3
                                else
                                    audOutput = (uint)eDmps3200cOutputs.Aux1; // 4
                            }
                            else 
                                audOutput = (uint)1;
                            //var output = SwitcherOutputs[audOutput] as Crestron.SimplSharpPro.DM.Cards.Card.Dmps3ProgramOutput;
                            CrestronConsole.PrintLine("SwitcherOutputs[{0}] {1}", audOutput, SwitcherOutputs[audOutput].CardInputOutputType);
                            var output = SwitcherOutputs[audOutput] as Card.Dmps3ProgramOutput;
                            if (output == null)
                                CrestronConsole.PrintLine("output == null");
                            else
                            {
                                if (mute)
                                   output.SourceMuteOn();
                               else
                                   output.SourceMuteOff();
                            }
                        }
                        else
                        {
                            int i = 0;
                            try
                            {
                                //Display display = displays.Find(x => x != null && x.GetName().Contains("Epson") || x.GetName().Contains("LG"));
                                for (i = 0; i < config.vidOutputs.Count && i < displays.Count; i++)
                                    //if (config.vidOutputs[j] != null && config.vidOutputs[j].room == loc.Owner && (displays[j].GetName().Contains("Epson") || displays[j].GetName().Contains("LG")))
                                    if (displays[i] != null && config.vidOutputs[i] != null && config.vidOutputs[i].room <= config.locations.Count && config.locations[config.vidOutputs[i].room - 1].Owner == location.Owner)
                                        displays[i].SetVolMute(mute ? PowerStates.ON : PowerStates.OFF);
                            }
                            catch (Exception e)
                            {
                                CrestronConsole.PrintLine("DoMute, displays[{0}].SetVolMute. {1}", i, e.Message);
                            }
                        }
                    }
                }
            }
            UpdateZoneMuteFb(loc);
        }
        public void SetMicLevel(Level mic)
        {
            int index = config.mics.FindIndex(x => x == mic);
            CrestronConsole.PrintLine("SetMicLevel:{0}, {1}", index, mic.level);
            if (audDspComms != null)
            {
                int vol = Utils.ConvertRanges(mic.level, 0, 100, -10, 10);
                //if (InitialParametersClass.ControllerPromptName.Contains("RMC")) // single room
                if (config.locations.Find(x => x.Name.Contains("Theatre")) != null)
                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersSingleRoomAfterMonoProgram[index], vol);
                else if (config.locations.Count < 2) // single room
                {
                    if (config.locations.Find(x => x.Name.Contains("4.06")) != null)
                        audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersBeforeMics[index], vol);
                    else
                        audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersSingleRoomAfterStereoProgram[index], vol);
                }
                else if (config.locations.Find(x => x.Name.Contains("12.03")) != null
                    || config.locations.Find(x => x.Name.Contains("8.01")) != null)
                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersDualRoomAfterStereoProgram[index], vol);
                else if (config.locations.Find(x => x.Name.Contains("4.11")) != null
                   || config.locations.Find(x => x.Name.Contains("3.18")) != null)
                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersBeforeMics[index], vol);
                else
                    audDspComms.SetVolume(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFaderDualRoomBeforeAndAfterMonoProgram[index], vol);
            }
            else if (IsDmps())
            {
                uint[] audOutputs = { 0, 0 };
                if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-3"))
                {
                    audOutputs[0] = (uint)eDmps300cOutputs.Program; // 3
                    audOutputs[1] = (uint)eDmps300cOutputs.Aux1; // 4
                }
                else if (InitialParametersClass.ControllerPromptName.Contains("DMPS3-2"))
                {
                    audOutputs[0] = (uint)eDmps3200cOutputs.Program; // 5
                    audOutputs[1] = (uint)eDmps3200cOutputs.Aux1; // 6
                }
                var output0 = SwitcherOutputs[audOutputs[0]] as Crestron.SimplSharpPro.DM.Cards.Card.Dmps3ProgramOutput;
                if (output0 == null)
                    CrestronConsole.PrintLine("output[0] == null");
                else
                    output0.OutputMixer.MicLevel[(uint)index].UShortValue = (ushort)Utils.ConvertRanges(mic.level, 0, 100, -200, 0);
                var output1 = SwitcherOutputs[audOutputs[0]] as Crestron.SimplSharpPro.DM.Cards.Card.Dmps3Aux1Output;
                if (output1 == null)
                    CrestronConsole.PrintLine("output[1] == null");
                else
                    output1.OutputMixer.MicLevel[(uint)index].UShortValue = (ushort)Utils.ConvertRanges(mic.level, 0, 100, -200, 0);
            }
            foreach (UiWithLocation ui in uis)
                ui.Device.SmartObjects[SMART_ID_MICS].UShortInput[String.Format("an_fb{0}", index + 1)].UShortValue = (ushort)(mic.level * 655);
        }
        public void SetMicmute(Level mic)
        {
            int index = config.mics.FindIndex(x => x == mic);
            CrestronConsole.PrintLine("SetMicMute:{0}, {1}, fb{2}", index, mic.mute, index * 3 + 3);
            if (audDspComms != null)
            {
                //if (config.mics[index].room == 1)
                if (config.locations.Find(x => x.Name.Contains("Theatre")) != null)
                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersSingleRoomAfterMonoProgram[index], mic.mute);
                else if (config.locations.Count < 2) // single room
                {
                    if (config.locations.Find(x => x.Name.Contains("4.06")) != null)
                        audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersBeforeMics[index], mic.mute);
                    else
                        audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersSingleRoomAfterStereoProgram[index], mic.mute);
                }
                else if (config.locations.Find(x => x.Name.Contains("12.03")) != null
                    || config.locations.Find(x => x.Name.Contains("8.01")) != null)
                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersDualRoomAfterStereoProgram[index], mic.mute);
                else if (config.locations.Find(x => x.Name.Contains("4.11")) != null
                    || config.locations.Find(x => x.Name.Contains("3.18")) != null)
                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFadersBeforeMics[index], mic.mute);
                else
                    audDspComms.VolMute(config.audioDsp.deviceID, config.audioDsp.gainBlockID, micFaderDualRoomBeforeAndAfterMonoProgram[index], mic.mute);
            }
            else if (IsDmps())
            {
                if (mic.mute)
                    Microphones[(uint)index].MuteOn();
                else
                    Microphones[(uint)index].MuteOff();
            }
            foreach (UiWithLocation ui in uis)
                ui.Device.SmartObjects[SMART_ID_MICS].BooleanInput[String.Format("fb{0}", index * DIG_INC_MICS + DIG_IDX_MIC_MUTE)].BoolValue = mic.mute;

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

                CrestronConsole.PrintLine("IP Address: {0}", CrestronEthernetHelper.GetEthernetParameter(
                    CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS,
                    CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter)));
                CrestronConsole.PrintLine("Mac Address: {0}", CrestronEthernetHelper.GetEthernetParameter(
                    CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_MAC_ADDRESS,
                    CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter)));
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

        public string SendTerminalCommand(string str)
        {
            CrestronConsole.PrintLine("SendTerminalCommand {0}", str);
            string response = String.Empty;
            try
            {
                CrestronConsole.SendControlSystemCommand(str, ref response);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("SendTerminalCommand exception {0}", e.ToString());
            }
            return response;
        }
        public void DoEndOfDayShutdown()
        {
            try
            {
                foreach (Location loc in config.locations)
                {
                    SystemPowerOff(uis.Find(x => x.location == loc).Device);
                }
                Thread.Sleep(10000);
                //SendTerminalCommand("reboot\n");
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DoEndOfDayShutdown exception {0}", e.ToString());
            }
        }
        public void EndOfDayEventCallBack(ScheduledEvent schevent, ScheduledEventCommon.eCallbackReason reason)
        {
            CrestronConsole.PrintLine(schevent.Name + "  shutting down");
            DoEndOfDayShutdown();
        }
        private bool IsDmps()
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
                StoreConfig(config, configFileName);
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("CreateConfig exception {0}", e.ToString());
            }
        }
        private void StoreConfig(object o, string fileName)
        {
            try
            {
                CrestronConsole.PrintLine("StoreConfig: {0}", fileName);
                using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite))
                {
                    fs.Write(JsonConvert.SerializeObject(o, Formatting.Indented), Encoding.ASCII);
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("StoreConfig exception {0}", e.ToString());
            }
        }
        private void RecallConfig(string args)
        {
            config = RecallConfig(configFilePath, string.Format("* System config.json", InitialParametersClass.ProgramIDTag));
        }
        private Config RecallConfig(string filePath, string fileName)
        {
            CrestronConsole.PrintLine("** RecallConfig: {0}{1}{2}",filePath,System.IO.Path.DirectorySeparatorChar, fileName);
            Config data;
            string[] result = Directory.GetFiles(filePath, fileName);
            //foreach (string str in result)
            //    CrestronConsole.PrintLine("{0}", str);
            try
            {
                if (result != null && result.Count() > 0)
                {
                    configFileName = result[0];
                    CrestronConsole.PrintLine("Reading file:: {0}", configFileName);
                    using (FileStream fs = new FileStream(configFileName, FileMode.Open, FileAccess.Read, FileShare.Read))
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
            CrestronConsole.PrintLine("*** configAudioDsp");
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
                        else if (input.devType == (ushort)VidDev.DOCCAM)
                        {
                            docCam = new LumensDocCam(String.Format("DocCam {0}", i));
                            CrestronConsole.PrintLine("Adding {0} DocCam to vidInputs[{1}]", input.brand, i);
                            if (config.locations.Count < 2) // must be a serial port
                            {
                                if (InitialParametersClass.ControllerPromptName.Contains("RMC") &&
                                    config.lights.address == CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter)))
                                {
                                    CrestronConsole.PrintLine("Using IR port for doccam");
                                    if (IROutputPorts[1].Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                                        CrestronConsole.PrintLine("Error registering IR port for doccam");
                                        docCam.SetIRSerialComms(IROutputPorts[1]);
                                }
                                else if (config.locations.Find(x => x.Name == "10-11") == null)
                                {
                                    CrestronConsole.PrintLine("Using Serial port 1 for doccam");
                                    SerialPort comms = new SerialPort(ComPorts[1], String.Format("{0} DocCam {1}", input.brand, i));
                                    docCam.SetComms(comms);
                                }
                            }
                            else if (DmTransmitters == null)
                            {
                                CrestronConsole.PrintLine("DmTransmitters == null");
                                CrestronConsole.PrintLine("Using Serial port 1 for doccam");
                                SerialPort comms = new SerialPort(ComPorts[1], String.Format("{0} DocCam {1}", input.brand, i));
                                docCam.SetComms(comms);
                            }
                            else
                            {
                                CrestronConsole.PrintLine("DmTransmitters != null, DmTransmitters.Count: {0}, config.vidInputs[{1}].room {2}, SwitchType: {3}", DmTransmitters.Count, i, config.vidInputs[i].room, vidMatrix.SwitchType);
                                if (DmTransmitters[i] == null || i >= DmTransmitters.Count || (config.vidInputs[i].room == 1 && vidMatrix.SwitchType.ToString().Contains("DmMd")))
                                {
                                    CrestronConsole.PrintLine("DmTransmitters[{0}] doesn't exist", i);
                                    CrestronConsole.PrintLine("Using Serial port 1 for doccam");
                                    SerialPort comms = new SerialPort(ComPorts[1], String.Format("{0} DocCam {1}", input.brand, i));
                                    docCam.SetComms(comms);
                                }
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
        private void configLights()
        {
            ushort i = 0;
            CrestronConsole.PrintLine("*** configLights");
            lights = new Dynalite("Lights");
            foreach (string str in config.lights.presets)
            {
                i++;
                foreach (UiWithLocation ui in uis)
                {
                    if (str != null && str.Length > 0)
                    {
                        ui.Device.SmartObjects[SMART_ID_LIGHT_PRESETS].BooleanInput[String.Format("Item {0} Visible", i)].BoolValue = true;
                        ui.Device.SmartObjects[SMART_ID_LIGHT_PRESETS].StringInput[String.Format("Set Item {0} Text", i)].StringValue = str;
                    }
                }
            }
            foreach (UiWithLocation ui in uis)
                ui.Device.BooleanInput[DIG_HAS_LIGHTS].BoolValue = true;
            if (config.lights.address != null && config.lights.address.Length > 0)
            {
                CrestronConsole.PrintLine("IP address {0}, Lighting address:{1}", 
                    CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter)),
                    config.lights.address);
                if (CrestronEthernetHelper.GetEthernetParameter(CrestronEthernetHelper.ETHERNET_PARAMETER_TO_GET.GET_CURRENT_IP_ADDRESS, CrestronEthernetHelper.GetAdapterdIdForSpecifiedAdapterType(EthernetAdapterType.EthernetLANAdapter)) ==
                    config.lights.address)
                {
                    CrestronConsole.PrintLine("This is the master lighting control room");
                    uint port = (uint)(InitialParametersClass.ControllerPromptName.Contains("RMC") || config.locations.Find(x => x.Name == "11.21") != null ? 1 : 2); // 4.11 COM-2
                    CrestronConsole.PrintLine("Creating Relay Server for lighting on com port {0}", port);
                    LightsRelayServer = new DynaliteIPServerToSerialPortRelay(ComPorts[port], IP_PORT_LIGHTS, config.lights.address, EthernetAdapterType.EthernetLANAdapter, 30, "Lighting relayer");
                    //LightsRelayServer = new IPServerToSerialPortRelay(ComPorts[port], IP_PORT_LIGHTS, config.lights.address, EthernetAdapterType.EthernetLANAdapter, 30, "Lighting relayer");
                    LightsRelayServer.debugAsHex = true;
                    if (ComPorts[port].Registered)
                    {
                        CrestronConsole.PrintLine("Setting com specs for Relay Server com port: {0}", port);
                        ComPorts[port].SetComPortSpec(ComPort.eComBaudRates.ComspecBaudRate9600,
                                                 ComPort.eComDataBits.ComspecDataBits8,
                                                 ComPort.eComParityType.ComspecParityNone,
                                                 ComPort.eComStopBits.ComspecStopBits1,
                                                 ComPort.eComProtocolType.ComspecProtocolRS232,
                                                 ComPort.eComHardwareHandshakeType.ComspecHardwareHandshakeNone,
                                                 ComPort.eComSoftwareHandshakeType.ComspecSoftwareHandshakeNone,
                                                 false);
                    }
                     CrestronConsole.PrintLine("Baud for Relay Server com port: {0}, {1} ", port, ComPorts[port].BaudRate);
                    
                }
                CrestronConsole.PrintLine("Using {0} for lighting", config.lights.address);
                IPClient comms = new IPClient(config.lights.address, IP_PORT_LIGHTS, "Lights");
                lights.SetComms(comms);
            }
        }
        private void configCams()
        {
            CrestronConsole.PrintLine("*** configCams");
            try
            {
                if (config == null)
                    CrestronConsole.PrintLine("configCams config == null");
                else
                {
                    for (int i = 0; i < config.vidInputs.Count; i++)
                    {
                        RoomPlusDev input = config.vidInputs[i];
                        PanasonicCam cam = null;
                        if (input == null)
                        {
                            cam = null;
                            CrestronConsole.Print(" null");
                        }
                        else if (input.devType == (ushort)VidDev.CAM_1 || input.devType == (ushort)VidDev.CAM_2)
                        {
                            cam = new PanasonicCam(String.Format("Cam {0}", i));
                            CrestronConsole.PrintLine("Adding {0} Cam to vidInputs[{1}]", input.brand, i);
                            cam.SetUrl(input.address);
                            for (byte j = 0; j < config.vidOutputs.Count; j++) // route camera to second feed to recording
                            {
                                if (config.vidOutputs[j] != null && config.vidOutputs[j].devType == (ushort)VidDev.REC_2 && config.vidInputs[i].room == config.vidOutputs[j].room)
                                {
                                    CrestronConsole.PrintLine("routing cam[{0}] to record 2: room {1}", i, config.vidOutputs[j].room);
                                    DoSwitch((byte)(i + 1), (byte)(j + 1), SwitchType.VIDEO);
                                }
                            }
                        }
                        cams.Add(cam);
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("configCams ERROR: {0}", e);
            }
        }
        private void configRecording()
        {
            CrestronConsole.PrintLine("*** configRecording");
            try
            {
                if (config == null)
                    CrestronConsole.PrintLine("configRecording config == null");
                else
                {
                    for (int i = 0; i < config.vidOutputs.Count; i++)
                    {
                        RoomPlusDev output = config.vidOutputs[i];
                        Echo360 dev = null;
                        if (output == null)
                        {
                            dev = null;
                            CrestronConsole.Print(" null");
                        }
                        else if (output.devType == (ushort)VidDev.REC_1)
                        {
                            dev = new Echo360(String.Format("Record"));
                            CrestronConsole.PrintLine("Adding {0} Record to vidOutputs[{1}]", output.brand, i);
                            dev.SetUrl(output.address);
                            dev.IPPort = 8080;
                            dev.Username = RecordUser;
                            dev.Password = RecordPassword;
                            dev.StatusFb += new EchoStatusEventHandler(EchoStatusEventHandler);
                            dev.HardwareFb += new EchoHardwareDetailsEventHandler(EchoHardwareEventHandler);
                            dev.MonitoringFb += new EchoMonitoringEventHandler(EchoMonitorEventHandler);

                        }
                        recording.Add(dev);
                    }
                }
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("configRecording ERROR: {0}", e);
            }
        }
        private void configKeypads()
        {
            CrestronConsole.PrintLine("*** configKeypads");
            kps = new List<CnxB2>();
            if (config.locations.Find(x => x.Name == "11.20") != null)
            {
                CrestronConsole.PrintLine("++ Consult rooms ++");
                for (byte b = 0; b < config.vidInputs.Count; b++)
                {
                    if (config.vidInputs[b] != null && config.vidInputs[b].devType == (ushort)VidDev.CAM_1) // each room has a camera and a keypad
                    {
                        CrestronConsole.PrintLine("++ Creating keypad at {0} ++", CRESNET_KEYPAD_BASE + b);
                        CnxB2 kp = new CnxB2((byte)(CRESNET_KEYPAD_BASE + b), this);
                        kp.ButtonStateChange += new ButtonEventHandler(KpSigChangeHandler);
                        if (kp.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                        {
                            //ErrorLog.Error("kps[{0}] failed registration. Cause: {1}", b, kp.RegistrationFailureReason);
                            CrestronConsole.PrintLine("kps[{0}] failed registration. Cause: {1}", b, kp.RegistrationFailureReason);
                        }
                        kps.Add(kp);
                        kp.OnlineStatusChange += new OnlineStatusChangeEventHandler(KpOnlineHandler);
                    }
                }
            }
        }
        private void configUis()
        {
            CrestronConsole.PrintLine("*** Config UIs");
            //RemapDestList();
            uis = new List<UiWithLocation>(); // location to be set on config load
            for (byte b = 1; b <= config.locations.Count; b++)
            {
                UiWithLocation ui = new UiWithLocation(b, (byte)(IPID_UIS_BASE + b - 1), config.locations.Find(x => x.Id == b), this); // id, ipid, cs
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
                    ui.Device.LoadSmartObjects(Directory.GetApplicationDirectory() + System.IO.Path.DirectorySeparatorChar + "Navitas TSW-750 v2.3.sgd");
                    //CrestronConsole.AddNewConsoleCommand(initUi1, "InitUi1", "", ConsoleAccessLevelEnum.AccessOperator);
                    ui.Device.SmartObjects[SMART_ID_KEYPAD].SigChange += new SmartObjectSigChangeEventHandler(PasswordEventHandler);
                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_0].SigChange += new SmartObjectSigChangeEventHandler(SourceSelectLocalEventHandler);
                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_0].SigChange += new SmartObjectSigChangeEventHandler(SourceSelectLoc00EventHandler);
                    ui.Device.SmartObjects[SMART_ID_SOURCE_LIST_JOINED_1].SigChange += new SmartObjectSigChangeEventHandler(SourceSelectLoc01EventHandler);
                    ui.Device.SmartObjects[SMART_ID_DEST_LIST].SigChange += new SmartObjectSigChangeEventHandler(DestSelectEventHandler);
                    ui.Device.SmartObjects[SMART_ID_MICS].SigChange += new SmartObjectSigChangeEventHandler(MicsEventHandler);
                    ui.Device.SmartObjects[SMART_ID_CAMERA_DPAD].SigChange += new SmartObjectSigChangeEventHandler(CamDPadEventHandler);
                    ui.Device.SmartObjects[SMART_ID_CAMERA_PRESETS].SigChange += new SmartObjectSigChangeEventHandler(CamPresetEventHandler);
                    ui.Device.SmartObjects[SMART_ID_LIGHT_PRESETS].SigChange += new SmartObjectSigChangeEventHandler(LightPresetEventHandler);
                    ui.Device.SmartObjects[SMART_ID_PROJ_POWER].SigChange += new SmartObjectSigChangeEventHandler(ProjPowerButtonEventHandler);
                    ui.Device.SmartObjects[SMART_ID_PROJ_IMAGE_MUTE].SigChange += new SmartObjectSigChangeEventHandler(ProjPicMuteButtonEventHandler);
                }
                catch (Exception e)
                {
                    ErrorLog.Error("LoadSmartObjects exception: {0}", e.ToString());
                    CrestronConsole.PrintLine("LoadSmartObjects exception: {0}", e.ToString());
                }
            }
        }
        private void configEisc()
        {
            CrestronConsole.PrintLine("*** config EISC");
            if (String.IsNullOrEmpty(config.remoteRoomIp))
                config.remoteRoomIp = "127.0.0.2";
            eisc = new ThreeSeriesTcpIpEthernetIntersystemCommunications(IPID_EISC, config.remoteRoomIp, this);
            eisc.SigChange += new SigEventHandler(EiscSigChangeHandler);
            if (eisc.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
            {
                ErrorLog.Error("eisc failed registration. Cause: {0}", eisc.RegistrationFailureReason);
                CrestronConsole.PrintLine("eisc failed registration. Cause: {0}", eisc.RegistrationFailureReason);
            }
            eisc.OnlineStatusChange += new OnlineStatusChangeEventHandler(CommonOnlineHandler);
        }
        private void configSchedules()
        {
            CrestronConsole.PrintLine("*** configSchedules");
            try
            {
                //endOfDaySched.Recurrence.Weekly(ScheduledEventCommon.eWeekDays.Monday | ScheduledEventCommon.eWeekDays.Wednesday | ScheduledEventCommon.eWeekDays.Friday);
                var endOfDayDateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                                    22, 0, 0); // 10:00pm
                if (endOfDayDateTime < DateTime.Now)
                    endOfDayDateTime = endOfDayDateTime.AddDays(1);
                endOfDayEvent = new ScheduledEvent("endOfDayEvent", new ScheduledEventGroup("endOfDayEventGroup"));
                if (endOfDayEvent.DateAndTime.SetAbsoluteEventTime(endOfDayDateTime.Year, endOfDayDateTime.Month, endOfDayDateTime.Day, endOfDayDateTime.Hour, endOfDayDateTime.Minute) == ScheduledEventCommon.eResultCodes.Success)
                    //if (endOfDayEvent.DateAndTime.SetAbsoluteEventTime(endOfDayDateTime) == ScheduledEventCommon.eResultCodes.Success)
                {
                    endOfDayEvent.Recurrence.Daily();
                    endOfDayEvent.UserCallBack += EndOfDayEventCallBack;
                    ScheduledEventCommon.eResultCodes res = endOfDayEvent.Enable();
                    CrestronConsole.PrintLine("endOfDayEvent" + " Enable: " + res);
                }
                else
                    CrestronConsole.PrintLine("Error setting daily recurrance");
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("Error creating endOfDay event.\n{0}", e);
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
                        //CrestronConsole.Print(".{0}", i);
                        RoomPlusDev output = config.vidOutputs[i];
                        //CrestronConsole.PrintLine("  output {0}", i);
                        Display display;
                        if (output == null)
                        {
                            display = null;
                            //CrestronConsole.Print(" null");
                        }
                        else 
                        {
                            IPClient ip = null;
                            SerialPort ser = null;
                            switch(output.brand)
                            {
                                case "Panasonic":
                                    if (output.address != null && output.address.Length > 0)
                                    {
                                        display = new PanasonicProj(String.Format("{0} Projector [{1}]", output.brand, i));
                                        CrestronConsole.PrintLine("Adding {0} projector at {1} to output [{2}]", output.brand, output.address, i);
                                        ip = new IPClient(output.address, display.DEFAULT_IP_PORT, String.Format("{0} Projector {1}", output.brand, i));
                                        display.SetComms(ip); // Panasonic port 1024
                                    }
                                    else
                                        display = null;
                                    break;
                                case "Epson":
                                    display = new EpsonProj(String.Format("{0} Projector [{1}]", output.brand, i));
                                    CrestronConsole.PrintLine("Adding {0} projector to output {1}", output.brand, i);
                                    if (output.address != null && output.address.Length > 0)
                                    {
                                        ip = new IPClient(output.address, 3629, String.Format("{0} Projector {1}", output.brand, i));
                                        display.SetComms(ip); // Epson port 362
                                    }
                                    else if (DmReceivers == null)
                                    {
                                        CrestronConsole.PrintLine("DmReceivers == null");
                                        uint comIndex = 1;
                                        CrestronConsole.PrintLine("adding {0} projector[{1}] to ComPorts[{2}]", output.brand, i, comIndex);
                                        ser = new SerialPort(ComPorts[comIndex], String.Format("{0} Projector {1}", output.brand, i));
                                        display.SetComms(ser);
                                    }
                                    else
                                    {
                                        CrestronConsole.PrintLine("DmReceivers != null, [{0}], DmReceivers.Count: {1}", i, DmReceivers.Count);
                                        if (DmReceivers[i] == null || i >= DmReceivers.Count)
                                        {
                                            CrestronConsole.PrintLine("DmReceivers[{0}] doesn't exist", i);
                                            uint comIndex = 1;
                                            CrestronConsole.PrintLine("adding {0} projector to ComPorts[{1}]", output.brand, comIndex);
                                            ser = new SerialPort(ComPorts[comIndex], String.Format("{0} Projector {1}", output.brand, i));
                                            display.SetComms(ser);
                                        }
                                        else
                                        {
                                            CrestronConsole.PrintLine("DmReceivers[{0}] != null, registered:{1}, online:{2}", i, DmReceivers[i].Registered, DmReceivers[i].IsOnline);
                                            //EndpointReceiverBase rx = (DmRmc4k100C)DmReceivers[i];
                                            DmRmc4k100C rx = (DmRmc4k100C)DmReceivers[i];
                                            if (rx.Registered)
                                            {
                                                int comIndex = 1;
                                                CrestronConsole.PrintLine("adding rx[{0}].ComPorts[{1}] to 0x{2:X2} {3}", i, comIndex, rx.ID, output.brand);
                                                ser = new SerialPort(rx.ComPorts[1], String.Format("{0} Projector {1}", output.brand, i));
                                                display.SetComms(ser);
                                            }
                                            else
                                                CrestronConsole.PrintLine("DmReceivers[{0}]: 0x{1:X2}, not registered", i, rx.ID);
                                        }
                                    }
                                    break;
                                case "LG":
                                    display = new LgMonitor(String.Format("{0} LCD [{1}]", output.brand, i));
                                    CrestronConsole.PrintLine("Adding {0} LCD to output {1}", output.brand, i);
                                    if (DmReceivers[i] == null || i >= DmReceivers.Count)
                                    {
                                        CrestronConsole.PrintLine("DmReceivers[{0}] doesn't exist", i);
                                    }
                                    else
                                    {
                                        CrestronConsole.PrintLine("DmReceivers[{0}] != null, registered:{1}, online:{2}", i, DmReceivers[i].Registered, DmReceivers[i].IsOnline);
                                        //EndpointReceiverBase rx = (DmRmc4k100C)DmReceivers[i];
                                        DmRmc4k100C rx = (DmRmc4k100C)DmReceivers[i];
                                        if (rx.Registered)
                                        {
                                            int comIndex = 1;
                                            CrestronConsole.PrintLine("adding rx[{0}].ComPorts[{1}] to 0x{2:X2} {3}", i, comIndex, rx.ID, output.brand);
                                            ser = new SerialPort(rx.ComPorts[1], String.Format("{0} LCD {1}", output.brand, i));
                                            display.SetComms(ser);
                                        }
                                        else
                                            CrestronConsole.PrintLine("DmReceivers[{0}]: 0x{1:X2}, not registered", i, rx.ID);
                                    }
                                    break;
                                case "Hitachi":
                                    display = new HitachiProj(String.Format("{0} Projector [{1}]", output.brand, i));
                                    CrestronConsole.PrintLine("Adding {0} projector to output {1}", output.brand, i);
                                    if (output.address != null && output.address.Length > 0)
                                    {
                                        ip = new IPClient(output.address, 23, String.Format("{0} Projector {1}", output.brand, i));
                                        display.SetComms(ip); // Hitachi port ??
                                    }
                                    //else if (DmReceivers == null)
                                    //    CrestronConsole.PrintLine("DmReceivers == null");
                                    else
                                    {
                                        uint comIndex = 1;
                                        CrestronConsole.PrintLine("adding {0} projector to ComPorts[{1}]", output.brand, comIndex);
                                        ser = new SerialPort(ComPorts[comIndex], String.Format("{0} Projector {1}", output.brand, i));
                                        display.SetComms(ser);
                                    }
                                    break;
                                default:
                                    //display = new Display(String.Format("{0} {1}", output.brand, i));
                                    display = null;
                                    break;
                            }
                        }
                        displays.Add(display);
                        if (display != null)
                        {
                            display.PowerFb += new Display.PowerEventHandler(ProjPowerEventHandler);
                            display.SourceFb += new Display.DisplaySourceEventHandler(ProjSourceEventHandler);
                        }
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
                else if (InitialParametersClass.ControllerPromptName.Contains("RMC")) // has a Hd-Md4x1-4k-e
                {
                    vidMatrix = new HdMd4x14kE(IPID_VIDMATRIX, config.vidSwitcherIpAddress, this);
                    CrestronConsole.PrintLine("Type {0}", vidMatrix.SwitchType);
                    RegisterVidMatrix();
                    RegisterVidMatrixEventHandlers();
                }
                else// if (InitialParametersClass.ControllerPromptName.Contains("CP3"))
                {
                    if (config.vidInputs.Count() > 8 || config.vidOutputs.Count() > 8)
                        vidMatrix = new DmMd16x16(IPID_VIDMATRIX, this);
                    else
                        vidMatrix = new DmMd8x8(IPID_VIDMATRIX, this);
                    CrestronConsole.PrintLine("Type {0}", vidMatrix.SwitchType);
                    DmInputInit();
                    DmOutputInit();
                    RegisterVidMatrix();
                    RegisterVidMatrixEventHandlers();
                    if (vidMatrix.Registered)
                        DmEndPointsInit();
                    else
                        CrestronConsole.PrintLine("Registering endpoints FAILED, vidMatrix not registered");
                }
                CrestronConsole.PrintLine("* ConfigSwitcher done");
            }
            catch (Exception e)
            {
                CrestronConsole.PrintLine("ConfigSwitcher exception: {0}", e.ToString());
            }
        }
        private void RegisterVidMatrix()
        {
            if (config.vidSwitcherIpAddress != null && config.vidSwitcherIpAddress.Length > 0)
                CrestronConsole.PrintLine("RegisterVidMatrix {0} at {1}", vidMatrix.SwitchType, config.vidSwitcherIpAddress);
            else
                CrestronConsole.PrintLine("RegisterVidMatrix {0} at ID {1}", vidMatrix.SwitchType, vidMatrix.ID);
            if (config.locations.Find(x => x.Name == "10-11") == null)
            {
                if (vidMatrix.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    CrestronConsole.PrintLine("vidMatrix failed registration. Cause: {0}", vidMatrix.RegistrationFailureReason);
                else
                {
                    CrestronConsole.PrintLine("vidMatrix registration success");
                    vidMatrix.OnlineStatusChange += new OnlineStatusChangeEventHandler(CommonOnlineHandler);
                }
            }
        }
        private void RegisterVidMatrixEventHandlers()
        {
            try
            {
                CrestronConsole.PrintLine("RegisterVidMatrixEventHandlers");
                if (IsDmps())
                {
                    CrestronConsole.PrintLine("DMInputEventHandler");
                    DMInputChange += new DMInputEventHandler(vidMatrixInputChange);
                    CrestronConsole.PrintLine("DMOutputEventHandler");
                    DMOutputChange += new DMOutputEventHandler(vidMatrixOutputChange);
                    CrestronConsole.PrintLine("DMSystemEventHandler");
                    DMSystemChange += new DMSystemEventHandler(vidMatrixSystemChange);
                    CrestronConsole.PrintLine("done");
                }
                else if (config.locations.Find(x => x.Name == "10-11") == null)
                        //if (vidMatrix.Outputs != null && vidMatrix.Outputs.Count > 0)
                {
                    CrestronConsole.PrintLine(" EventHandlers for {0}", vidMatrix.SwitchType);
                    vidMatrix.DMInputChange += new DMInputEventHandler(vidMatrixInputChange);
                    vidMatrix.DMOutputChange += new DMOutputEventHandler(vidMatrixOutputChange);
                    vidMatrix.DMSystemChange += new DMSystemEventHandler(vidMatrixSystemChange);
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
                CrestronConsole.PrintLine("DmInputInit");
                List<CardDevice> inputCards = new List<CardDevice>();
                DmTransmitters = new List<DmHDBasedTEndPoint>();
                int maxInputs = config.vidInputs.Count < vidMatrix.Inputs.Count ? config.vidInputs.Count : vidMatrix.Inputs.Count;
                for (byte b = 0; b < maxInputs; b++)
                {
                    DmHDBasedTEndPoint tx = null;
                    if (config.vidInputs[b] != null)
                    {
                        Dmc4kC card = new Dmc4kC((uint)b + 1, vidMatrix);
                        inputCards.Add(card);
                        string name = String.Format("Input {0}", b+1);
                        if (b < config.vidInputs.Count && config.vidInputs[b] != null)
                        {
                            if (config.vidInputs[b].room <= config.locations.Count)
                            {
                                if (config.locations[config.vidInputs[b].room - 1] != null)
                                    name = String.Format("{0}{1}", config.locations.Count > 1 ? config.locations[config.vidInputs[b].room - 1].Name + " " : "", config.vidInputs[b].devName);
                            }
                            else
                                name = String.Format("{0}", config.vidInputs[b].devName);
                        }
                        CrestronConsole.PrintLine("[{0}] {1}", b, name);
                        vidMatrix.Inputs[(uint)(b + 1)].Name.StringValue = name;
                        if (config.vidInputs[b].devType == (ushort)VidDev.DOCCAM)
                            tx = new DmTx4K100C1G((uint)(IPID_DMTX_BASE + b), vidMatrix.Inputs[(uint)b + 1]);
                    }
                    else
                        CrestronConsole.PrintLine("config.vidInputs[{0}] == null", b);
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
            try
            {
                CrestronConsole.PrintLine("DmpsInputInit");
                if (SwitcherInputs == null)
                    CrestronConsole.PrintLine("SwitcherInputs == null");
                else
                    CrestronConsole.PrintLine("SwitcherInputs.Count: {0}", SwitcherInputs.Count);
                DmTransmitters = new List<DmHDBasedTEndPoint>();
                for (uint i = 1; i <= SwitcherInputs.Count; i++)
                {
                    DmHDBasedTEndPoint tx = null;
                    ICardInputOutputType input = SwitcherInputs[i];
                    string name = String.Format("Input {0}", i);
                    try
                    {
                        if (i <= config.vidInputs.Count && config.vidInputs[(int)(i - 1)] != null)
                        {
                            if (config.vidInputs[(int)(i - 1)].room <= config.locations.Count)
                            {
                                if (config.locations[config.vidInputs[(int)(i - 1)].room - 1] != null)
                                    name = String.Format("{0}{1}", config.locations.Count > 1 ? config.locations[config.vidInputs[(int)(i - 1)].room - 1].Name + " " : "", config.vidInputs[(int)(i - 1)].devName);
                            }
                            else // colaboration
                                name = String.Format("{0}", config.vidInputs[(int)(i - 1)].devName);
                        }
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine("DmpsInputInit name {0} exception {1}", i, e.Message);
                    }
                    CrestronConsole.PrintLine("{0} input card Type: {1}, name {2}", i, input.CardInputOutputType, name);
                    switch (input.CardInputOutputType)
                    {
                        case eCardInputOutputType.Dmps3HdmiInput:
                        case (eCardInputOutputType.Dmps3HdmiInputWithoutAnalogAudio):
                            Card.Dmps3HdmiInputWithoutAnalogAudio Dmps3HdmiInput = (Card.Dmps3HdmiInputWithoutAnalogAudio)input;
                            Dmps3HdmiInput.HdmiInputPort.StreamCec.CecChange += new CecChangeEventHandler(Dmps3HdmiInputStreamCec_CecChange);
                            if (i <= config.vidInputs.Count && config.vidInputs[(int)(i - 1)] != null)
                                Dmps3HdmiInput.Name.StringValue = name;
                            break;
                        case eCardInputOutputType.Dmps3HdmiVgaInput:
                            Card.Dmps3HdmiVgaInput Dmps3HdmiVgaInput = (Card.Dmps3HdmiVgaInput)input;
                            Dmps3HdmiVgaInput.HdmiInputPort.StreamCec.CecChange += new CecChangeEventHandler(Dmps3HdmiInputStreamCec_CecChange);
                            Dmps3HdmiVgaInput.VgaInputPort.VideoAttributes.AttributeChange += new GenericEventHandler(VgaDviInputPortVideoAttributesBasic_AttributeChange);
                            Dmps3HdmiVgaInput.VgaInputPort.VideoControls.ControlChange += new GenericEventHandler(VgaDviInputPortVideoControlsBasic_ControlChange);
                            if (i <= config.vidInputs.Count && config.vidInputs[(int)(i - 1)] != null)
                                Dmps3HdmiVgaInput.Name.StringValue = name;
                            break;
                        case eCardInputOutputType.Dmps3HdmiVgaBncInput:
                            Card.Dmps3HdmiVgaBncInput Dmps3HdmiVgaBncInput = (Card.Dmps3HdmiVgaBncInput)input;
                            Dmps3HdmiVgaBncInput.HdmiInputPort.StreamCec.CecChange += new CecChangeEventHandler(Dmps3HdmiInputStreamCec_CecChange);
                            Dmps3HdmiVgaBncInput.VgaInputPort.VideoAttributes.AttributeChange += new GenericEventHandler(VgaDviInputPortVideoAttributesBasic_AttributeChange);
                            Dmps3HdmiVgaBncInput.VgaInputPort.VideoControls.ControlChange += new GenericEventHandler(VgaDviInputPortVideoControlsBasic_ControlChange);
                            Dmps3HdmiVgaBncInput.BncInputPort.VideoAttributes.AttributeChange += new GenericEventHandler(BncInputPortVideoAttributes_AttributeChange);
                            Dmps3HdmiVgaBncInput.BncInputPort.VideoControls.ControlChange += new GenericEventHandler(BncInputPortVideoControls_ControlChange);
                            if (i <= config.vidInputs.Count && config.vidInputs[(int)(i - 1)] != null)
                                Dmps3HdmiVgaBncInput.Name.StringValue = name;
                            break;
                        case eCardInputOutputType.Dmps3DmInput:
                            Card.Dmps3DmInput Dmps3DmInput = (Card.Dmps3DmInput)input;
                            Dmps3DmInput.DmInputPort.VideoAttributes.AttributeChange += new GenericEventHandler(DmInputPortVideoAttributes_AttributeChange);
                            if (i <= config.vidInputs.Count && config.vidInputs[(int)(i - 1)] != null)
                                Dmps3DmInput.Name.StringValue = name;
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
            catch (Exception e)
            {
                CrestronConsole.PrintLine("DmInputInit exception: {0}\n{1}", e.Message, e.ToString());
            }
       }
        private void DmOutputInit()
        {
            CrestronConsole.PrintLine("DmOutputInit");
            List<DmOutputModuleBase> outputCards = new List<DmOutputModuleBase>();
            DmReceivers = new List<EndpointReceiverBase>();
            CrestronConsole.PrintLine("vidMatrix.Outputs.count {0}", vidMatrix.Inputs.Count);
            for (byte b = 0; b < vidMatrix.Outputs.Count / 2; b++) // output cards (2 slots each)
            {
                DmOutputModuleBase outputCard = new Dmc4kCoHdSingle((uint)b + 1, vidMatrix); // 3:HD,5:DM,8:DM4K
                outputCards.Add(outputCard);
            }
            for (byte b = 0; b < config.vidOutputs.Count && b < vidMatrix.Outputs.Count; b++) // output slots
            {
                //CrestronConsole.PrintLine("output {0}",b);
                string name = String.Format("Output {0}", b+1);
                if(b < config.vidOutputs.Count && config.vidOutputs[b] != null)
                {
                    if (config.vidOutputs[b].room <= config.locations.Count)
                    {
                        if (config.locations[config.vidOutputs[b].room - 1] != null)
                            name = String.Format("{0}{1}", config.locations.Count > 1 ? config.locations[config.vidOutputs[b].room - 1].Name + " " : "", config.vidOutputs[b].devName);
                    }
                    else
                        name = String.Format("{0}", config.vidOutputs[b].devName);
                }
                CrestronConsole.PrintLine("[{0}] {1}", b, name);
                EndpointReceiverBase rx = null;
                if (config.vidOutputs[b] != null)
                {
                    vidMatrix.Outputs[(uint)(b + 1)].Name.StringValue = name;
                    if (config.vidOutputs[b].brand == "Epson" || config.vidOutputs[b].brand == "LG")
                        rx = new DmRmc4k100C((uint)IPID_DMRX_BASE + b, vidMatrix.Outputs[(uint)b + 1]);
                }
                DmReceivers.Add(rx);
            }
            CrestronConsole.PrintLine("DmOutputInit done");
        }
        private void DmpsOutputInit(string str)
        {
            DmpsOutputInit(); 
        }
        private void DmpsOutputInit()
        {
            CrestronConsole.PrintLine("DmpsOutputInit ({0})", NumberOfSwitcherOutputs);
            if (SwitcherInputs == null)
                CrestronConsole.PrintLine("SwitcherOutputs == null");
            else
                CrestronConsole.PrintLine("SwitcherOutputs.Count: {0}", SwitcherOutputs.Count);
            try
            {
                DmReceivers = new List<EndpointReceiverBase>();
                for (uint i = 1; i <= SwitcherOutputs.Count; i++)
                {
                    DmRmc4k100C rx = null;
                    ICardInputOutputType output = SwitcherOutputs[i];
                    string name = String.Format("Output {0}", i);
                    try
                    {
                        if (i <= config.vidOutputs.Count && config.vidOutputs[(int)(i - 1)] != null)
                        {
                            if(config.vidOutputs[(int)(i - 1)].room <= config.locations.Count)
                            {
                                if (config.locations[config.vidOutputs[(int)(i - 1)].room - 1] != null)
                                    name = String.Format("{0}{1}", config.locations.Count > 1 ? config.locations[config.vidOutputs[(int)(i - 1)].room - 1].Name + " " : "", config.vidOutputs[(int)(i - 1)].devName);
                            }
                            else
                                name = String.Format("{0}", config.vidOutputs[(int)(i - 1)].devName);
                        }
                    }
                    catch (Exception e)
                    {
                        CrestronConsole.PrintLine("DmpsOutputInit name {0} exception {1}", i, e.Message);
                    }
                    CrestronConsole.PrintLine("{0} output card Type: {1}, Name: {2}", i, output.CardInputOutputType, name);
                    switch (output.CardInputOutputType)
                    {
                        case eCardInputOutputType.Dmps3HdmiOutput:
                            Card.Dmps3HdmiOutput Dmps3HdmiOutput = (Card.Dmps3HdmiOutput)output;
                            Dmps3HdmiOutput.HdmiOutputPort.StreamCec.CecChange += new CecChangeEventHandler(Dmps3HdmiOutputStreamCec_CecChange);
                            Dmps3HdmiOutput.HdmiOutputPort.ConnectedDevice.DeviceInformationChange += new ConnectedDeviceChangeEventHandler(Dmps3HdmiOutputConnectedDevice_DeviceInformationChange);
                            if (i <= config.vidOutputs.Count && config.vidOutputs[(int)(i - 1)] != null)
                                Dmps3HdmiOutput.Name.StringValue = config.vidOutputs[(int)(i - 1)].devName;
                            break;
                        case eCardInputOutputType.Dmps3DmOutput:
                            Card.Dmps3DmOutput Dmps3DmOutput = (Card.Dmps3DmOutput)output;
                            rx = new DmRmc4k100C((uint)IPID_DMRX_BASE + i - 1, (DMOutput)SwitcherOutputs[i]);
                            Dmps3DmOutput.DmOutputPort.ConnectedDevice.DeviceInformationChange += new ConnectedDeviceChangeEventHandler(Dmps3DmOutputConnectedDevice_DeviceInformationChange);
                            if (i <= config.vidOutputs.Count && config.vidOutputs[(int)(i - 1)] != null)
                                Dmps3DmOutput.Name.StringValue = config.vidOutputs[(int)(i - 1)].devName;
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
                //CrestronConsole.PrintLine("DmReceivers {0}", DmReceivers.Count);
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
                    CrestronConsole.PrintLine("Registering receivers ({0})", DmReceivers.Count);
                    for (int i = 0; i < DmReceivers.Count && i < config.vidOutputs.Count; i++)
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
                    for (int i = 0; i < DmTransmitters.Count && i < config.vidInputs.Count; i++)
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
        private void CreateMappingDictionaries()
        {
            _dmSystemEventIdsMapping = new Dictionary<uint, string>
            {
                {DMSystemEventIds.NA, "DMSystemEventIds.NA"},
                {DMSystemEventIds.PasswordModeEventId, "DMSystemEventIds.PasswordModeEventId"},
                {DMSystemEventIds.SystemTemperatureMonitor1EventId, "DMSystemEventIds.SystemTemperatureMonitor1EventId"},
                {DMSystemEventIds.AudioBreakawayEventId, "DMSystemEventIds.AudioBreakawayEventId"},
                {DMSystemEventIds.SavingSettingsEventId, "DMSystemEventIds.SavingSettingsEventId"},
                {DMSystemEventIds.USBBreakawayEventId, "DMSystemEventIds.USBBreakawayEventId"},
                {DMSystemEventIds.SystemIdBusyEventId, "DMSystemEventIds.SystemIdBusyEventId"},
                {DMSystemEventIds.SystemIdEventId, "DMSystemEventIds.SystemIdEventId"},
                {DMSystemEventIds.USBHIDMessageEventId, "DMSystemEventIds.USBHIDMessageEventId"},
                {DMSystemEventIds.FrontPanelLockOffEventId, "DMSystemEventIds.FrontPanelLockOffEventId"},
                {DMSystemEventIds.FrontPanelLockOnEventId, "DMSystemEventIds.FrontPanelLockOnEventId"},
                {DMSystemEventIds.RedundantPowerSupplyOKFeedbackEventId, "DMSystemEventIds.RedundantPowerSupplyOKFeedbackEventId"},
                {DMSystemEventIds.FrontPanelPasswordEventId, "DMSystemEventIds.FrontPanelPasswordEventId"},
                {DMSystemEventIds.SystemPowerOffEventId, "DMSystemEventIds.SystemPowerOffEventId"},
                {DMSystemEventIds.SystemPowerOnEventId, "DMSystemEventIds.SystemPowerOnEventId"},
                {DMSystemEventIds.PnmModeEventId, "DMSystemEventIds.PnmModeEventId"},
                {DMSystemEventIds.SystemPowerButtonEventId, "DMSystemEventIds.SystemPowerButtonEventId"},
                {DMSystemEventIds.FrontPanelBrightnessLowEventId, "DMSystemEventIds.FrontPanelBrightnessLowEventId"},
                {DMSystemEventIds.FrontPanelBrightnessMediumEventId, "DMSystemEventIds.FrontPanelBrightnessMediumEventId"},
                {DMSystemEventIds.FrontPanelBrightnessHighEventId, "DMSystemEventIds.FrontPanelBrightnessHighEventId"}
            };

            _dmInputEventIdsMapping = new Dictionary<uint, string>
            {
                {DMInputEventIds.NA, "DMInputEventIds.NA"},
                {DMInputEventIds.VideoOutEventId, "DMInputEventIds.VideoOutEventId"},
                {DMInputEventIds.InputNameEventId, "DMInputEventIds.InputNameEventId"},
                {DMInputEventIds.AudioOutEventId, "DMInputEventIds.AudioOutEventId"},
                {DMInputEventIds.AudioCompEventId, "DMInputEventIds.AudioCompEventId"},
                {DMInputEventIds.EndpointNameEventId, "DMInputEventIds.EndpointNameEventId"},
                {DMInputEventIds.EndpointOnlineEventId, "DMInputEventIds.EndpointOnlineEventId"},
                {DMInputEventIds.UsbRoutedToEventId, "DMInputEventIds.UsbRoutedToEventId"},
                {DMInputEventIds.VideoDetectedEventId, "DMInputEventIds.VideoDetectedEventId"},
                {DMInputEventIds.HdcpSupportOnEventId, "DMInputEventIds.HdcpSupportOnEventId"},
                {DMInputEventIds.HdcpSupportOffEventId, "DMInputEventIds.HdcpSupportOffEventId"},
                {DMInputEventIds.SourceSyncEventId, "DMInputEventIds.SourceSyncEventId"},
                {DMInputEventIds.OnlineFeedbackEventId, "DMInputEventIds.OnlineFeedbackEventId"},
                {DMInputEventIds.PresentFeedbackEventId, "DMInputEventIds.PresentFeedbackEventId"},
                {DMInputEventIds.DisabledByHdcpEventId, "DMInputEventIds.DisabledByHdcpEventId"},
                {DMInputEventIds.DacVolumeEventId, "DMInputEventIds.DacVolumeEventId"},
                {DMInputEventIds.CableLengthEventId, "DMInputEventIds.CableLengthEventId"},
                {DMInputEventIds.CableTypeEventId, "DMInputEventIds.CableTypeEventId"},
                {DMInputEventIds.FullSizeImageEventId, "DMInputEventIds.FullSizeImageEventId"},
                {DMInputEventIds.MultiViewImageEventId, "DMInputEventIds.MultiViewImageEventId"},
                {DMInputEventIds.EnableFullSizeImageCycleEventId, "DMInputEventIds.EnableFullSizeImageCycleEventId"},
                {DMInputEventIds.DisableFullSizeImageCycleEventId, "DMInputEventIds.DisableFullSizeImageCycleEventId"},
                {DMInputEventIds.SelectFullScreenImageEventId, "DMInputEventIds.SelectFullScreenImageEventId"},
                {DMInputEventIds.ImageRotateRateEventId, "DMInputEventIds.ImageRotateRateEventId"},
                {DMInputEventIds.CameraDisabledEventId, "DMInputEventIds.CameraDisabledEventId"},
                {DMInputEventIds.VideoTypeEventId, "DMInputEventIds.VideoTypeEventId"},
                {DMInputEventIds.SourceTypeControlEventId, "DMInputEventIds.SourceTypeControlEventId"},
                {DMInputEventIds.FreeRunEventId, "DMInputEventIds.FreeRunEventId"},
                {DMInputEventIds.Camera1SyncDetectedEventId, "DMInputEventIds.Camera1SyncDetectedEventId"},
                {DMInputEventIds.Camera1EnabledEventId, "DMInputEventIds.Camera1EnabledEventId"},
                {DMInputEventIds.Camera1DisabledEventId, "DMInputEventIds.Camera1DisabledEventId"},
                {DMInputEventIds.Camera1TextColorEventId, "DMInputEventIds.Camera1TextColorEventId"},
                {DMInputEventIds.Camera1TextBoxColorEventId, "DMInputEventIds.Camera1TextBoxColorEventId"},
                {DMInputEventIds.Camera1TextPositionEventId, "DMInputEventIds.Camera1TextPositionEventId"},
                {DMInputEventIds.Camera1LabelEventId, "DMInputEventIds.Camera1LabelEventId"},
                {DMInputEventIds.Camera2SyncDetectedEventId, "DMInputEventIds.Camera2SyncDetectedEventId"},
                {DMInputEventIds.Camera2EnabledEventId, "DMInputEventIds.Camera2EnabledEventId"},
                {DMInputEventIds.Camera2DisabledEventId, "DMInputEventIds.Camera2DisabledEventId"},
                {DMInputEventIds.Camera2TextColorEventId, "DMInputEventIds.Camera2TextColorEventId"},
                {DMInputEventIds.Camera2TextBoxColorEventId, "DMInputEventIds.Camera2TextBoxColorEventId"},
                {DMInputEventIds.Camera2TextPositionEventId, "DMInputEventIds.Camera2TextPositionEventId"},
                {DMInputEventIds.Camera2LabelEventId, "DMInputEventIds.Camera2LabelEventId"},
                {DMInputEventIds.Camera3SyncDetectedEventId, "DMInputEventIds.Camera3SyncDetectedEventId"},
                {DMInputEventIds.Camera3EnabledEventId, "DMInputEventIds.Camera3EnabledEventId"},
                {DMInputEventIds.Camera3DisabledEventId, "DMInputEventIds.Camera3DisabledEventId"},
                {DMInputEventIds.Camera3TextColorEventId, "DMInputEventIds.Camera3TextColorEventId"},
                {DMInputEventIds.Camera3TextBoxColorEventId, "DMInputEventIds.Camera3TextBoxColorEventId"},
                {DMInputEventIds.Camera3TextPositionEventId, "DMInputEventIds.Camera3TextPositionEventId"},
                {DMInputEventIds.Camera3LabelEventId, "DMInputEventIds.Camera3LabelEventId"},
                {DMInputEventIds.Camera4SyncDetectedEventId, "DMInputEventIds.Camera4SyncDetectedEventId"},
                {DMInputEventIds.Camera4EnabledEventId, "DMInputEventIds.Camera4EnabledEventId"},
                {DMInputEventIds.Camera4DisabledEventId, "DMInputEventIds.Camera4DisabledEventId"},
                {DMInputEventIds.Camera4TextColorEventId, "DMInputEventIds.Camera4TextColorEventId"},
                {DMInputEventIds.Camera4TextBoxColorEventId, "DMInputEventIds.Camera4TextBoxColorEventId"},
                {DMInputEventIds.Camera4TextPositionEventId, "DMInputEventIds.Camera4TextPositionEventId"},
                {DMInputEventIds.Camera4LabelEventId, "DMInputEventIds.Camera4LabelEventId"},
                {DMInputEventIds.ForceMEventId, "DMInputEventIds.ForceMEventId"},
                {DMInputEventIds.PrimaryAudioGroupSelectEventId, "DMInputEventIds.PrimaryAudioGroupSelectEventId"},
                {DMInputEventIds.AudioGroup1DetectedEventId, "DMInputEventIds.AudioGroup1DetectedEventId"},
                {DMInputEventIds.AudioGroup2DetectedEventId, "DMInputEventIds.AudioGroup2DetectedEventId"},
                {DMInputEventIds.AudioGroup3DetectedEventId, "DMInputEventIds.AudioGroup3DetectedEventId"},
                {DMInputEventIds.AudioGroup4DetectedEventId, "DMInputEventIds.AudioGroup4DetectedEventId"},
                {DMInputEventIds.NoncompliantAudioDetectedEventId, "DMInputEventIds.NoncompliantAudioDetectedEventId"},
                {DMInputEventIds.CorrectNoncompliantAudioEventId, "DMInputEventIds.CorrectNoncompliantAudioEventId"},
                {DMInputEventIds.PreventNoncompliantAudioCorrectionEventId, "DMInputEventIds.PreventNoncompliantAudioCorrectionEventId"},
                {DMInputEventIds.AudioFormatEventId, "DMInputEventIds.AudioFormatEventId"},
                {DMInputEventIds.AudioChannelsEventId, "DMInputEventIds.AudioChannelsEventId"},
                {DMInputEventIds.AudioSourceEventId, "DMInputEventIds.AudioSourceEventId"},
                {DMInputEventIds.TemperatureEventId, "DMInputEventIds.TemperatureEventId"},
                {DMInputEventIds.HotplugDetectedEventId, "DMInputEventIds.HotplugDetectedEventId"},
                {DMInputEventIds.StartEventId, "DMInputEventIds.StartEventId"},
                {DMInputEventIds.StopEventId, "DMInputEventIds.StopEventId"},
                {DMInputEventIds.PauseEventId, "DMInputEventIds.PauseEventId"},
                {DMInputEventIds.DecoderReadyEventId, "DMInputEventIds.DecoderReadyEventId"},
                {DMInputEventIds.ServerUrlEventId, "DMInputEventIds.ServerUrlEventId"},
                {DMInputEventIds.InitiatorAddressEventId, "DMInputEventIds.InitiatorAddressEventId"},
                {DMInputEventIds.UserNameEventId, "DMInputEventIds.UserNameEventId"},
                {DMInputEventIds.PasswordEventId, "DMInputEventIds.PasswordEventId"},
                {DMInputEventIds.MulticastAddressEventId, "DMInputEventIds.MulticastAddressEventId"},
                {DMInputEventIds.StreamBitrateEventId, "DMInputEventIds.StreamBitrateEventId"},
                {DMInputEventIds.StreamProfileEventId, "DMInputEventIds.StreamProfileEventId"},
                {DMInputEventIds.ElapsedSecEventId, "DMInputEventIds.ElapsedSecEventId"},
                {DMInputEventIds.SessionInitiationEventId, "DMInputEventIds.SessionInitiationEventId"},
                {DMInputEventIds.StreamingBufferEventId, "DMInputEventIds.StreamingBufferEventId"},
                {DMInputEventIds.StreamingTransportModeEventId, "DMInputEventIds.StreamingTransportModeEventId"},
                {DMInputEventIds.AudioMuteEventId, "DMInputEventIds.AudioMuteEventId"},
                {DMInputEventIds.AudioUnmuteEventId, "DMInputEventIds.AudioUnmuteEventId"},
                {DMInputEventIds.OsdEnableEventId, "DMInputEventIds.OsdEnableEventId"},
                {DMInputEventIds.OsdDisableEventId, "DMInputEventIds.OsdDisableEventId"},
                {DMInputEventIds.BassEventId, "DMInputEventIds.BassEventId"},
                {DMInputEventIds.TrebleEventId, "DMInputEventIds.TrebleEventId"},
                {DMInputEventIds.DelayEventId, "DMInputEventIds.DelayEventId"},
                {DMInputEventIds.VolumeEventId, "DMInputEventIds.VolumeEventId"},
                {DMInputEventIds.SourceAverageBitRateEventId, "DMInputEventIds.SourceAverageBitRateEventId"},
                {DMInputEventIds.OsdLocationEventId, "DMInputEventIds.OsdLocationEventId"},
                {DMInputEventIds.OsdXPositionEventId, "DMInputEventIds.OsdXPositionEventId"},
                {DMInputEventIds.OsdYPositionEventId, "DMInputEventIds.OsdYPositionEventId"},
                {DMInputEventIds.ResolutionEventId, "DMInputEventIds.ResolutionEventId"},
                {DMInputEventIds.StreamHorizontalResolutionEventId, "DMInputEventIds.StreamHorizontalResolutionEventId"},
                {DMInputEventIds.StreamVerticalResolutionEventId, "DMInputEventIds.StreamVerticalResolutionEventId"},
                {DMInputEventIds.StreamFramesPerSecondEventId, "DMInputEventIds.StreamFramesPerSecondEventId"},
                {DMInputEventIds.OsdTextEventId, "DMInputEventIds.OsdTextEventId"},
                {DMInputEventIds.ContentLanLinkStatusEventId, "DMInputEventIds.ContentLanLinkStatusEventId"},
                {DMInputEventIds.EnableDhcpEventId, "DMInputEventIds.EnableDhcpEventId"},
                {DMInputEventIds.DisableDhcpEventId, "DMInputEventIds.DisableDhcpEventId"},
                {DMInputEventIds.StatisticsEnableEventId, "DMInputEventIds.StatisticsEnableEventId"},
                {DMInputEventIds.StatisticsDisableEventId, "DMInputEventIds.StatisticsDisableEventId"},
                {DMInputEventIds.ContentLanModeEventId, "DMInputEventIds.ContentLanModeEventId"},
                {DMInputEventIds.RtspPortEventId, "DMInputEventIds.RtspPortEventId"},
                {DMInputEventIds.RtpVideoPortEventId, "DMInputEventIds.RtpVideoPortEventId"},
                {DMInputEventIds.RtpAudioPortEventId, "DMInputEventIds.RtpAudioPortEventId"},
                {DMInputEventIds.TsPortEventId, "DMInputEventIds.TsPortEventId"},
                {DMInputEventIds.StatisticsNumberOfVideoPacketsReceivedEventId, "DMInputEventIds.StatisticsNumberOfVideoPacketsReceivedEventId"},
                {DMInputEventIds.StatisticsNumberOfVideoPacketsDroppedEventId, "DMInputEventIds.StatisticsNumberOfVideoPacketsDroppedEventId"},
                {DMInputEventIds.StatisticsNumberOfAudioPacketsReceivedEventId, "DMInputEventIds.StatisticsNumberOfAudioPacketsReceivedEventId"},
                {DMInputEventIds.StatisticsNumberOfAudioPacketsDroppedEventId, "DMInputEventIds.StatisticsNumberOfAudioPacketsDroppedEventId"},
                {DMInputEventIds.TimesetMethodEventId, "DMInputEventIds.TimesetMethodEventId"},
                {DMInputEventIds.TimeZoneEventId, "DMInputEventIds.TimeZoneEventId"},
                {DMInputEventIds.CurrentIpAddressEventId, "DMInputEventIds.CurrentIpAddressEventId"},
                {DMInputEventIds.CurrentSubnetMaskEventId, "DMInputEventIds.CurrentSubnetMaskEventId"},
                {DMInputEventIds.CurrentDefaultRouterEventId, "DMInputEventIds.CurrentDefaultRouterEventId"},
                {DMInputEventIds.HostNameEventId, "DMInputEventIds.HostNameEventId"},
                {DMInputEventIds.CurrentDomainNameEventId, "DMInputEventIds.CurrentDomainNameEventId"},
                {DMInputEventIds.StaticIpAddressEventId, "DMInputEventIds.StaticIpAddressEventId"},
                {DMInputEventIds.StaticSubnetMaskEventId, "DMInputEventIds.StaticSubnetMaskEventId"},
                {DMInputEventIds.StaticDefaultRouterEventId, "DMInputEventIds.StaticDefaultRouterEventId"},
                {DMInputEventIds.DomainNameServerEventId, "DMInputEventIds.DomainNameServerEventId"},
                {DMInputEventIds.StaticDomainNameServerEventId, "DMInputEventIds.StaticDomainNameServerEventId"},
                {DMInputEventIds.StaticDomainNameEventId, "DMInputEventIds.StaticDomainNameEventId"},
                {DMInputEventIds.CurrentIpv6AddressEventId, "DMInputEventIds.CurrentIpv6AddressEventId"},
                {DMInputEventIds.StaticIpv6AddressEventId, "DMInputEventIds.StaticIpv6AddressEventId"},
                {DMInputEventIds.NtpServerEventId, "DMInputEventIds.NtpServerEventId"},
                {DMInputEventIds.ProcessingEventId, "DMInputEventIds.ProcessingEventId"},
                {DMInputEventIds.StatusEventId, "DMInputEventIds.StatusEventId"},
                {DMInputEventIds.StatusTextEventId, "DMInputEventIds.StatusTextEventId"},
                {DMInputEventIds.AudioGainFeedbackEventId, "DMInputEventIds.AudioGainFeedbackEventId"},
                {DMInputEventIds.AudioSourceDetectedEventId, "DMInputEventIds.AudioSourceDetectedEventId"},
                {DMInputEventIds.VgaAutoCalibrateDetectedEventId, "DMInputEventIds.VgaAutoCalibrateDetecteEventId"}
            };


            _dmOutputEventIdsMapping = new Dictionary<uint, string>
            {
                {DMOutputEventIds.NA, "DMOutputEventIds.NA"},
                {DMOutputEventIds.OutputNameEventId, "DMOutputEventIds.OutputNameEventId"},
                {DMOutputEventIds.HdcpStateEventId, "DMOutputEventIds.HdcpStateEventId"},
                {DMOutputEventIds.DisabledByHdcpEventId, "DMOutputEventIds.DisabledByHdcpEventId"},
                {DMOutputEventIds.DeepColorModeDisableEventId, "DMOutputEventIds.DeepColorModeDisableEventId"},
                {DMOutputEventIds.DeepColorModeEnableEventId, "DMOutputEventIds.DeepColorModeEnableEventId"},
                {DMOutputEventIds.EndpointOnlineEventId, "DMOutputEventIds.EndpointOnlineEventId"},
                {DMOutputEventIds.AudioChannelsEventId, "DMOutputEventIds.AudioChannelsEventId"},
                {DMOutputEventIds.AudioFormatEventId, "DMOutputEventIds.AudioFormatEventId"},
                {DMOutputEventIds.AudioOutEventId, "DMOutputEventIds.AudioOutEventId"},
                {DMOutputEventIds.UsbRoutedToEventId, "DMOutputEventIds.UsbRoutedToEventId"},
                {DMOutputEventIds.VideoOutEventId, "DMOutputEventIds.VideoOutEventId"},
                {DMOutputEventIds.StreamDeepColorModeEventId, "DMOutputEventIds.StreamDeepColorModeEventId"},
                {DMOutputEventIds.Stream3DStatusEventId, "DMOutputEventIds.Stream3DStatusEventId"},
                {DMOutputEventIds.OnlineFeedbackEventId, "DMOutputEventIds.OnlineFeedbackEventId"},
                {DMOutputEventIds.PresentFeedbackEventId, "DMOutputEventIds.PresentFeedbackEventId"},
                {DMOutputEventIds.ForceHdcpEnabledEventId, "DMOutputEventIds.ForceHdcpEnabledEventId"},
                {DMOutputEventIds.ForceHdcpDisabledEventId, "DMOutputEventIds.ForceHdcpDisabledEventId"},
                {DMOutputEventIds.OutputDisabledEventId, "DMOutputEventIds.OutputDisabledEventId"},
                {DMOutputEventIds.OutputEnabledEventId, "DMOutputEventIds.OutputEnabledEventId"},
                {DMOutputEventIds.EnablePipSwapEventId, "DMOutputEventIds.EnablePipSwapEventId"},
                {DMOutputEventIds.DisablePipSwapEventId, "DMOutputEventIds.DisablePipSwapEventId"},
                {DMOutputEventIds.PipFullframeEventId, "DMOutputEventIds.PipFullframeEventId"},
                {DMOutputEventIds.PipSideBySideEventId, "DMOutputEventIds.PipSideBySideEventId"},
                {DMOutputEventIds.PipUpperLeftEventId, "DMOutputEventIds.PipUpperLeftEventId"},
                {DMOutputEventIds.PipCenterLeftEventId, "DMOutputEventIds.PipCenterLeftEventId"},
                {DMOutputEventIds.PipLowerLeftEventId, "DMOutputEventIds.PipLowerLeftEventId"},
                {DMOutputEventIds.PipUpperRightEventId, "DMOutputEventIds.PipUpperRightEventId"},
                {DMOutputEventIds.PipCenterRightEventId, "DMOutputEventIds.PipCenterRightEventId"},
                {DMOutputEventIds.PipLowerRightEventId, "DMOutputEventIds.PipLowerRightEventId"},
                {DMOutputEventIds.AudioMuteEventId, "DMOutputEventIds.AudioMuteEventId"},
                {DMOutputEventIds.AudioUnmuteEventId, "DMOutputEventIds.AudioUnmuteEventId"},
                {DMOutputEventIds.PipSizeEventId, "DMOutputEventIds.PipSizeEventId"},
                {DMOutputEventIds.PipBorderThicknessEventId, "DMOutputEventIds.PipBorderThicknessEventId"},
                {DMOutputEventIds.VolumeEventId, "DMOutputEventIds.VolumeEventId"},
                {DMOutputEventIds.TrebleEventId, "DMOutputEventIds.TrebleEventId"},
                {DMOutputEventIds.BassEventId, "DMOutputEventIds.BassEventId"},
                {DMOutputEventIds.DelayEventId, "DMOutputEventIds.DelayEventId"},
                {DMOutputEventIds.VideoDetectedEventId, "DMOutputEventIds.VideoDetectedEventId"},
                {DMOutputEventIds.NoVideoEventId, "DMOutputEventIds.NoVideoEventId"},
                {DMOutputEventIds.SyncDetectedEventId, "DMOutputEventIds.SyncDetectedEventId"},
                {DMOutputEventIds.MixLevelEventId, "DMOutputEventIds.MixLevelEventId"},
                {DMOutputEventIds.SamplingFreqEventId, "DMOutputEventIds.SamplingFreqEventId"},
                {DMOutputEventIds.ContentLANLinkStatusEventId, "DMOutputEventIds.ContentLANLinkStatusEventId"},
                {DMOutputEventIds.EnableDhcpEventId, "DMOutputEventIds.EnableDhcpEventId"},
                {DMOutputEventIds.DisableDhcpEventId, "DMOutputEventIds.DisableDhcpEventId"},
                {DMOutputEventIds.ContentLanModeEventId, "DMOutputEventIds.ContentLanModeEventId"},
                {DMOutputEventIds.HostNameEventId, "DMOutputEventIds.HostNameEventId"},
                {DMOutputEventIds.CurrentIpAddressEventId, "DMOutputEventIds.CurrentIpAddressEventId"},
                {DMOutputEventIds.CurrentSubnetMaskEventId, "DMOutputEventIds.CurrentSubnetMaskEventId"},
                {DMOutputEventIds.CurrentDefaultRouterEventId, "DMOutputEventIds.CurrentDefaultRouterEventId"},
                {DMOutputEventIds.CurrentDomainNameEventId, "DMOutputEventIds.CurrentDomainNameEventId"},
                {DMOutputEventIds.CurrentDomainNameServerEventId, "DMOutputEventIds.CurrentDomainNameServerEventId"},
                {DMOutputEventIds.StaticIpAddressEventId, "DMOutputEventIds.StaticIpAddressEventId"},
                {DMOutputEventIds.StaticSubnetMaskEventId, "DMOutputEventIds.StaticSubnetMaskEventId"},
                {DMOutputEventIds.StaticDefaultRouterEventId, "DMOutputEventIds.StaticDefaultRouterEventId"},
                {DMOutputEventIds.StaticDomainNameEventId, "DMOutputEventIds.StaticDomainNameEventId"},
                {DMOutputEventIds.StaticDomainNameServerEventId, "DMOutputEventIds.StaticDomainNameServerEventId"},
                {DMOutputEventIds.StartEventId, "DMOutputEventIds.StartEventId"},
                {DMOutputEventIds.StopEventId, "DMOutputEventIds.StopEventId"},
                {DMOutputEventIds.PauseEventId, "DMOutputEventIds.PauseEventId"},
                {DMOutputEventIds.EncoderReadyEventId, "DMOutputEventIds.EncoderReadyEventId"},
                {DMOutputEventIds.EnableStreamasTSEventId, "DMOutputEventIds.EnableStreamasTSEventId"},
                {DMOutputEventIds.DisableStreamasTSEventId, "DMOutputEventIds.DisableStreamasTSEventId"},
                {DMOutputEventIds.EnablePasswordProtectionEventId, "DMOutputEventIds.EnablePasswordProtectionEventId"},
                {DMOutputEventIds.DisablePasswordProtectionEventId, "DMOutputEventIds.DisablePasswordProtectionEventId"},
                {DMOutputEventIds.StreamFormatEventId, "DMOutputEventIds.StreamFormatEventId"},
                {DMOutputEventIds.StreamBitrateEventId, "DMOutputEventIds.StreamBitrateEventId"},
                {DMOutputEventIds.StreamProfileEventId, "DMOutputEventIds.StreamProfileEventId"},
                {DMOutputEventIds.ElapsedSecEventId, "DMOutputEventIds.ElapsedSecEventId"},
                {DMOutputEventIds.StatusDescriptionEventId, "DMOutputEventIds.StatusDescriptionEventId"},
                {DMOutputEventIds.ClientURLEventId, "DMOutputEventIds.ClientURLEventId"},
                {DMOutputEventIds.TargetAddressEventId, "DMOutputEventIds.TargetAddressEventId"},
                {DMOutputEventIds.MulticastAddressEventId, "DMOutputEventIds.MulticastAddressEventId"},
                {DMOutputEventIds.UserNameEventId, "DMOutputEventIds.UserNameEventId"},
                {DMOutputEventIds.PasswordEventId, "DMOutputEventIds.PasswordEventId"},
                {DMOutputEventIds.NoVideo2EventId, "DMOutputEventIds.NoVideo2EventId"},
                {DMOutputEventIds.TemperatureEventId, "DMOutputEventIds.TemperatureEventId"},
                {DMOutputEventIds.EndpointNameEventId, "DMOutputEventIds.EndpointNameEventId"},
                {DMOutputEventIds.CableTypeEventId, "DMOutputEventIds.CableTypeEventId"},
                {DMOutputEventIds.ColorSpaceEventId, "DMOutputEventIds.ColorSpaceEventId"},
                {DMOutputEventIds.SessionInitiationEventId, "DMOutputEventIds.SessionInitiationEventId"},
                {DMOutputEventIds.HotplugDetectedEventId, "DMOutputEventIds.HotplugDetectedEventId"},
                {DMOutputEventIds.LimiterEnableFeedbackEventId, "DMOutputEventIds.LimiterEnableFeedbackEventId"},
                {DMOutputEventIds.LimiterDisableFeedbackEventId, "DMOutputEventIds.LimiterDisableFeedbackEventId"},
                {DMOutputEventIds.LimiterSoftKneeOnFeedbackEventId, "DMOutputEventIds.LimiterSoftKneeOnFeedbackEventId"},
                {DMOutputEventIds.LimiterSoftKneeOffFeedbackEventId, "DMOutputEventIds.LimiterSoftKneeOffFeedbackEventId"},
                {DMOutputEventIds.RecallingPresetFeedbackEventId, "DMOutputEventIds.RecallingPresetFeedbackEventId"},
                {DMOutputEventIds.PresetReadyPulseFeedbackEventId, "DMOutputEventIds.PresetReadyPulseFeedbackEventId"},
                {DMOutputEventIds.BassFeedbackEventId, "DMOutputEventIds.BassFeedbackEventId"},
                {DMOutputEventIds.TrebleFeedbackEventId, "DMOutputEventIds.TrebleFeedbackEventId"},
                {DMOutputEventIds.GeqGain315HzFeedbackEventId, "DMOutputEventIds.GeqGain315HzFeedbackEventId"},
                {DMOutputEventIds.GeqGain63HzFeedbackEventId, "DMOutputEventIds.GeqGain63HzFeedbackEventId"},
                {DMOutputEventIds.GeqGain125HzFeedbackEventId, "DMOutputEventIds.GeqGain125HzFeedbackEventId"},
                {DMOutputEventIds.GeqGain250HzFeedbackEventId, "DMOutputEventIds.GeqGain250HzFeedbackEventId"},
                {DMOutputEventIds.GeqGain500HzFeedbackEventId, "DMOutputEventIds.GeqGain500HzFeedbackEventId"},
                {DMOutputEventIds.GeqGain1KHzFeedbackEventId, "DMOutputEventIds.GeqGain1KHzFeedbackEventId"},
                {DMOutputEventIds.GeqGain2KHzFeedbackEventId, "DMOutputEventIds.GeqGain2KHzFeedbackEventId"},
                {DMOutputEventIds.GeqGain4KHzFeedbackEventId, "DMOutputEventIds.GeqGain4KHzFeedbackEventId"},
                {DMOutputEventIds.GeqGain8KHzFeedbackEventId, "DMOutputEventIds.GeqGain8KHzFeedbackEventId"},
                {DMOutputEventIds.GeqGain16KHzFeedbackEventId, "DMOutputEventIds.GeqGain16KHzFeedbackEventId"},
                {DMOutputEventIds.PeqFilter1TypeFeedbackEventId, "DMOutputEventIds.PeqFilter1TypeFeedbackEventId"},
                {DMOutputEventIds.PeqFilter1GainFeedbackEventId, "DMOutputEventIds.PeqFilter1GainFeedbackEventId"},
                {DMOutputEventIds.PeqFilter1FrequencyFeedbackEventId, "DMOutputEventIds.PeqFilter1FrequencyFeedbackEventId"},
                {DMOutputEventIds.PeqFilter1BandwidthFeedbackEventId, "DMOutputEventIds.PeqFilter1BandwidthFeedbackEventId"},
                {DMOutputEventIds.PeqFilter2TypeFeedbackEventId, "DMOutputEventIds.PeqFilter2TypeFeedbackEventId"},
                {DMOutputEventIds.PeqFilter2GainFeedbackEventId, "DMOutputEventIds.PeqFilter2GainFeedbackEventId"},
                {DMOutputEventIds.PeqFilter2FrequencyFeedbackEventId, "DMOutputEventIds.PeqFilter2FrequencyFeedbackEventId"},
                {DMOutputEventIds.PeqFilter2BandwidthFeedbackEventId, "DMOutputEventIds.PeqFilter2BandwidthFeedbackEventId"},
                {DMOutputEventIds.PeqFilter3TypeFeedbackEventId, "DMOutputEventIds.PeqFilter3TypeFeedbackEventId"},
                {DMOutputEventIds.PeqFilter3GainFeedbackEventId, "DMOutputEventIds.PeqFilter3GainFeedbackEventId"},
                {DMOutputEventIds.PeqFilter3FrequencyFeedbackEventId, "DMOutputEventIds.PeqFilter3FrequencyFeedbackEventId"},
                {DMOutputEventIds.PeqFilter3BandwidthFeedbackEventId, "DMOutputEventIds.PeqFilter3BandwidthFeedbackEventId"},
                {DMOutputEventIds.PeqFilter4TypeFeedbackEventId, "DMOutputEventIds.PeqFilter4TypeFeedbackEventId"},
                {DMOutputEventIds.PeqFilter4GainFeedbackEventId, "DMOutputEventIds.PeqFilter4GainFeedbackEventId"},
                {DMOutputEventIds.PeqFilter4FrequencyFeedbackEventId, "DMOutputEventIds.PeqFilter4FrequencyFeedbackEventId"},
                {DMOutputEventIds.PeqFilter4BandwidthFeedbackEventId, "DMOutputEventIds.PeqFilter4BandwidthFeedbackEventId"},
                {DMOutputEventIds.LimiterThresholdFeedbackEventId, "DMOutputEventIds.LimiterThresholdFeedbackEventId"},
                {DMOutputEventIds.LimiterAttackFeedbackEventId, "DMOutputEventIds.LimiterAttackFeedbackEventId"},
                {DMOutputEventIds.LimiterReleaseFeedbackEventId, "DMOutputEventIds.LimiterReleaseFeedbackEventId"},
                {DMOutputEventIds.LimiterRatioFeedbackEventId, "DMOutputEventIds.LimiterRatioFeedbackEventId"},
                {DMOutputEventIds.LimiterHoldFeedbackEventId, "DMOutputEventIds.LimiterHoldFeedbackEventId"},
                {DMOutputEventIds.MixerBypassedFeedBackEventId, "DMOutputEventIds.MixerBypassedFeedBackEventId"},
                {DMOutputEventIds.MonoOutputFeedBackEventId, "DMOutputEventIds.MonoOutputFeedBackEventId"},
                {DMOutputEventIds.StereoOutputFeedBackEventId, "DMOutputEventIds.StereoOutputFeedBackEventId"},
                {DMOutputEventIds.Mic1MuteOnFeedBackEventId, "DMOutputEventIds.Mic1MuteOnFeedBackEventId"},
                {DMOutputEventIds.Mic1MuteOffFeedBackEventId, "DMOutputEventIds.Mic1MuteOffFeedBackEventId"},
                {DMOutputEventIds.Mic2MuteOnFeedBackEventId, "DMOutputEventIds.Mic2MuteOnFeedBackEventId"},
                {DMOutputEventIds.Mic2MuteOffFeedBackEventId, "DMOutputEventIds.Mic2MuteOffFeedBackEventId"},
                {DMOutputEventIds.Mic3MuteOnFeedBackEventId, "DMOutputEventIds.Mic3MuteOnFeedBackEventId"},
                {DMOutputEventIds.Mic3MuteOffFeedBackEventId, "DMOutputEventIds.Mic3MuteOffFeedBackEventId"},
                {DMOutputEventIds.Mic4MuteOnFeedBackEventId, "DMOutputEventIds.Mic4MuteOnFeedBackEventId"},
                {DMOutputEventIds.Mic4MuteOffFeedBackEventId, "DMOutputEventIds.Mic4MuteOffFeedBackEventId"},
                {DMOutputEventIds.Mic5MuteOnFeedBackEventId, "DMOutputEventIds.Mic5MuteOnFeedBackEventId"},
                {DMOutputEventIds.Mic5MuteOffFeedBackEventId, "DMOutputEventIds.Mic5MuteOffFeedBackEventId"},
                {DMOutputEventIds.Mic6MuteOnFeedBackEventId, "DMOutputEventIds.Mic6MuteOnFeedBackEventId"},
                {DMOutputEventIds.Mic6MuteOffFeedBackEventId, "DMOutputEventIds.Mic6MuteOffFeedBackEventId"},
                {DMOutputEventIds.Mic1LevelFeedBackEventId, "DMOutputEventIds.Mic1LevelFeedBackEventId"},
                {DMOutputEventIds.Mic2LevelFeedBackEventId, "DMOutputEventIds.Mic2LevelFeedBackEventId"},
                {DMOutputEventIds.Mic3LevelFeedBackEventId, "DMOutputEventIds.Mic3LevelFeedBackEventId"},
                {DMOutputEventIds.Mic4LevelFeedBackEventId, "DMOutputEventIds.Mic4LevelFeedBackEventId"},
                {DMOutputEventIds.Mic5LevelFeedBackEventId, "DMOutputEventIds.Mic5LevelFeedBackEventId"},
                {DMOutputEventIds.Mic6LevelFeedBackEventId, "DMOutputEventIds.Mic6LevelFeedBackEventId"},
                {DMOutputEventIds.Mic1PanFeedBackEventId, "DMOutputEventIds.Mic1PanFeedBackEventId"},
                {DMOutputEventIds.Mic2PanFeedBackEventId, "DMOutputEventIds.Mic2PanFeedBackEventId"},
                {DMOutputEventIds.Mic3PanFeedBackEventId, "DMOutputEventIds.Mic3PanFeedBackEventId"},
                {DMOutputEventIds.Mic4PanFeedBackEventId, "DMOutputEventIds.Mic4PanFeedBackEventId"},
                {DMOutputEventIds.Mic5PanFeedBackEventId, "DMOutputEventIds.Mic5PanFeedBackEventId"},
                {DMOutputEventIds.Mic6PanFeedBackEventId, "DMOutputEventIds.Mic6PanFeedBackEventId"},
                {DMOutputEventIds.DelayFeedBackEventId, "DMOutputEventIds.DelayFeedBackEventId"},
                {DMOutputEventIds.MinVolumeFeedBackEventId, "DMOutputEventIds.MinVolumeFeedBackEventId"},
                {DMOutputEventIds.MaxVolumeFeedBackEventId, "DMOutputEventIds.MaxVolumeFeedBackEventId"},
                {DMOutputEventIds.StartupVolumeFeedBackEventId, "DMOutputEventIds.StartupVolumeFeedBackEventId"},
                {DMOutputEventIds.OutputVuFeedBackEventId, "DMOutputEventIds.OutputVuFeedBackEventId"},
                {DMOutputEventIds.AmpPowerOnFeedBackEventId, "DMOutputEventIds.AmpPowerOnFeedBackEventId"},
                {DMOutputEventIds.AmpPowerOffFeedBackEventId, "DMOutputEventIds.AmpPowerOffFeedBackEventId"},
                {DMOutputEventIds.MasterMuteOnFeedBackEventId, "DMOutputEventIds.MasterMuteOnFeedBackEventId"},
                {DMOutputEventIds.MasterMuteOffFeedBackEventId, "DMOutputEventIds.MasterMuteOffFeedBackEventId"},
                {DMOutputEventIds.MicMasterMuteOnFeedBackEventId, "DMOutputEventIds.MicMasterMuteOnFeedBackEventId"},
                {DMOutputEventIds.MicMasterMuteOffFeedBackEventId, "DMOutputEventIds.MicMasterMuteOffFeedBackEventId"},
                {DMOutputEventIds.SourceMuteOnFeedBackEventId, "DMOutputEventIds.SourceMuteOnFeedBackEventId"},
                {DMOutputEventIds.SourceMuteOffFeedBackEventId, "DMOutputEventIds.SourceMuteOffFeedBackEventId"},
                {DMOutputEventIds.Codec1MuteOnFeedBackEventId, "DMOutputEventIds.Codec1MuteOnFeedBackEventId"},
                {DMOutputEventIds.Codec1MuteOffFeedBackEventId, "DMOutputEventIds.Codec1MuteOffFeedBackEventId"},
                {DMOutputEventIds.Codec2MuteOnFeedBackEventId, "DMOutputEventIds.Codec2MuteOnFeedBackEventId"},
                {DMOutputEventIds.Codec2MuteOffFeedBackEventId, "DMOutputEventIds.Codec2MuteOffFeedBackEventId"},
                {DMOutputEventIds.MasterVolumeFeedBackEventId, "DMOutputEventIds.MasterVolumeFeedBackEventId"},
                {DMOutputEventIds.MicMasterLevelFeedBackEventId, "DMOutputEventIds.MicMasterLevelFeedBackEventId"},
                {DMOutputEventIds.SourceLevelFeedBackEventId, "DMOutputEventIds.SourceLevelFeedBackEventId"},
                {DMOutputEventIds.SourceBalanceFeedBackEventId, "DMOutputEventIds.SourceBalanceFeedBackEventId"},
                {DMOutputEventIds.Codec1LevelFeedBackEventId, "DMOutputEventIds.Codec1LevelFeedBackEventId"},
                {DMOutputEventIds.Codec1BalanceFeedBackEventId, "DMOutputEventIds.Codec1BalanceFeedBackEventId"},
                {DMOutputEventIds.Codec2LevelFeedBackEventId, "DMOutputEventIds.Codec2LevelFeedBackEventId"},
                {DMOutputEventIds.Codec2BalanceFeedBackEventId, "DMOutputEventIds.Codec2BalanceFeedBackEventId"}
            };

            _connectedDeviceEventIdsMapping = new Dictionary<uint, string>
            {
                {ConnectedDeviceEventIds.ManufacturerEventId, "ConnectedDeviceEventIds.ManufacturerEventId"},
                {ConnectedDeviceEventIds.NameEventId, "ConnectedDeviceEventIds.NameEventId"},
                {ConnectedDeviceEventIds.PreferredTimingEventId, "ConnectedDeviceEventIds.PreferredTimingEventId"},
                {ConnectedDeviceEventIds.SerialNumberEventId, "ConnectedDeviceEventIds.SerialNumberEventId"}
            };

            _cecEventIdsMapping = new Dictionary<uint, string>
            {
                {CecEventIds.ErrorFeedbackEventId, "CecEventIds.ErrorFeedbackEventId"},
                {CecEventIds.CecMessageReceivedEventId, "CecEventIds.CecMessageReceivedEventId"}
            };

            _videoAttributeIdsMapping = new Dictionary<uint, string>
            {
                {VideoAttributeEventIds.InterlacedFeedbackEventId, "VideoAttributeEventIds.InterlacedFeedbackEventId"},
                {VideoAttributeEventIds.HorizontalResolutionFeedbackEventId, "VideoAttributeEventIds.HorizontalResolutionFeedbackEventId"},
                {VideoAttributeEventIds.VerticalResolutionFeedbackEventId, "VideoAttributeEventIds.VerticalResolutionFeedbackEventId"},
                {VideoAttributeEventIds.FramesPerSecondFeedbackEventId, "VideoAttributeEventIds.FramesPerSecondFeedbackEventId"},
                {VideoAttributeEventIds.AspectRatioFeedbackEventId, "VideoAttributeEventIds.AspectRatioFeedbackEventId"},
                {VideoAttributeEventIds.HdcpStateFeedbackEventId, "VideoAttributeEventIds.HdcpStateFeedbackEventId"},
                {VideoAttributeEventIds.DeepColorFeedbackEventId, "VideoAttributeEventIds.DeepColorFeedbackEventId"},
                {VideoAttributeEventIds.HdcpActiveFeedbackEventId, "VideoAttributeEventIds.HdcpActiveFeedbackEventId"},
                {VideoAttributeEventIds.MaximumSupportedHdcpKeysFeedbackEventId, "VideoAttributeEventIds.MaximumSupportedHdcpKeysFeedbackEventId"},
                {VideoAttributeEventIds.Stream3DStatusFeedbackEventId, "VideoAttributeEventIds.Stream3DStatusFeedbackEventId"},
                {VideoAttributeEventIds.ColorSpaceEventId, "VideoAttributeEventIds.ColorSpaceEventId"},
                {VideoAttributeEventIds.PCResolutionDetectedEventId, "VideoAttributeEventIds.PCResolutionDetectedEventId"}
            };

            _videoControlsIdsMapping = new Dictionary<uint, string>
            {
                {VideoControlsEventIds.BrightnessFeedbackEventId, "VideoControlsEventIds.BrightnessFeedbackEventId"},
                {VideoControlsEventIds.ContrastFeedbackEventId, "VideoControlsEventIds.ContrastFeedbackEventId"},
                {VideoControlsEventIds.SaturationFeedbackEventId, "VideoControlsEventIds.SaturationFeedbackEventId"},
                {VideoControlsEventIds.HueFeedbackEventId, "VideoControlsEventIds.HueFeedbackEventId"},
                {VideoControlsEventIds.RedFeedbackEventId, "VideoControlsEventIds.RedFeedbackEventId"},
                {VideoControlsEventIds.GreenFeedbackEventId, "VideoControlsEventIds.GreenFeedbackEventId"},
                {VideoControlsEventIds.BlueFeedbackEventId, "VideoControlsEventIds.BlueFeedbackEventId"},
                {VideoControlsEventIds.OverscanModeFeedbackEventId, "VideoControlsEventIds.OverscanModeFeedbackEventId"},
                {VideoControlsEventIds.NoiseReductionFeedbackEventId, "VideoControlsEventIds.NoiseReductionFeedbackEventId"},
                {VideoControlsEventIds.FinePhaseFeedbackEventId, "VideoControlsEventIds.FinePhaseFeedbackEventId"},
                {VideoControlsEventIds.XPositionFeedbackEventId, "VideoControlsEventIds.XPositionFeedbackEventId"},
                {VideoControlsEventIds.YPositionFeedbackEventId, "VideoControlsEventIds.YPositionFeedbackEventId"},
                {VideoControlsEventIds.CoarseFeedbackEventId, "VideoControlsEventIds.CoarseFeedbackEventId"},
                {VideoControlsEventIds.FreeRunFeedbackEventId, "VideoControlsEventIds.FreeRunFeedbackEventId"},
                {VideoControlsEventIds.VideoTypeControlEventId, "VideoControlsEventIds.VideoTypeControlEventId"}
            };

            _microphoneEventIdsMapping = new Dictionary<uint, string>
            {
                {MicrophoneEventIds.MuteOnFeedBackEventId, "MicrophoneEventIds.MuteOnFeedBackEventId"},
                {MicrophoneEventIds.MuteOffFeedBackEventId, "MicrophoneEventIds.MuteOffFeedBackEventId"},
                {MicrophoneEventIds.GatingFeedBackEventId, "MicrophoneEventIds.GatingFeedBackEventId"},
                {MicrophoneEventIds.NominalFeedBackEventId, "MicrophoneEventIds.NominalFeedBackEventId"},
                {MicrophoneEventIds.ClipFeedBackEventId, "MicrophoneEventIds.ClipFeedBackEventId"},
                {MicrophoneEventIds.PhantomPowerOnFeedBackEventId, "MicrophoneEventIds.PhantomPowerOnFeedBackEventId"},
                {MicrophoneEventIds.PhantomPowerOffFeedBackEventId, "MicrophoneEventIds.PhantomPowerOffFeedBackEventId"},
                {MicrophoneEventIds.GateEnableFeedBackEventId, "MicrophoneEventIds.GateEnableFeedBackEventId"},
                {MicrophoneEventIds.GateDisableFeedBackEventId, "MicrophoneEventIds.GateDisableFeedBackEventId"},
                {MicrophoneEventIds.CompressorEnableFeedBackEventId, "MicrophoneEventIds.CompressorEnableFeedBackEventId"},
                {MicrophoneEventIds.CompressorDisableFeedBackEventId, "MicrophoneEventIds.CompressorDisableFeedBackEventId"},
                {MicrophoneEventIds.CompressorSoftKneeOnFeedBackEventId, "MicrophoneEventIds.CompressorSoftKneeOnFeedBackEventId"},
                {MicrophoneEventIds.CompressorSoftKneeOffFeedBackEventId, "MicrophoneEventIds.CompressorSoftKneeOffFeedBackEventId"},
                {MicrophoneEventIds.CompressorThresholdReachedFeedBackEventId, "MicrophoneEventIds.CompressorThresholdReachedFeedBackEventId"},
                {MicrophoneEventIds.HighPassFilterEnableFeedBackEventId, "MicrophoneEventIds.HighPassFilterEnableFeedBackEventId"},
                {MicrophoneEventIds.HighPassFilterDisableFeedBackEventId, "MicrophoneEventIds.HighPassFilterDisableFeedBackEventId"},
                {MicrophoneEventIds.GainFeedBackEventId, "MicrophoneEventIds.GainFeedBackEventId"},
                {MicrophoneEventIds.VuFeedBackEventId, "MicrophoneEventIds.VuFeedBackEventId"},
                {MicrophoneEventIds.DelayFeedBackEventId, "MicrophoneEventIds.DelayFeedBackEventId"},
                {MicrophoneEventIds.GateThresholdFeedBackEventId, "MicrophoneEventIds.GateThresholdFeedBackEventId"},
                {MicrophoneEventIds.GateDepthFeedBackEventId, "MicrophoneEventIds.GateDepthFeedBackEventId"},
                {MicrophoneEventIds.GateAttackFeedBackEventId, "MicrophoneEventIds.GateAttackFeedBackEventId"},
                {MicrophoneEventIds.GateReleaseFeedBackEventId, "MicrophoneEventIds.GateReleaseFeedBackEventId"},
                {MicrophoneEventIds.GateHoldFeedBackEventId, "MicrophoneEventIds.GateHoldFeedBackEventId"},
                {MicrophoneEventIds.CompressorThresholdFeedBackEventId, "MicrophoneEventIds.CompressorThresholdFeedBackEventId"},
                {MicrophoneEventIds.CompressorAttackFeedBackEventId, "MicrophoneEventIds.CompressorAttackFeedBackEventId"},
                {MicrophoneEventIds.CompressorReleaseFeedBackEventId, "MicrophoneEventIds.CompressorReleaseFeedBackEventId"},
                {MicrophoneEventIds.CompressorRatioFeedBackEventId, "MicrophoneEventIds.CompressorRatioFeedBackEventId"},
                {MicrophoneEventIds.CompressorHoldFeedBackEventId, "MicrophoneEventIds.CompressorHoldFeedBackEventId"},
                {MicrophoneEventIds.EqHighBandFrequencyFeedBackEventId, "MicrophoneEventIds.EqHighBandFrequencyFeedBackEventId"},
                {MicrophoneEventIds.EqHighBandGainFeedBackEventId, "MicrophoneEventIds.EqHighBandGainFeedBackEventId"},
                {MicrophoneEventIds.EqHighMidBandFrequencyFeedBackEventId, "MicrophoneEventIds.EqHighMidBandFrequencyFeedBackEventId"},
                {MicrophoneEventIds.EqHighMidBandGainFeedBackEventId, "MicrophoneEventIds.EqHighMidBandGainFeedBackEventId"},
                {MicrophoneEventIds.EqLowMidBandFrequencyFeedBackEventId, "MicrophoneEventIds.EqLowMidBandFrequencyFeedBackEventId"},
                {MicrophoneEventIds.EqLowMidBandGainFeedBackEventId, "MicrophoneEventIds.EqLowMidBandGainFeedBackEventId"},
                {MicrophoneEventIds.EqLowBandFrequencyFeedBackEventId, "MicrophoneEventIds.EqLowBandFrequencyFeedBackEventId"},
                {MicrophoneEventIds.EqLowBandGainFeedBackEventId, "MicrophoneEventIds.EqLowBandGainFeedBackEventId"}
            };
        }

        #endregion
    }
}
