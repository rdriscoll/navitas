using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace Navitas
{
    public class LevelRamp
    {
        private ControlSystem cs; // should really make delegates and do this right
        public CTimer rampTimer;
        public Direction rampDirection;
        public Level level;

        public long rampTimerInterval = 200;
        public long rampTimerDueTime = 500;
        private byte rampStep = 2;
        private byte nudgeStep = 1;
        private const byte MIN_VOL = 1;
        private const byte MAX_VOL = 100;

        public LevelRamp(ControlSystem cs, Level level)
        {
            CrestronConsole.PrintLine("LevelRamp {0}", level.name);
            this.cs = cs;
            this.level = level;
        }
        public void DoVol(Direction dir) // nudge first
        {
            rampDirection = dir;
            switch (dir)
            {
                case Direction.UP:
                    level.level = (level.level + nudgeStep > MAX_VOL) ? MAX_VOL : (byte)(level.level + nudgeStep);
                    cs.SetMicLevel(level);
                    if (rampTimer == null)
                        rampTimer = new CTimer(rampTimerExpired, this, rampTimerDueTime, rampTimerInterval);
                    else
                        rampTimer.Reset(rampTimerDueTime, rampTimerInterval);
                    break;
                case Direction.DOWN:
                    level.level = (level.level - nudgeStep < MIN_VOL) ? MIN_VOL : (byte)(level.level - nudgeStep);
                    cs.SetMicLevel(level);
                    if (rampTimer == null)
                        rampTimer = new CTimer(rampTimerExpired, this, rampTimerDueTime, rampTimerInterval);
                    else
                        rampTimer.Reset(rampTimerDueTime, rampTimerInterval);
                    break;
                case Direction.STOP:
                    if (rampTimer != null)
                        rampTimer.Stop();
                    break;
            }
        }
        private void DoRamp(Direction dir)
        {
            switch (dir)
            {
                case Direction.UP:
                    level.level = (level.level + rampStep > MAX_VOL) ? MAX_VOL : (byte)(level.level + rampStep);
                    cs.SetMicLevel(level);
                    break;
                case Direction.DOWN:
                    level.level = (level.level - rampStep < MIN_VOL) ? MIN_VOL : (byte)(level.level - rampStep);
                    cs.SetMicLevel(level);
                    break;
                case Direction.STOP:
                    if (rampTimer != null)
                        rampTimer.Stop();
                    break;
            }
        }
        public void DoMute(PowerStates arg)
        {
            switch (arg)
            {
                case PowerStates.ON:
                    level.mute = true;
                    cs.SetMicmute(level);
                    break;
                case PowerStates.OFF:
                    level.mute = false;
                    cs.SetMicmute(level);
                    break;
                case PowerStates.TOGGLE:
                    DoMute(level.mute ? PowerStates.OFF : PowerStates.ON);
                    break;
            }
        }
        private void rampTimerExpired(object obj)
        {
            if (rampTimer != null)
                DoRamp(rampDirection);
        }
    }
}