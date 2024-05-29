using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;

namespace Navitas
{
    public class Audio
    {
        void setVolume(uint output, int level)
        {
            if (level < 0)
                level = 65535 + level;
            /*
            Card.Dmps3AuxOutput myAudioOutput = SwitcherOutputs[output] as Card.Dmps3AuxOutput;
            myAudioOutput.MasterVolume.UShortValue = (ushort)level;
            CrestronConsole.Print("Set master volume for output{0}\n new level: {1}", output, myAudioOutput.MasterVolumeFeedBack.UShortValue);
            //CrestronConsole.Print("Set master volume for output{0}\n level: {1}\n returned level: {2}\n", output, level, myAudioOutput.MasterVolumeFeedBack); 

             */
        }
    }
}