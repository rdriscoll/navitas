using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace Navitas
{
    class Config
    {
        public String project { get; set; }
        public String type { get; set; }
        public List<Location> locations;
        //public List<UserInterface> uiLocations;
        public String name { get; set; }
        public String controller { get; set; }
        public String version { get; set; }
        public byte IPID { get; set; }
        public String vidSwitcherIpAddress { get; set; }
        public List<Level> mics { get; set; }
        public Lights lights { get; set; }
        public List<RoomPlusDev> vidInputs  { get; set; }
        public List<RoomPlusDev> vidOutputs { get; set; }
        public AudioDsp audioDsp { get; set; }

        public bool showRecordInMenu { get; set; }
        public bool loginRequired { get; set; }
        public ushort numMics { get; set; }
        public String passwordUser { get; set; }
        public String passwordAdmin { get; set; }

        public Config(String Type, String Name, String Controller, List<Location> locations)
        {
            SetDefaultStrings();
            this.type = Type;
            this.name = Name;
            this.controller = Controller;
            this.locations = locations;
            //IPID = 0x99; // test only
            passwordAdmin = "1988";
            passwordUser = "1234";
        }
        public Config()
        {
            SetDefaultStrings();
        }
        public void SetDefaultStrings()
        {
            name = String.Empty;
            passwordAdmin = "1988";
            passwordUser = "1234";
        }
    }

    public class RoomPlusDev
    {
// { "room": 1, "devType": 1, "devName": "Projector 1", "Brand":"Panasonic", "IP Address": "192.168.0.110" },
        public byte room;
        public bool selectedForSwitching;
        public ushort currentSourceVid;
        public ushort currentSourceAud;
        public ushort devType;
        public String devName;
        public String brand;
        public String address;
        public RoomPlusDev(byte room, ushort devType, String devName)
        {
            this.room = room;
            this.devType = devType;
            this.devName = devName;
        }
    }

    public class AudioDsp
    {
        public string address { get; set; }
        public ushort deviceID { get; set; } // eg HiQ = 0x03C703 
        public uint gainBlockID { get; set; } // last 6 digits of HiQ ( if HiQ = 0x03C703000100 then GainBlockID = 0x000100 )
        public uint matrixBlockID { get; set; }
        public ushort port { get; set; }
    }

    public class Level
    {
        public string name { get; set; }
        public byte level { get; set; }
        public bool mute { get; set; }
        public ushort room { get; set; }
    }

    public class Lights
    {
        public string address { get; set; }
        public List<string> presets { get; set; }
    }

    enum VidDev
    {
        DOCCAM = 1,
        PC = 2,
        LAPTOP = 3,
        CAM_1 = 4,
        CAM_2 = 5,
        WiP = 7,

        PROJ_1 = 1,
        PROJ_2 = 2,
        PROJ_3 = 3,
        REC_1 = 4,
        REC_2 = 5,
        AUDIO = 6,
        LCD = 7,
        VC = 8
    };

}