using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace eft_dma_radar
{

    //public static class ProfileAPI
    //{
    //    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    //    public struct Info
    //    {
    //        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    //        public string nickname;
    //        public int experience;
    //        public int kills;
    //        public int deaths;
    //        public int inGameTime;
    //    };
    //
    //    //[DllImport("GetInfo.dll", CallingConvention = CallingConvention.Cdecl)]
    //    //public static extern void ApiInit(IntPtr phpin);
    //
    //
    //    //[DllImport("GetInfo.dll", CallingConvention = CallingConvention.Cdecl)]
    //    //public static extern int GetInfo(IntPtr ID);
    //
    //
    //}

    static class KmBox
      {
        [DllImport("kmNetLib.dll", EntryPoint = "kmNet_init", CallingConvention = CallingConvention.Cdecl)]
        public static extern int kmNet_init(IntPtr ip, IntPtr port, IntPtr mac);
        [DllImport("kmNetLib.dll")]
        public static extern int kmNet_mouse_move(short x, short y);
        [DllImport("kmNetLib.dll")]
        public static extern int kmNet_monitor(short port);
        [DllImport("kmNetLib.dll")]
        public static extern int kmNet_monitor_mouse_side2();
        [DllImport("kmNetLib.dll")]
        public static extern int kmNet_monitor_mouse_side1();
        //public static extern int kmNet_init(char ip, char port, char mac);

      }
    
}
