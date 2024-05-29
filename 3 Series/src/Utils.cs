using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using System.Text.RegularExpressions;
using Crestron.SimplSharp.Cryptography;

namespace Navitas
{
    public static class Utils
    {
        public static string CreateHexPrintableString(string str)
        {
            byte[] b = GetBytes(str); // UTF8 creates 2 bytes when over \x80
            return CreateHexPrintableString(b);
        }
        public static string CreateHexPrintableString(byte[] bArgs)
        {
            string strOut_ = "";
            foreach (byte bIndex_ in bArgs)
            {
                //string sHexOutput_ = String.Format("{0:X}", bIndex_);
                //strOut_ += @"\x" + String.Format("{0:X2}", sHexOutput_);
                strOut_ += String.Format("\\x{0:X2}", bIndex_);
            }
            return strOut_;
        }
        public static string CreatePrintableString(string str, bool debugAsHex)
        {
            if (debugAsHex)
                return CreateHexPrintableString(str);
            else
            {
                byte[] b = GetBytes(str); // UTF8 creates 2 bytes when over \x80
                return CreatePrintableString(b, debugAsHex);
            }
        }
        public static string CreatePrintableString(byte[] bArgs, bool debugAsHex)
        {
            if (debugAsHex)
                return CreateHexPrintableString(bArgs);
            else
            {
                string strOut_ = "";
                foreach (byte bIndex_ in bArgs)
                {
                    string sOutput_ = String.Empty;
                    if (bIndex_ < 0x20 || bIndex_ > 0x7F)
                        strOut_ += String.Format("\\x{0:X2}", bIndex_);
                    else
                        strOut_ += GetString(new byte[] { bIndex_ });
                }
                return strOut_;
            }
        }
        public static byte[] GetBytesFromAsciiHexString(string str)
        {
            String p1 = @"(\\[xX][0-9a-fA-F]{2}|.)";
            Regex r1 = new Regex(p1);
            //String p2 = @"\\x([xX][0-9a-fA-F]{2})";
            //Regex r2 = new Regex(p2);
            MatchCollection m = r1.Matches(str);
            string s1 = "";
            foreach (Match m1 in m)
            {
                string s2 = m1.Value;
                if (m1.Value.IndexOf("\\x") > -1)
                {
                    string s3 = m1.Value.Remove(0, 2);
                    byte b2 = Byte.Parse(s3, System.Globalization.NumberStyles.HexNumber);
                    s1 = s1 + GetString(new byte[] { b2 });
                }
                else
                {
                    byte[] b1 = Encoding.Default.GetBytes(m1.Value);
                    s1 = s1 + GetString(b1);
                }
            }
            byte[] b = GetBytes(s1);
            return b;
        }
        public static string GetString(byte[] bytes)
        {
            return Encoding.GetEncoding("ISO-8859-1").GetString(bytes, 0, bytes.Length);
        }
        public static byte[] GetBytes(string str) // because no encodings work the way we need
        {
            return Encoding.GetEncoding("ISO-8859-1").GetBytes(str);
        }
        public static byte[] GetBytesFromLong(long val, int numBytes)
        {
            byte[] b = new byte[numBytes];
            long temp = val;
            for (int i = numBytes - 1; i > -1; i--)
            {
                b[i] = (byte)(temp % 0x100);
                temp = temp >> 8;
            }
            return b;
        }
        public static bool GetBit(byte b, int bitNumber)
        {
            return (b & (1 << bitNumber)) != 0;
        }
        public static int SetBit(int b, byte bitNumber, bool val)
        {
            int r = val == true ? b | (1 << bitNumber) : b & (Byte.MaxValue - (1 << bitNumber));
            return r;
        }
        public static byte AddBytes(byte[] b)
        {
            byte r = new byte();
            for (int i = 0; i < b.Length; i++)
                r += b[i];
            return r;
        }

        public static byte AddBytes(string str)
        {
            return AddBytes(GetBytes(str));
        }
        public static int atoi(string strArg) // "hello 123 there" returns 123, because ToInt throws exceptions when non numbers are inserted
        {
            String m = Regex.Match(strArg, @"\d+").Value;
            return (m.Length == 0 ? 0 : Convert.ToInt32(m));
        }
        public static int ConvertRanges(int val, int inMin, int inMax, int outMin, int outMax)
        {
            int inRange = inMax - inMin;
            int outRange = outMax - outMin;
            int result = ((val - inMin) * outRange) / inRange + outMin;
            return result;
        }

        public static string MD5(string s)
        {
            MD5CryptoServiceProvider x = new MD5CryptoServiceProvider();
            return BitConverter.ToString(x.ComputeHash(Encoding.Default.GetBytes(s))).Replace("-", string.Empty);
        }

        #region extensions

        public static string Remove(this string str, int startIndex)
        {
            return str.Remove(startIndex, str.Length - startIndex);
        }

        #endregion
    }
    #region EvenArgs

    public class UShortEventArgs : EventArgs
    {
        public ushort val;
        public UShortEventArgs(ushort val)
        {
            this.val = val;
        }
    }
    public class BoolEventArgs : EventArgs
    {
        public bool val;
        public BoolEventArgs(bool val)
        {
            this.val = val;
        }
    }
    public class StringEventArgs : EventArgs
    {
        public string str;
        public StringEventArgs(string str)
        {
            this.str = str;
        }
    }
    public class PowerEventArgs : EventArgs
    {
        public PowerStates val;
        public ushort mSecsRemaining;
        public PowerEventArgs(PowerStates val, ushort mSecsRemaining)
        {
            this.val = val;
            this.mSecsRemaining = mSecsRemaining;
        }
    }
    public class DisplaySourceEventArgs : EventArgs
    {
        public DisplaySources val;
        public DisplaySourceEventArgs(DisplaySources val)
        {
            this.val = val;
        }
    }
    public class LevelEventArgs : EventArgs
    {
        public ushort val { get; set; }
        public ushort barGraphLevel; // 0-65535
        public LevelEventArgs() { }
        public LevelEventArgs(ushort val, ushort barGraphLevel)
        {
            this.val = val;
            this.barGraphLevel = barGraphLevel;
        }
    }
    public class BSSAudioNodeEventArgs : EventArgs
    {
        public object val;
        public BSSAudioNodeEventArgs(BssNode val)
        {
            this.val = val;
        }
    }

    #endregion
    #region enums

    public enum PowerStates
    {
        OFF = 0,
        ON = 1,
        WARMING = 2,
        COOLING = 3,
        TOGGLE = 4
    };
    public enum SwitchType
    {
        AUDIO = 0,
        VIDEO = 1,
    };
    public enum DisplaySources
    {
        HDMI_1 = 0,
        HDMI_2 = 1,
        HDMI_3 = 2,
        HDMI_4 = 3,
        DTV_1 = 4,
        DTV_2 = 5,
        ATV_1 = 6,
        RGB_1 = 7,
        RGB_2 = 8,
        RGB_3 = 9,
        VID_1 = 10,
        VID_2 = 11,
        COMP_1 = 12,
        COMP_2 = 13,
        SVID_1 = 14,
        USB_1 = 15,
        USB_2 = 16,
        LAN_1 = 17,
        LAN_2 = 18,
        DVI_1 = 19,
        DVI_2 = 20,
        HDBT_1 = 21,
        DP_1 = 22,
        DP_2 = 23
    };
    public enum Direction
    {
        STOP = 0,
        UP = 1,
        DOWN = 2
    };

    #endregion

}