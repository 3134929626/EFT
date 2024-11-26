using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Text;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Timers;
using Offsets;
using Vmmsharp;
using Vmmsharp.Internal;
using System.Security.Cryptography;
using OpenTK.Graphics.ES20;
using System.Diagnostics;
using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Reflection;
using static Vmmsharp.VmmProcess;
using SkiaSharp;
using static Vmmsharp.LeechCore;

namespace eft_dma_radar
{

    public static class Buritto
    {
        internal struct VMMDLL_MAP_EATENTRY
        {
            internal ulong vaFunction;

            internal uint dwOrdinal;

            internal uint oFunctionsArray;

            internal uint oNamesArray;

            internal uint _FutureUse1;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string uszFunction;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string uszForwardedFunction;
        }

        internal struct VMMDLL_MAP_EAT
        {
            internal uint dwVersion;

            internal uint dwOrdinalBase;

            internal uint cNumberOfNames;

            internal uint cNumberOfFunctions;

            internal uint cNumberOfForwardedFunctions;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            internal uint[] _Reserved1;

            internal ulong vaModuleBase;

            internal ulong vaAddressOfFunctions;

            internal ulong vaAddressOfNames;

            internal ulong pbMultiText;

            internal uint cbMultiText;

            internal uint cMap;
        }

        internal struct VMMDLL_MAP_MODULEENTRY
        {
            internal ulong vaBase;

            internal ulong vaEntry;

            internal uint cbImageSize;

            internal bool fWow64;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string uszText;

            internal uint _Reserved3;

            internal uint _Reserved4;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            internal string uszFullName;

            internal uint tp;

            internal uint cbFileSizeRaw;

            internal uint cSection;

            internal uint cEAT;

            internal uint cIAT;

            internal uint _Reserved2;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            internal ulong[] _Reserved1;

            internal nint pExDebugInfo;

            internal nint pExVersionInfo;
        }

        [DllImport("vmm", EntryPoint = "VMMDLL_MemReadEx", ExactSpelling = true)]
        public static extern bool VMMDLL_MemReadEx(
        IntPtr hVMM,
        uint dwPID,
        ulong qwA,
        byte[] pb,
        uint cb,
        out uint pcbReadOpt,
        uint flags
    );

        [DllImport("vmm", EntryPoint = "VMMDLL_PdbSymbolAddress", CharSet = CharSet.Ansi)]
        private static extern bool VMMDLL_PdbSymbolAddress(
            IntPtr hVMM,
            [MarshalAs(UnmanagedType.LPStr)] string szModule,
            [MarshalAs(UnmanagedType.LPStr)] string szSymbolName,
            out ulong pvaSymbolAddress
    );

        [DllImport("vmm", EntryPoint = "VMMDLL_Map_GetModuleFromNameW", ExactSpelling = true)]
        public unsafe static extern bool VMMDLL_Map_GetModuleFromNameW(
            IntPtr hVMM,
            uint dwPID,
            [MarshalAs(UnmanagedType.LPWStr)] string uszModuleName,
            out nint ppModuleMapEntry,
            uint flags
    );

        [DllImport("vmm", EntryPoint = "VMMDLL_PdbLoad", CharSet = CharSet.Ansi)]
        private static extern bool VMMDLL_PdbLoad(
            IntPtr hVMM,
            uint dwPID,
            ulong vaModuleBase,
            [Out] StringBuilder szModuleName
        );


        [DllImport("vmm", EntryPoint = "VMMDLL_Map_GetEATU", CharSet = CharSet.Unicode)]
        public unsafe static extern bool VMMDLL_Map_GetEATU(
            IntPtr hVMM,
            uint dwPid,
            [MarshalAs(UnmanagedType.LPStr)] string uszModuleName,
            out IntPtr ppEatMap
    );

        [DllImport("vmm", EntryPoint = "VMMDLL_MemFree", ExactSpelling = true)]
        public unsafe static extern void VMMDLL_MemFree(byte* pvMem);

        public static bool GetPdbSymbolAddress(IntPtr hVMM, string moduleName, string symbolName, out ulong symbolAddress)
        {
            return VMMDLL_PdbSymbolAddress(hVMM, moduleName, symbolName, out symbolAddress);
        }

        public static bool PdbLoad(IntPtr hVMM, uint pid, ulong moduleBase, out string moduleName)
        {
            StringBuilder buffer = new StringBuilder(32);
            bool result = VMMDLL_PdbLoad(hVMM, pid, moduleBase, buffer);
            moduleName = result ? buffer.ToString() : null;
            return result;
        }

        public unsafe static bool GetExportFr(Vmm vmm_handle, uint process_pid, string module_name, string export_name, out ulong fnc_addy)
        {
            fnc_addy = 0;

            nint pipi = IntPtr.Zero;
            EATEntry[] array = new EATEntry[0];
            int num = Marshal.SizeOf<VMMDLL_MAP_EAT>();
            int num2 = Marshal.SizeOf<VMMDLL_MAP_EATENTRY>();

            bool success = Buritto.VMMDLL_Map_GetEATU(vmm_handle, process_pid, module_name, out pipi);
            if (!success)
            {
                Program.Log("Failed to get EAT");
                return false;
            }

            VMMDLL_MAP_EAT eat = Marshal.PtrToStructure<VMMDLL_MAP_EAT>(pipi);
            if (eat.dwVersion != 3)
            {
                Program.Log("Invalid dwVersion");
                Buritto.VMMDLL_MemFree((byte*)((IntPtr)pipi).ToPointer());
                return false;
            }
            Program.Log($"Number of functions: {eat.cNumberOfFunctions.ToString()}");
            Program.Log($"Cmap Count: {eat.cMap.ToString()}");

            array = new EATEntry[eat.cMap];
            for (int i = 0; i < eat.cMap; i++)
            {
                VMMDLL_MAP_EATENTRY eatentry = Marshal.PtrToStructure<VMMDLL_MAP_EATENTRY>((nint)(((IntPtr)pipi).ToInt64() + num + i * num2));
                if (string.Equals(eatentry.uszFunction, export_name))
                {
                    Program.Log("Found function VA");
                    fnc_addy = eatentry.vaFunction;
                    return true;
                }
                Program.Log(eatentry.uszFunction);
            }

            Program.Log("Failed to find exported function");
            Buritto.VMMDLL_MemFree((byte*)((IntPtr)pipi).ToPointer());
            return false;
        }

    }


    [StructLayout(LayoutKind.Sequential)]
    public struct Matrik
    {
        public float M11, M12, M13, M14;
        public float M21, M22, M23, M24;
        public float M31, M32, M33, M34;
        public float M41, M42, M43, M44;

        public static Matrik Identity => new Matrik
        {
            M11 = 1f,
            M12 = 0f,
            M13 = 0f,
            M14 = 0f,
            M21 = 0f,
            M22 = 1f,
            M23 = 0f,
            M24 = 0f,
            M31 = 0f,
            M32 = 0f,
            M33 = 1f,
            M34 = 0f,
            M41 = 0f,
            M42 = 0f,
            M43 = 0f,
            M44 = 1f
        };

        public Matrik(
            float m11, float m12, float m13, float m14,
            float m21, float m22, float m23, float m24,
            float m31, float m32, float m33, float m34,
            float m41, float m42, float m43, float m44)
        {
            M11 = m11; M12 = m12; M13 = m13; M14 = m14;
            M21 = m21; M22 = m22; M23 = m23; M24 = m24;
            M31 = m31; M32 = m32; M33 = m33; M34 = m34;
            M41 = m41; M42 = m42; M43 = m43; M44 = m44;
        }

        public static Matrik operator *(Matrik left, Matrik right)
        {
            Matrik result = new Matrik();
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    float sum = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        sum += left[row, i] * right[i, col];
                    }
                    result[row, col] = sum;
                }
            }
            return result;
        }

        public float this[int row, int column]
        {
            get
            {
                return row switch
                {
                    0 => column switch { 0 => M11, 1 => M12, 2 => M13, 3 => M14, _ => throw new ArgumentOutOfRangeException(nameof(column)) },
                    1 => column switch { 0 => M21, 1 => M22, 2 => M23, 3 => M24, _ => throw new ArgumentOutOfRangeException(nameof(column)) },
                    2 => column switch { 0 => M31, 1 => M32, 2 => M33, 3 => M34, _ => throw new ArgumentOutOfRangeException(nameof(column)) },
                    3 => column switch { 0 => M41, 1 => M42, 2 => M43, 3 => M44, _ => throw new ArgumentOutOfRangeException(nameof(column)) },
                    _ => throw new ArgumentOutOfRangeException(nameof(row))
                };
            }
            set
            {
                switch (row)
                {
                    case 0:
                        switch (column)
                        {
                            case 0: M11 = value; break;
                            case 1: M12 = value; break;
                            case 2: M13 = value; break;
                            case 3: M14 = value; break;
                            default: throw new ArgumentOutOfRangeException(nameof(column));
                        }
                        break;
                    case 1:
                        switch (column)
                        {
                            case 0: M21 = value; break;
                            case 1: M22 = value; break;
                            case 2: M23 = value; break;
                            case 3: M24 = value; break;
                            default: throw new ArgumentOutOfRangeException(nameof(column));
                        }
                        break;
                    case 2:
                        switch (column)
                        {
                            case 0: M31 = value; break;
                            case 1: M32 = value; break;
                            case 2: M33 = value; break;
                            case 3: M34 = value; break;
                            default: throw new ArgumentOutOfRangeException(nameof(column));
                        }
                        break;
                    case 3:
                        switch (column)
                        {
                            case 0: M41 = value; break;
                            case 1: M42 = value; break;
                            case 2: M43 = value; break;
                            case 3: M44 = value; break;
                            default: throw new ArgumentOutOfRangeException(nameof(column));
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(row));
                }
            }
        }

        public static Matrik Transpose(Matrik pM)
        {
            Matrik pOut = new Matrik();
            pOut.M11 = pM.M11;
            pOut.M12 = pM.M21;
            pOut.M13 = pM.M31;
            pOut.M14 = pM.M41;
            pOut.M21 = pM.M12;
            pOut.M22 = pM.M22;
            pOut.M23 = pM.M32;
            pOut.M24 = pM.M42;
            pOut.M31 = pM.M13;
            pOut.M32 = pM.M23;
            pOut.M33 = pM.M33;
            pOut.M34 = pM.M43;
            pOut.M41 = pM.M14;
            pOut.M42 = pM.M24;
            pOut.M43 = pM.M34;
            pOut.M44 = pM.M44;
            return pOut;
        }
    }



    public class InputHandla //Credits to metick's DMA c++ library
    {
        public static bool done_init = false;
        private static int try_count = 0;
        VmmProcess winlogon;
        private uint win_logon_pid;
        private ulong gafAsyncKeyStateExport;
        private byte[] state_bitmap = new byte[64];
        private byte[] previous_state_bitmap = new byte[256 / 8];
        private Stopwatch stopwatch = Stopwatch.StartNew();

        private Vmm mem
        {
            get => Memory.VMM;
        }

        public bool Init()
        {
            if (done_init) return true;

            if (try_count > 3)
            {
                done_init = true;
                Program.Log("Failed to initialize input handler in 3+ attempts");
                return false;
            }

            var meow = mem.RegValueRead("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\CurrentBuild", out _);
            var Winver = Int32.Parse(System.Text.Encoding.Unicode.GetString(meow));


            var mrrp = mem.RegValueRead("HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\UBR", out _);
            uint Ubr = BitConverter.ToUInt32(mrrp);


            this.winlogon = mem.Process("winlogon.exe");
            this.win_logon_pid = winlogon.PID;

            if (winlogon.PID == 0)
            {
                Program.Log("Winlogon not found");
                try_count += 1;
                return false;
            }

            if (Winver > 22000)
            {
                Program.Log("Winver greater than 2200, attempting to read with offset");

                List<VmmProcess> crsissies = new List<VmmProcess>();

                foreach (var proc in mem.Processes)
                {
                    var info = proc.GetInfo();
                    if (info.sName == "csrss.exe" || info.sNameLong == "csrss.exe")
                    {
                        crsissies.Add(proc);
                    }
                }

                Program.Log($"Found: {crsissies.Count()} crsissies");

                foreach (var csrs in crsissies)
                {
                    ulong temp = csrs.GetModuleBase("win32ksgd.sys");
                    if (temp == 0) continue;
                    ulong g_session_global_slots = temp + 0x3110;

                    ulong? t1 = csrs.MemReadAs<ulong>(g_session_global_slots);
                    ulong? t2 = csrs.MemReadAs<ulong>(t1.Value);
                    ulong? t3 = csrs.MemReadAs<ulong>(t2.Value);
                    ulong user_session_state = t3.Value;


                    if (Winver >= 22631 && Ubr >= 3810)
                    {
                        Program.Log("Win11 detected");
                        this.gafAsyncKeyStateExport = user_session_state + 0x36A8;
                    }
                    else
                    {
                        Program.Log("Older windows version detected, Attempting to resolve by offset");
                        this.gafAsyncKeyStateExport = user_session_state + 0x3690;
                    }
                    if (gafAsyncKeyStateExport > 0x7FFFFFFFFFFF)
                        break;
                }
                if (gafAsyncKeyStateExport > 0x7FFFFFFFFFFF)
                {
                    Program.Log("Inputhandler success");
                    done_init = true;
                    return true;
                }
            }
            else
            {
                Program.Log("Older winver detected, attempting to resolve via EAT");
                ulong kitty = 0;
                bool success = Buritto.GetExportFr(mem, win_logon_pid | Vmm.PID_PROCESS_WITH_KERNELMEMORY, "win32kbase.sys", "gafAsyncKeyState", out kitty);

                if (success)
                {
                    if (kitty >= 0x7FFFFFFFFFFF)
                    {
                        Program.Log("Resolved export via getexport");
                        this.gafAsyncKeyStateExport = kitty;
                        done_init = true;
                        return true;
                    }
                }

                Program.Log("Failed to resolve via EAT, attempting to resolve with PDB");
                nint moduleinfo = IntPtr.Zero;
                if (Buritto.VMMDLL_Map_GetModuleFromNameW(mem, win_logon_pid | Vmm.PID_PROCESS_WITH_KERNELMEMORY, "win32kbase.sys", out moduleinfo, 0))
                {
                    Buritto.VMMDLL_MAP_EAT mod = Marshal.PtrToStructure<Buritto.VMMDLL_MAP_EAT>(moduleinfo);

                    string name;
                    if (Buritto.PdbLoad(mem, win_logon_pid | Vmm.PID_PROCESS_WITH_KERNELMEMORY, mod.vaModuleBase, out name))
                    {
                        Program.Log("Downloaded pdb");
                        ulong gafgaf = 0;
                        if (Buritto.GetPdbSymbolAddress(mem, name, "gafAsyncKeyState", out gafgaf))
                        {
                            Program.Log("Found PDB Symbol address");
                            if (gafgaf >= 0x7FFFFFFFFFFF)
                            {
                                Program.Log("Resolved export via pdb");
                                this.gafAsyncKeyStateExport = gafgaf;
                                done_init = true;
                                return true;
                            }
                        }
                    }
                }

            }
            Program.Log("Failed to find export");
            try_count += 1;
            return false;
        }

        public void UpdateKeys()
        {
            byte[] previous_key_state_bitmap = new byte[64];
            Array.Copy(state_bitmap, previous_key_state_bitmap, 64);

            bool success = Buritto.VMMDLL_MemReadEx(mem, win_logon_pid | Vmm.PID_PROCESS_WITH_KERNELMEMORY, gafAsyncKeyStateExport, state_bitmap, 64, out _, Vmm.FLAG_NOCACHE);

            if (!success)
            {
                Program.Log("You fucking failure");
                return;
            }

            for (int vk = 0; vk < 256; ++vk)
            {
                if ((state_bitmap[(vk * 2 / 8)] & 1 << vk % 4 * 2) != 0 && (previous_key_state_bitmap[(vk * 2 / 8)] & 1 << vk % 4 * 2) == 0)
                {
                    previous_state_bitmap[vk / 8] |= (byte)(1 << vk % 8);
                }
            }
        }

        public bool IsKeyDown(Int32 virtual_key_code)
        {
            if (gafAsyncKeyStateExport < 0x7FFFFFFFFFFF)
                return false;
            if (stopwatch.ElapsedMilliseconds > 1)
            {
                UpdateKeys();
                stopwatch.Restart();
            }
            return (state_bitmap[(virtual_key_code * 2 / 8)] & 1 << virtual_key_code % 4 * 2) != 0;
        }
    }
    public class Aimbot
    {
        private Player udPlayer;
        bool bLastHeld;
        public static InputHandla keyboard = new InputHandla();

        public KeyChecker _keyChecker = new KeyChecker();
        public static float Rad2Deg(float rad)
        {
            return rad * (180.0f / (float)Math.PI);
        }
        private static void NormalizeAngle(ref Vector2 angle)
        {
            var newX = angle.X switch
            {
                <= -180f => angle.X + 360f,
                > 180f => angle.X - 360f,
                _ => angle.X
            };

            var newY = angle.Y switch
            {
                > 90f => angle.Y - 180f,
                <= -90f => angle.Y + 180f,
                _ => angle.Y
            };

            angle = new Vector2(newX, newY);
        }

        public static Vector2 CalcAngle(Vector3 source, Vector3 destination)
        {
            Vector3 difference = source - destination;
            float length = difference.Length();
            Vector2 ret = new Vector2();

            ret.Y = (float)Math.Asin(difference.Y / length);
            ret.X = -(float)Math.Atan2(difference.X, -difference.Z);
            ret = new Vector2(ret.X * 57.29578f, ret.Y * 57.29578f);

            return ret;
        }


        public class KeyChecker
        {
            private const int MaxCallsPerSecond = 15;
            private const int Interval = 1000 / MaxCallsPerSecond;
            private bool _bHeld;
            private System.Timers.Timer _timer;

            public void Start()
            {
                _timer.Start();
            }

            public void Stop()
            {
                _timer.Stop();
            }
        }


        private CameraManager _cameraManager
        {
            get => Memory.CameraManager;
        }
        private ReadOnlyDictionary<string, Player> AllPlayers
        {
            get => Memory.Players;
        }
        private bool InGame
        {
            get => Memory.InGame;
        }

        private PlayerManager playamanaga
        {
            get => Memory.PlayerManager;
        }

        private Player LocalPlayer
        {
            get => Memory.LocalPlayer;
        }
        public Vector3 GetFireportPos()
        {
            if (!this.InGame || Memory.InHideout)
            {
                MessageBox.Show("Not in game");
                return new Vector3();
            }
            ulong handscontainer = Memory.ReadPtrChain(playamanaga._proceduralWeaponAnimation, new uint[] { ProceduralWeaponAnimation.FirearmContoller, FirearmController.Fireport, 0x10, 0x10 });
            Transform tranny = new Transform(handscontainer);
            Vector3 goofy = tranny.GetPosition();
            return new Vector3(goofy.X, goofy.Z, goofy.Y);
        }

        private float D3DXVec3Dot(Vector3 a, Vector3 b)
        {
            return (a.X * b.X +
                    a.Y * b.Y +
                    a.Z * b.Z);
        }

        public bool WorldToScreen(Vector3 _Enemy, out Vector2 _Screen)
        {
            _Screen = new Vector2(0, 0);

            Matrik viewMatrix = _cameraManager.ViewMatrix;
            Matrik temp = Matrik.Transpose(viewMatrix);

            Vector3 translationVector = new Vector3(temp.M41, temp.M42, temp.M43);
            Vector3 up = new Vector3(temp.M21, temp.M22, temp.M23);
            Vector3 right = new Vector3(temp.M11, temp.M12, temp.M13);

            float w = D3DXVec3Dot(translationVector, _Enemy) + temp.M44;

            if (w < 0.098f)
            {
                return false;
            }

            // Calculate screen coordinates
            float y = D3DXVec3Dot(up, _Enemy) + temp.M24;
            float x = D3DXVec3Dot(right, _Enemy) + temp.M14;

            _Screen.X = (1920f / 2f) * (1f + x / w);
            _Screen.Y = (1080f / 2f) * (1f - y / w);

            return true;
        }

        public Vector3 GetHead(Player player)
        {
            if (!this.InGame || Memory.InHideout || !player.IsAlive)
            {
                return new Vector3();
            }

            var boneMatrix = Memory.ReadPtrChain(player.PlayerBody, [0x28, 0x28, 0x10]);
            var bone = Vector3.Distance(player.Position, LocalPlayer.Position) < 60f ? (player.Name == "???") ? 31 : (uint)PlayerBones.HumanNeck : (player.Name == "???") ? 31 : (uint)PlayerBones.HumanHead;
            var pointer = Memory.ReadPtrChain(boneMatrix, [0x20 + ((frmMain.锁腿1 ? (uint)PlayerBones.HumanRThigh2 : bone) * 0x8), 0x10]);
            Transform headTranny = new Transform(pointer, false);
            return headTranny.GetPosition();
        }

        public bool GetHeadScr(Player player, out Vector2 screen, out Vector3 pos)
        {
            screen = new Vector2();
            pos = new Vector3();
            if (player.BoneTransforms != null && player.BoneTransforms.Count != 0 && !player.IsLocalPlayer && (!player.IsFriendlyActive || frmMain.瞄准队友) && player.IsAlive && player.IsActive && Vector3.Distance(player.Position, LocalPlayer.Position) < Program.Config.AimDis)
            {
                //Vector3 temp = GetHead(player);
                Vector3 temp = player.BonePositions;
                Vector3 HeadPos = new Vector3(temp.X, temp.Z, temp.Y);
                //Vector3 HeadPos = new Vector3(temp.X, temp.Z, temp.Y);
                if (WorldToScreen(HeadPos, out Vector2 scrpos))
                {
                    screen = scrpos;
                    pos = HeadPos;
                    return true;
                }
            }
            return false;
        }



        static DateTime 局_上次时间 = DateTime.Now;
        static Vector3 局_上次坐标 = new Vector3(0, 0, 0);
        static Vector3 局_移动速度 = new Vector3(0, 0, 0);
        static Vector3 局_返回坐标 = new Vector3(0, 0, 0);

        static float 局_武器速度 = 0;

        public Vector3 预判(Vector3 newPosition, Player player)
        {
            //float bullet_speed = 0;
            //float ballistic_coeff = 0;
            //float bullet_mass = 0;
            //float bullet_diam = 0;
            //var ammo_template = Memory.ReadPtrChain(this.LocalPlayer.Base, [Offsets.Player.HandsController, 0x60, 0x40, 0x190]);
            //
            //if (ammo_template != 0)
            //{
            //    bullet_speed = Memory.ReadValue<float>(ammo_template + 0x1AC);
            //    ballistic_coeff = Memory.ReadValue<float>(ammo_template + 0x1C0);
            //    bullet_mass = Memory.ReadValue<float>(ammo_template + 0x248);
            //    bullet_diam = Memory.ReadValue<float>(ammo_template + 0x24C);
            //}

            var localbone = new Vector3(this.LocalPlayer.Position.X, this.LocalPlayer.Position.Z, this.LocalPlayer.Position.Y);
            var angle = (localbone.Y - newPosition.Y) / Vector3.Distance(localbone, newPosition);
            var down = FormTrajectory2(Vector3.Distance(localbone, newPosition), new Vector3(0, 0, 0), new Vector3(this.LocalPlayer.bullet_speed, 0, 0), this.LocalPlayer.bullet_mass, this.LocalPlayer.bullet_diam, this.LocalPlayer.ballistic_coeff, out GStruct267[] trajectoryInfo2);
            var down1 = down * (float)Math.Sin(angle);
            newPosition.Y += down - Math.Abs(down1 / 2);


            //var 局_本次时间 = DateTime.Now;
            //var 局_间隔时间 = 局_本次时间 - 局_上次时间;
            //if (局_间隔时间 > TimeSpan.FromMilliseconds(120))
            //{
            //    局_上次时间 = 局_本次时间;
            //    局_间隔时间 = 局_本次时间 - 局_上次时间;
            //    局_上次坐标 = new Vector3(0, 0, 0);
            //    局_移动速度 = new Vector3(0, 0, 0);
            //    局_返回坐标 = newPosition;
            //}


            //if (局_上次坐标.X == 0 && 局_上次坐标.Y == 0 && 局_上次坐标.Z == 0)
            //{
            //    局_上次坐标 = newPosition;
            //}

            //if (Math.Abs(局_上次坐标.Z - newPosition.Z) > 0.5f)
            //{
            //    newPosition.Z = 局_上次坐标.Z;
            //
            //}

            //float 局_X差 = (newPosition.X - 局_上次坐标.X) * 1.0f;
            //float 局_Y差 = (newPosition.Y - 局_上次坐标.Y) * 0.6f;
            //float 局_Z差 = (newPosition.Z - 局_上次坐标.Z) * 1.0f;

            //if (局_间隔时间 > TimeSpan.FromMilliseconds(10))
            //{
            //printf("%lu\n", 局_间隔时间);
            //局_上次时间 = 局_本次时间;
            //局_上次坐标 = newPosition;

            //局_移动速度.X = 局_X差 / (float)局_间隔时间.TotalMilliseconds * 1000f;
            //局_移动速度.Y = 局_Y差 / (float)局_间隔时间.TotalMilliseconds * 1000f;
            //局_移动速度.Z = 局_Z差 / (float)局_间隔时间.TotalMilliseconds * 1000f;

            //[Class] -.GClass07A8 : MovementState ->[10C] vector3_0x10C : UnityEngine.Vector3
            局_移动速度 = Memory.ReadValue<Vector3>(player.MovementContext + 0x10C);
            //}


            //var speed = bullet_speed * Math.Exp((-0.000055 * ballistic_coeff * (bullet_diam / 100) * (bullet_diam / 100)) * Vector3.Distance(localbone, newPosition) / (bullet_mass / 100));
            var speed = FormTrajectory(Vector3.Distance(localbone, newPosition), new Vector3(0, 0, 0), new Vector3(this.LocalPlayer.bullet_speed, 0, 0), this.LocalPlayer.bullet_mass, this.LocalPlayer.bullet_diam, this.LocalPlayer.ballistic_coeff, out GStruct267[] trajectoryInfo);

            float 局_子弹飞行 = speed + (Program.Config.GamePing / 2000.0f);//_config.BulletSpeed
                                                                       //if (局_子弹飞行 < 0.08f)
                                                                       //{
                                                                       //    局_子弹飞行 = 0.08f;
                                                                       //}
            局_返回坐标.X = newPosition.X + 局_移动速度.X * 局_子弹飞行;
            局_返回坐标.Y = newPosition.Y + 局_移动速度.Y * 局_子弹飞行;
            局_返回坐标.Z = newPosition.Z + 局_移动速度.Z * 局_子弹飞行;
            //局_返回坐标 = newPosition;




            return 局_返回坐标;
        }
        public bool GetHeadScr2(Player player, out Vector2 screen, out Vector3 pos)
        {
            screen = new Vector2();
            pos = new Vector3();
            if (player.BoneTransforms != null && player.BoneTransforms.Count != 0 && !player.IsLocalPlayer && (!player.IsFriendlyActive || frmMain.瞄准队友) && player.IsAlive && player.IsActive && Vector3.Distance(player.Position, LocalPlayer.Position) < Program.Config.AimDis)
            {
                //Vector3 temp = GetHead(player);
                Vector3 temp = frmMain.锁腿1 ? player.BoneTransforms[1].GetPosition() : player.BoneTransforms[0].GetPosition();
                //Vector3 temp = frmMain.锁腿1 ? player.BoneTransforms[1].GetPosition() : (Vector3.Distance(player.Position, LocalPlayer.Position) < 60f ? player.BoneTransforms[2].GetPosition() : player.BoneTransforms[0].GetPosition());
                //var localbone = new Vector3(this.LocalPlayer.Position.X, this.LocalPlayer.Position.Z, this.LocalPlayer.Position.Y);

                Vector3 HeadPos = new Vector3(temp.X, temp.Z, temp.Y);
                //Vector3 HeadPos = new Vector3(temp.X, temp.Z, temp.Y);
                HeadPos = 预判(HeadPos, player);

                if (WorldToScreen(HeadPos, out Vector2 scrpos))
                {
                    screen = scrpos;
                    pos = HeadPos;
                    return true;
                }
            }
            return false;
        }

        //public bool GetHeadScr(Player player, out Vector2 screen, out Vector3 pos)
        //{
        //    screen = new Vector2();
        //    pos = new Vector3();
        //    if (player.BoneTransforms != null && player.BoneTransforms.Count != 0 && !player.IsLocalPlayer && player.IsAlive && player.IsActive && Vector3.Distance(player.Position, LocalPlayer.Position) < 100)
        //    {
        //        int headBoneIndex = player.RequiredBones.IndexOf(PlayerBones.HumanHead);
        //        if (headBoneIndex >= 0 && headBoneIndex < player.BoneTransforms.Count && player.BoneTransforms[headBoneIndex] != null)
        //        {
        //            if (player.BoneTransforms[headBoneIndex] is not null)
        //            {
        //                Vector3 temp = player.BoneTransforms[headBoneIndex].GetPosition();

        //                Vector3 HeadPos = new Vector3(temp.X, temp.Z, temp.Y);
        //                Vector2 scrpos = new Vector2(0, 0);

        //                if (WorldToScreen(HeadPos, out scrpos))
        //                {
        //                    screen = scrpos;
        //                    pos = HeadPos;
        //                    return true;
        //                }
        //            }
        //        }
        //    }
        //    return false;
        //}
        private static readonly List<Vector2> speedlist = new List<Vector2>
        {
            new Vector2(0f, 0.2629f),
            new Vector2(0.05f, 0.2558f),
            new Vector2(0.1f, 0.2487f),
            new Vector2(0.15f, 0.2413f),
            new Vector2(0.2f, 0.2344f),
            new Vector2(0.25f, 0.2278f),
            new Vector2(0.3f, 0.2214f),
            new Vector2(0.35f, 0.2155f),
            new Vector2(0.4f, 0.2104f),
            new Vector2(0.45f, 0.2061f),
            new Vector2(0.5f, 0.2032f),
            new Vector2(0.55f, 0.202f),
            new Vector2(0.6f, 0.2034f),
            new Vector2(0.7f, 0.2165f),
            new Vector2(0.725f, 0.223f),
            new Vector2(0.75f, 0.2313f),
            new Vector2(0.775f, 0.2417f),
            new Vector2(0.8f, 0.2546f),
            new Vector2(0.825f, 0.2706f),
            new Vector2(0.85f, 0.2901f),
            new Vector2(0.875f, 0.3136f),
            new Vector2(0.9f, 0.3415f),
            new Vector2(0.925f, 0.3734f),
            new Vector2(0.95f, 0.4084f),
            new Vector2(0.975f, 0.4448f),
            new Vector2(1f, 0.4805f),
            new Vector2(1.025f, 0.5136f),
            new Vector2(1.05f, 0.5427f),
            new Vector2(1.075f, 0.5677f),
            new Vector2(1.1f, 0.5883f),
            new Vector2(1.125f, 0.6053f),
            new Vector2(1.15f, 0.6191f),
            new Vector2(1.2f, 0.6393f),
            new Vector2(1.25f, 0.6518f),
            new Vector2(1.3f, 0.6589f),
            new Vector2(1.35f, 0.6621f),
            new Vector2(1.4f, 0.6625f),
            new Vector2(1.45f, 0.6607f),
            new Vector2(1.5f, 0.6573f),
            new Vector2(1.55f, 0.6528f),
            new Vector2(1.6f, 0.6474f),
            new Vector2(1.65f, 0.6413f),
            new Vector2(1.7f, 0.6347f),
            new Vector2(1.75f, 0.628f),
            new Vector2(1.8f, 0.621f),
            new Vector2(1.85f, 0.6141f),
            new Vector2(1.9f, 0.6072f),
            new Vector2(1.95f, 0.6003f),
            new Vector2(2f, 0.5934f),
            new Vector2(2.05f, 0.5867f),
            new Vector2(2.1f, 0.5804f),
            new Vector2(2.15f, 0.5743f),
            new Vector2(2.2f, 0.5685f),
            new Vector2(2.25f, 0.563f),
            new Vector2(2.3f, 0.5577f),
            new Vector2(2.35f, 0.5527f),
            new Vector2(2.4f, 0.5481f),
            new Vector2(2.45f, 0.5438f),
            new Vector2(2.5f, 0.5397f),
            new Vector2(2.6f, 0.5325f),
            new Vector2(2.7f, 0.5264f),
            new Vector2(2.8f, 0.5211f),
            new Vector2(2.9f, 0.5168f),
            new Vector2(3f, 0.5133f),
            new Vector2(3.1f, 0.5105f),
            new Vector2(3.2f, 0.5084f),
            new Vector2(3.3f, 0.5067f),
            new Vector2(3.4f, 0.5054f),
            new Vector2(3.5f, 0.504f),
            new Vector2(3.6f, 0.503f),
            new Vector2(3.7f, 0.5022f),
            new Vector2(3.8f, 0.5016f),
            new Vector2(3.9f, 0.501f),
            new Vector2(4f, 0.5006f),
            new Vector2(4.2f, 0.4998f),
            new Vector2(4.4f, 0.4995f),
            new Vector2(4.6f, 0.4992f),
            new Vector2(4.8f, 0.499f),
            new Vector2(5f, 0.4988f)
        };
        public struct GStruct267
        {
            public float time;
            public Vector3 position;
            public Vector3 velocity;
            public GStruct267(float time, Vector3 position, Vector3 velocity)
            {
                this.time = time;
                this.position = position;
                this.velocity = velocity;
            }
        };
        public static float CalculateG1DragCoefficient(float velocity)
        {
            int num = (int)Math.Floor(velocity / 343f / 0.05f);
            if (num <= 0)
            {
                return 0f;
            }
            if (num > speedlist.Count - 1)
            {
                return speedlist.Last<Vector2>().Y;
            }
            float num2 = speedlist[num - 1].X * 343f;
            float num3 = speedlist[num].X * 343f;
            float ballist = speedlist[num - 1].Y;
            return (speedlist[num].Y - ballist) / (num3 - num2) * (velocity - num2) + ballist;
        }
        static Vector3 gravity = new Vector3(0, -9.81f, 0);
        public static float FormTrajectory(float 距离, Vector3 zeroPosition, Vector3 zeroVelocity, float bulletMassGram, float bulletDiameterMilimeters, float ballisticCoefficient, out GStruct267[] trajectoryInfo)
        {
            trajectoryInfo = new GStruct267[600];
            float num = bulletMassGram / 1000f;
            float num2 = bulletDiameterMilimeters / 1000f;
            float num3 = num2 * num2 * 3.14159274f / 4f;
            float num4 = 0.01f;
            trajectoryInfo[0] = new GStruct267(0f, zeroPosition, zeroVelocity);
            bool flag = false;
            for (int i = 1; i < trajectoryInfo.Length; i++)
            {
                GStruct267 gstruct = trajectoryInfo[i - 1];
                Vector3 velocity = gstruct.velocity;
                Vector3 position = gstruct.position;
                float num5 = num * CalculateG1DragCoefficient(velocity.Length()) / ballisticCoefficient / (num2 * num2) * 0.0014223f;
                Vector3 a = gravity + Vector3.Normalize(velocity) * (-num5 * 1.2f * num3 * velocity.Length() * velocity.Length()) / (2f * num);
                Vector3 position2 = position + velocity * 0.01f + 5E-05f * a;
                Vector3 velocity2 = velocity + a * 0.01f;
                if (MathF.Sqrt(position2.X * position2.X + position2.Y * position2.Y) > 50 && !flag)
                {
                    position2.Y = zeroVelocity.Y * num4;
                    flag = true;
                }
                trajectoryInfo[i] = new GStruct267(num4, position2, velocity2);
                num4 += 0.01f;
            }

            //var json = new List<string>();
            //string txt = null;
            for (int i = 0; i < trajectoryInfo.Length; i++)
            {
                if (trajectoryInfo[i].position.X > 距离)
                {
                    //json.Add(trajectoryInfo[i].time.ToString() + "," + trajectoryInfo[i].position.ToString() + "," + trajectoryInfo[i].velocity.ToString());
                    return trajectoryInfo[i].time;
                    //break;
                }
            }
            return trajectoryInfo[trajectoryInfo.Length].time;
            //for (int i = 0; i < json.Count; i++)
            //{
            //    txt = txt + json[i].ToString() + "\n";
            //}
            //File.WriteAllText($"mapname.txt", txt);
        }
        public static float FormTrajectory2(float 距离, Vector3 zeroPosition, Vector3 zeroVelocity, float bulletMassGram, float bulletDiameterMilimeters, float ballisticCoefficient, out GStruct267[] trajectoryInfo)
        {
            trajectoryInfo = new GStruct267[600];
            float num = bulletMassGram / 1000f;
            float num2 = bulletDiameterMilimeters / 1000f;
            float num3 = num2 * num2 * 3.14159274f / 4f;
            float num4 = 0.01f;
            trajectoryInfo[0] = new GStruct267(0f, zeroPosition, zeroVelocity);
            bool flag = false;
            for (int i = 1; i < trajectoryInfo.Length; i++)
            {
                GStruct267 gstruct = trajectoryInfo[i - 1];
                Vector3 velocity = gstruct.velocity;
                Vector3 position = gstruct.position;
                float num5 = num * CalculateG1DragCoefficient(velocity.Length()) / ballisticCoefficient / (num2 * num2) * 0.0014223f;
                Vector3 a = gravity + Vector3.Normalize(velocity) * (-num5 * 1.2f * num3 * velocity.Length() * velocity.Length()) / (2f * num);
                Vector3 position2 = position + velocity * 0.01f + 5E-05f * a;
                Vector3 velocity2 = velocity + a * 0.01f;
                if (position2.X > 100f && !flag)
                {
                    position2.Y = zeroVelocity.Y * num4;
                    //velocity2.Y = zeroVelocity.Y;
                    flag = true;
                }
                trajectoryInfo[i] = new GStruct267(num4, position2, velocity2);
                num4 += 0.01f;
            }

            //var json = new List<string>();
            //string txt = null;
            for (int i = 0; i < trajectoryInfo.Length; i++)
            {
                if (MathF.Sqrt(trajectoryInfo[i].position.X * trajectoryInfo[i].position.X + trajectoryInfo[i].position.Y * trajectoryInfo[i].position.Y) > 距离)
                {
                    //json.Add(trajectoryInfo[i].time.ToString() + "," + trajectoryInfo[i].position.ToString() + "," + trajectoryInfo[i].velocity.ToString());
                    return Math.Abs(trajectoryInfo[i].position.Y);
                    //break;
                }
            }
            return Math.Abs(trajectoryInfo[trajectoryInfo.Length].position.Y);
            //for (int i = 0; i < json.Count; i++)
            //{
            //    txt = txt + json[i].ToString() + "\n";
            //}
            //File.WriteAllText($"mapname.txt", txt);
        }

        //float bullet_speed = 0;
        //float ballistic_coeff = 0;
        //float bullet_mass = 0;
        //float bullet_diam = 0;

        DateTime 上次自瞄时间 = DateTime.MinValue;
        Player 锁定玩家 = null;
        public void AimerBotter()
        {


            if (_cameraManager is null)
            {
                Program.Log("Gamara is ded");
                return;
            }

            this._cameraManager.GetViewmatrixAsync();


            try
            {
                if (!this.InGame || Memory.InHideout)
                {
                    MessageBox.Show("Not in game");
                    return;
                }

                Vector3 cameraPos = GetFireportPos();

                var players = this.AllPlayers
                ?.Select(x => x.Value)
                .Where(x => x.IsActive && x.IsAlive && !x.HasExfild); // Skip exfil'd players
                if (players is not null)
                {

                    List<玩家信息> validTargets = new List<玩家信息>();
                    Player clozestPlayer = null;
                    Vector2 clozestPlayerHead = Vector2.Zero;
                    Vector3 headPos2 = Vector3.Zero;
                    double lastDist = 999999;
                    foreach (var player in players)
                    {
                        GetHeadScr(player, out Vector2 HeadPos, out Vector3 headPos1);
                        Vector2 rel = new Vector2(HeadPos.X - (1920f / 2f), HeadPos.Y - (1080f / 2f));

                        var dist = Math.Sqrt(Math.Abs(rel.X) * Math.Abs(rel.Y));
                        //var fovdis = Vector2.Distance(rel, new Vector2(1920f / 2f, 1080f / 2f));

                        if (dist < lastDist)
                        {
                            clozestPlayer = player;
                            clozestPlayerHead = rel;
                            lastDist = dist;
                            headPos2 = headPos1;
                        }
                        if (lastDist > 60f)
                        {
                            clozestPlayer = null;
                            continue;
                        }
                    }
                    udPlayer = clozestPlayer;
                }


                if (udPlayer is not null && udPlayer.IsAlive && udPlayer.IsActive)
                {

                    GetHeadScr2(udPlayer, out Vector2 headPos, out Vector3 headPos1);
                    Vector2 rel = new Vector2(headPos.X - (1920f / 2f), headPos.Y - (1080f / 2f));
                    var dist = Math.Sqrt(Math.Abs(rel.X) * Math.Abs(rel.Y));

                    if (dist < 60f)
                    {

                        Vector2 ang = CalcAngle(cameraPos, headPos1);
                        var delta = ang - this.LocalPlayer.Rotation;
                        var delta2 = Vector2.Normalize(delta);
                        var gun_angle = new Vector3((delta2.X * 0.0174533f) / 1.5f, 0.0f, (delta2.Y * 0.0174533f) / 1.5f);

                        if (!float.IsNaN(ang.X) && !float.IsNaN(ang.Y))
                        {

                            Memory.WriteValue(playamanaga._proceduralWeaponAnimation + 0x224, new Vector3(gun_angle.X, -1.0f, gun_angle.Z * -1.0f));//[224] _shotDirection : UnityEngine.Vector3
                        }

                    }

                    //}

                }



            }
            catch (Exception ex)
            {
                Program.Log($"ERROR -> Aimer botter -> {ex.Message}\nStackTrace:{ex.StackTrace}");
            }
        }
        public void AimerBotterKmBox()
        {


            if (_cameraManager is null)
            {
                Program.Log("Gamara is ded");
                return;
            }

            this._cameraManager.GetViewmatrixAsync();


            try
            {
                if (!this.InGame || Memory.InHideout)
                {
                    MessageBox.Show("Not in game");
                    return;
                }


                bool bHeld = frmMain.上侧键;
                Vector3 cameraPos = GetFireportPos();
                //bool bHeld = KmBox.kmNet_monitor_mouse_side2() == 1;

                if (bHeld && bHeld == bLastHeld && udPlayer is not null && udPlayer.IsAlive && udPlayer.IsActive)
                {
                    if (锁定玩家 != null && 锁定玩家 != udPlayer)
                    {
                        锁定玩家 = udPlayer;
                        Thread.Sleep(130);
                        //return;
                    }


                    GetHeadScr2(udPlayer, out Vector2 headPos, out Vector3 headPos1);
                    Vector2 rel = new Vector2(headPos.X - (1920f / 2f), headPos.Y - (1080f / 2f));
                    var dist = Math.Sqrt(Math.Abs(rel.X) * Math.Abs(rel.Y));
                    //var fovdis = Vector2.Distance(rel, new Vector2(1920f / 2f, 1080f / 2f));

                    //if (DateTime.Now - 上次自瞄时间 > TimeSpan.FromMilliseconds(3))
                    //{
                    if (dist < 60f)
                    {
                        //KmBox.kmNet_mouse_move(Convert.ToInt16(Math.Round((rel.X) * 0.25, 0)), Convert.ToInt16(Math.Round((rel.Y) * 0.25, 0)));
                        Vector2 ang = CalcAngle(cameraPos, headPos1);

                        if (!float.IsNaN(ang.X) && !float.IsNaN(ang.Y))
                        {
                            LocalPlayer.SetRotationFr(ang);
                        }

                        锁定玩家 = udPlayer;
                        上次自瞄时间 = DateTime.Now;
                    }

                    //}

                }
                else if (bHeld && (bHeld != bLastHeld || udPlayer is null || !udPlayer.IsAlive || !udPlayer.IsActive))
                {
                    var players = this.AllPlayers
                    ?.Select(x => x.Value)
                    .Where(x => x.IsActive && x.IsAlive && !x.HasExfild); // Skip exfil'd players
                    if (players is not null)
                    {

                        List<玩家信息> validTargets = new List<玩家信息>();
                        Player clozestPlayer = null;
                        Vector2 clozestPlayerHead = Vector2.Zero;
                        Vector3 headPos2 = Vector3.Zero;
                        double lastDist = 999999;
                        foreach (var player in players)
                        {
                            GetHeadScr(player, out Vector2 HeadPos, out Vector3 headPos1);
                            Vector2 rel = new Vector2(HeadPos.X - (1920f / 2f), HeadPos.Y - (1080f / 2f));

                            var dist = Math.Sqrt(Math.Abs(rel.X) * Math.Abs(rel.Y));
                            //var fovdis = Vector2.Distance(rel, new Vector2(1920f / 2f, 1080f / 2f));
                            if (dist < 60f)
                            {
                                validTargets.Add(new 玩家信息(Vector3.Distance(player.Position, LocalPlayer.Position), dist, player, headPos1));
                            }

                            if (dist < lastDist)
                            {
                                clozestPlayer = player;
                                clozestPlayerHead = rel;
                                lastDist = dist;
                                headPos2 = headPos1;
                            }
                            if (lastDist > 60f)
                            {
                                clozestPlayer = null;
                                continue;
                            }
                        }
                        if (validTargets.Count > 0)
                        {
                            // Multiple targets in FOV, prioritize the one closest to the player
                            var closestPlayer = validTargets.Where(x => x.世界距离 < 80f);

                            if (closestPlayer.Count() > 0)
                            {
                                var 最近目标 = closestPlayer
                                    .OrderBy(x => x.屏幕距离)
                                    .FirstOrDefault();
                                if (锁定玩家 != null && 锁定玩家 != 最近目标.Player)
                                {
                                    锁定玩家 = 最近目标.Player;
                                    Thread.Sleep(130);
                                    GetHeadScr(锁定玩家, out Vector2 HeadPos, out _);
                                    Vector2 rel = new Vector2(HeadPos.X - (1920f / 2f), HeadPos.Y - (1080f / 2f));
                                    clozestPlayerHead = rel;
                                    //return;
                                }

                                Vector2 ang = CalcAngle(cameraPos, 最近目标.目标坐标);

                                if (!float.IsNaN(ang.X) && !float.IsNaN(ang.Y))
                                {
                                    LocalPlayer.SetRotationFr(ang);
                                }

                                锁定玩家 = 最近目标.Player;
                                上次自瞄时间 = DateTime.Now;

                                //}
                                udPlayer = 最近目标.Player;
                            }
                            else
                            {
                                if (锁定玩家 != null && 锁定玩家 != clozestPlayer)
                                {
                                    锁定玩家 = clozestPlayer;
                                    Thread.Sleep(130);
                                    GetHeadScr(锁定玩家, out Vector2 HeadPos, out _);
                                    Vector2 rel = new Vector2(HeadPos.X - (1920f / 2f), HeadPos.Y - (1080f / 2f));
                                    clozestPlayerHead = rel;
                                    //return;
                                }
                                //GetHeadScr2(clozestPlayer, out Vector2 HeadPos, out _);
                                //Vector2 rel = new Vector2(HeadPos.X - (1920f / 2f), HeadPos.Y - (1080f / 2f));
                                //if (DateTime.Now - 上次自瞄时间 > TimeSpan.FromMilliseconds(3))
                                //{
                                //KmBox.kmNet_mouse_move(Convert.ToInt16(Math.Round((clozestPlayerHead.X) * 0.25, 0)), Convert.ToInt16(Math.Round((clozestPlayerHead.Y) * 0.25, 0)));
                                Vector2 ang = CalcAngle(cameraPos, headPos2);

                                if (!float.IsNaN(ang.X) && !float.IsNaN(ang.Y))
                                {
                                    LocalPlayer.SetRotationFr(ang);
                                }

                                锁定玩家 = clozestPlayer;
                                上次自瞄时间 = DateTime.Now;

                                //}
                                udPlayer = clozestPlayer;
                            }
                        }

                    }
                }
                if (!bHeld)
                {
                    锁定玩家 = null;
                    //udPlayer = null;
                }
                bLastHeld = bHeld;
                //Thread.Sleep(1);
            }
            catch (Exception ex)
            {
                Program.Log($"ERROR -> Aimer botter -> {ex.Message}\nStackTrace:{ex.StackTrace}");
            }
        }
        public class 玩家信息
        {
            public float 世界距离 { get; set; }
            public double 屏幕距离 { get; set; }
            public Player Player { get; set; }
            public Vector3 目标坐标 { get; set; }
            public 玩家信息(float 世界距离1, double 屏幕距离1, Player Player1, Vector3 目标坐标1)
            {
                世界距离 = 世界距离1;
                屏幕距离 = 屏幕距离1;
                Player = Player1;
                目标坐标 = 目标坐标1;

            }
        }
    }
}