using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.DM;                // DM

namespace Navitas
{
    public class DmpsSwitch : Switch // this is a placeholder for when there is a DMPS so the switch is part of the controller
    {
        public CrestronCollection<HdMdNxMHdmiInput> HdmiInputs;
        public CrestronCollection<HdMdNxMHdmiOutput> HdmiOutputs;
        public DmpsSwitch(string paramDeviceName, object paramParent)
            : base(paramDeviceName, paramParent)
        {
            this.Name = paramDeviceName;
        }

        protected override void Dispose(bool paramDisposing)
        {
        }
        //public override eDeviceRegistrationUnRegistrationResponse Register();
        //public override eDeviceRegistrationUnRegistrationResponse UnRegister();

        public void AudioEnter()
        { }
    }
}