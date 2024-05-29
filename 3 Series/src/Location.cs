using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace Navitas
{
    public class Location
    {
        private byte id;

        private byte rampStep = 2;
        private byte nudgeStep = 1;
        public const byte MIN_VOL = 1;
        public const byte MAX_VOL = 100;

        public byte Id { get { return this.id; } }
        public string Name { get; set; }
        public byte Owner { get; set; }
        public List<byte> Owned { get; set; }

        public byte Volume { get; set; }
        public byte LightArea { get; set; }
        public bool mute { get; set; }

        public String WpAudioType { get; set; }
        public String CurrentVidSourceName { get; set; }
        public String CurrentAudSourceName { get; set; }

        public PowerStates Power { get; set; }

        public Location(byte id, String name)
        {
            this.id = id;
            this.Name = name;
            if (Owner < 1)
                Owner = id;
            Owned = new List<byte>();
            WpAudioType = "Auto";
            CurrentVidSourceName = String.Empty;
            CurrentAudSourceName = String.Empty;
        }

        public void RampVol(Direction dir)
        {
            //CrestronConsole.PrintLine("Location {0} RampVol", id);
            switch (dir)
            {
                case Direction.UP:
                    Volume = (Volume + rampStep > MAX_VOL) ? MAX_VOL : (byte)(Volume + rampStep);
                    break;
                case Direction.DOWN:
                    Volume = (Volume - rampStep < MIN_VOL) ? MIN_VOL : (byte)(Volume - rampStep);
                    break;
            }
        }
        public void NudgeVol(Direction dir)
        {
            //CrestronConsole.PrintLine("Location {0} RampVol", id);
            switch (dir)
            {
                case Direction.UP:
                    Volume = (Volume + nudgeStep > MAX_VOL) ? MAX_VOL : (byte)(Volume + nudgeStep);
                    break;
                case Direction.DOWN:
                    Volume = (Volume - nudgeStep < MIN_VOL) ? MIN_VOL : (byte)(Volume - nudgeStep);
                    break;
            }
        }

        public void SetVolume(byte val)
        {
            CrestronConsole.PrintLine("Location {0} SetVolume {1}", id, val);
            Volume = val;
        }
        /*
        public void SetMicVolume(byte val)
        {
            CrestronConsole.PrintLine("Location {0} SetMicVolume {1}", id, val);
            MicVolume = val;
        }
        */
        public void SetPower(PowerStates power)
        {
            this.Power = power;
        }

        public bool SetMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON: mute = true; break;
                case PowerStates.OFF: mute = false; break;
                case PowerStates.TOGGLE: mute = !mute; break;
            }
            return mute;
        }
        public bool SetMute(bool state)
        {
            return SetMute(state ? PowerStates.ON : PowerStates.OFF);
        }
        /*
        public bool SetMicMute(PowerStates state)
        {
            switch (state)
            {
                case PowerStates.ON: micMute = true; break;
                case PowerStates.OFF: micMute = false; break;
                case PowerStates.TOGGLE: micMute = !micMute; break;
            }
            return micMute;
        }
        public bool SetMicMute(bool state)
        {
            return SetMicMute(state ? PowerStates.ON : PowerStates.OFF);
        }
        */
    }
}