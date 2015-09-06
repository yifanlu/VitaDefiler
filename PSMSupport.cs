using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using Mono.Cecil.Metadata;
using Mono.Debugger.Soft;

namespace VitaDefiler.PSM
{
    public enum PsmDeviceType
    {
        Simulator,
        PsVita,
        Android
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ScePsmDevice
    {
        public Guid guid;
        public PsmDeviceType type;
        public int online;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x80)]
        public char[] deviceID;

    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct ScePsmApplication
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x80)]
        public char[] name;
        public int size;
    }

    public enum ScePsmDevErrorCode
    {
        CannotAccessStorage = -2147418107,
        InvalidAppID = -2147418109,
        InvalidFilepath = -2147418108,
        InvalidPackage = -2147418110,
        NoConnection = -2147418111,
        Ok = 0,
        StorageFull = -2147418106,
        VersionHost = -2147418100,
        VersionTarget = -2147418099
    }

    public enum ScePsmDrmKpubUploadState
    {
        NEW_REGIST,
        OVERWRITE
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi, BestFitMapping = false)]
    public delegate void PsmDeviceConsoleCallback(string message);

    public class TransportFunctions
    {
        private static class ScePsmHT32
        {
            // Fields
#if USE_UNITY
            private const string NATIVE_DLL = @"support\unity\tools\lib\host_transport32.dll";
#else
            private const string NATIVE_DLL = @"support\psm\tools\lib\host_transport32.dll";
#endif

            // Methods
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTCloseHandle")]
            public static extern int CloseHandle(int src, int handle);
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTCreateFile", CharSet = CharSet.Ansi)]
            public static extern int CreateFile(int src, string comname);
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTGetReceiveSize")]
            public static extern int GetReceiveSize(int src, int hFile);
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTReadFile", SetLastError = true)]
            public static extern int ReadFile(int src, int hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead);
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTWriteFile", SetLastError = true)]
            public static extern int WriteFile(int src, int hFile, IntPtr lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten);
        }

        private static class ScePsmHT64
        {
            // Fields
#if USE_UNITY
            private const string NATIVE_DLL = @"support\unity\tools\lib\host_transport64.dll";
#else
            private const string NATIVE_DLL = @"support\psm\tools\lib\host_transport64.dll";
#endif

            // Methods
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTCloseHandle")]
            public static extern int CloseHandle(int src, int handle);
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTCreateFile", CharSet = CharSet.Ansi)]
            public static extern int CreateFile(int src, string comname);
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTGetReceiveSize")]
            public static extern int GetReceiveSize(int src, int hFile);
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTReadFile", SetLastError = true)]
            public static extern int ReadFile(int src, int hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead);
            [DllImport(NATIVE_DLL, EntryPoint = "scePsmHTWriteFile", SetLastError = true)]
            public static extern int WriteFile(int src, int hFile, IntPtr lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten);
        }

        public static int CloseHandle(int src, int handle)
        {
            return ((IntPtr.Size == 8) ? ScePsmHT64.CloseHandle(src, handle) : ScePsmHT32.CloseHandle(src, handle));
        }

        public static int CreateFile(int src, string comname)
        {
            return ((IntPtr.Size == 8) ? ScePsmHT64.CreateFile(src, comname) : ScePsmHT32.CreateFile(src, comname));
        }

        public static int GetReceiveSize(int src, int hFile)
        {
            return ((IntPtr.Size == 8) ? ScePsmHT64.GetReceiveSize(src, hFile) : ScePsmHT32.GetReceiveSize(src, hFile));
        }

        public static int ReadFile(int src, int hFile, IntPtr lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead)
        {
            return ((IntPtr.Size == 8) ? ScePsmHT64.ReadFile(src, hFile, lpBuffer, nNumberOfBytesToRead, out lpNumberOfBytesRead) : ScePsmHT32.ReadFile(src, hFile, lpBuffer, nNumberOfBytesToRead, out lpNumberOfBytesRead));
        }

        public static int WriteFile(int src, int hFile, IntPtr lpBuffer, uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten)
        {
            return ((IntPtr.Size == 8) ? ScePsmHT64.WriteFile(src, hFile, lpBuffer, nNumberOfBytesToWrite, out lpNumberOfBytesWritten) : ScePsmHT32.WriteFile(src, hFile, lpBuffer, nNumberOfBytesToWrite, out lpNumberOfBytesWritten));
        }

        public static string GetVitaPortWithSerial(string serial)
        {
            ManagementClass class2 = new ManagementClass("Win32_SerialPort");
            string vitaDebugPnpDeviceID = @"USB\VID_054C&PID_069B\" + serial;
            foreach (ManagementBaseObject obj2 in class2.GetInstances())
            {
                if ((obj2.GetPropertyValue("PNPDeviceID") as string).IndexOf(vitaDebugPnpDeviceID, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    string str2 = obj2.GetPropertyValue("Caption").ToString();
                    if ((str2 != null) && str2.Contains("PSM USB Debug"))
                    {
                        string str3 = obj2.GetPropertyValue("DeviceID").ToString();
                        if (!string.IsNullOrEmpty(str3))
                        {
                            return str3;
                        }
                    }
                }
            }
            return null;
        }
    }

    internal delegate int scePsmDevConnect([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);

    internal delegate int scePsmDevCreatePackage([MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string dirForPack);

    internal delegate int scePsmDevDisconnect([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);

    internal delegate int scePsmDevExistAppExeKey([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, long accountId, [MarshalAs(UnmanagedType.LPStr)] string titleIdentifier, [MarshalAs(UnmanagedType.LPStr)] string env);

    internal delegate int scePsmDevExtractPackage([MarshalAs(UnmanagedType.LPStr)] string dirExtract, [MarshalAs(UnmanagedType.LPStr)] string packageFile);

    internal delegate int scePsmDevGetDeviceSeed([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string filename);

    internal delegate int scePsmDevGetErrStr([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder errstr);

    internal delegate int scePsmDevGetLog([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder logstr);

    internal delegate int scePsmDevGetPsmAppStatus([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);

    internal delegate int scePsmDevInstall([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string appId);

    internal delegate int scePsmDevKill([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);

    internal delegate int scePsmDevLaunch([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appId, bool debug, bool profile, bool keepnet, bool logwaiting, [MarshalAs(UnmanagedType.LPStr)] string arg);

    internal delegate int scePsmDevListApplications([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, SizeConst = 100)] ScePsmApplication[] appArray);

    internal delegate int scePsmDevListDevices([In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, SizeConst = 8)] ScePsmDevice[] deviceArray);

    internal delegate int scePsmDevPickFileFromPackage([MarshalAs(UnmanagedType.LPStr)] string outName, [MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string inName);

    internal delegate int scePsmDevRequestEndPsmApp([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, string msg);

    internal delegate int scePsmDevResponseEndPsmApp([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, int response, string option);

    internal delegate int scePsmDevSetAdbExePath(string path);

    internal delegate int scePsmDevSetAppExeKey([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string filename);

    internal delegate int scePsmDevSetConsoleWrite([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, IntPtr proc);

    internal delegate int scePsmDevUninstall([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appId);

    internal delegate int scePsmDevVersion([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);

    internal delegate int scePsmDevGetAgentVersion([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] psm_devagent, [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] host_transport);
    
    internal delegate int scePsmDevLaunchUnity([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appName, int argnum, [MarshalAs(UnmanagedType.LPArray)] string[] argstr);

    internal static class PSMFunctions
    {
        // Fields
        private static scePsmDevConnect _scePsmDevConnect;
        private static scePsmDevCreatePackage _scePsmDevCreatePackage;
        private static scePsmDevDisconnect _scePsmDevDisconnect;
        private static scePsmDevExistAppExeKey _scePsmDevExistAppExeKey;
        private static scePsmDevExtractPackage _scePsmDevExtractPackage;
        private static scePsmDevGetDeviceSeed _scePsmDevGetDeviceSeed;
        private static scePsmDevGetErrStr _scePsmDevGetErrStr;
        private static scePsmDevGetLog _scePsmDevGetLog;
        private static scePsmDevGetPsmAppStatus _scePsmDevGetPsmAppStatus;
        private static scePsmDevInstall _scePsmDevInstall;
        private static scePsmDevKill _scePsmDevKill;
        private static scePsmDevLaunch _scePsmDevLaunch;
        private static scePsmDevListApplications _scePsmDevListApplications;
        private static scePsmDevListDevices _scePsmDevListDevices;
        private static scePsmDevPickFileFromPackage _scePsmDevPickFileFromPackage;
        private static scePsmDevRequestEndPsmApp _scePsmDevRequestEndPsmApp;
        private static scePsmDevResponseEndPsmApp _scePsmDevResponseEndPsmApp;
        private static scePsmDevSetAdbExePath _scePsmDevSetAdbExePath;
        private static scePsmDevSetAppExeKey _scePsmDevSetAppExeKey;
        private static scePsmDevSetConsoleWrite _scePsmDevSetConsoleWrite;
        private static scePsmDevUninstall _scePsmDevUninstall;
        private static scePsmDevVersion _scePsmDevVersion;
        private static scePsmDevGetAgentVersion _scePsmDevGetAgentVersion;
        private static scePsmDevLaunchUnity _scePsmDevLaunchUnity;

        private const int APPLICATION_NUM = 100;
        private const int DEVICE_NUM = 8;
#if USE_UNITY
        private const string dll32 = @"support\unity\tools\lib\psm_device32.dll";
        private const string dll64 = @"support\unity\tools\lib\psm_device64.dll";
#else
        private const string dll32 = @"support\psm\tools\lib\psm_device32.dll";
        private const string dll64 = @"support\psm\tools\lib\psm_device64.dll";
#endif
        private static int mDeviceNum = 0;
        private static ScePsmDevice[] mDevices = new ScePsmDevice[8];
        private static Mutex mInfoMutex = new Mutex();
        private static Dictionary<Guid, Mutex> mutexTable = new Dictionary<Guid, Mutex>();
        public const int SCE_PSM_DEVICE_OK = 0;
        public const int SCE_PSM_HANDLE_MIN = 1;
        public const int TARGET_ANDROID = 1;
        public static string[] TARGET_NAME = new string[] { "Simulater", "Android", "Vita" };
        public const int TARGET_PS_VITA = 2;
        public const int TARGET_SIMULATOR = 0;

        static PSMFunctions()
        {
            Initialize();
        }

        // Methods
        public static int Connect(Guid guid)
        {
            mInfoMutex.WaitOne();
            int num = _scePsmDevConnect(guid);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevConnect(0x{0:X8} : {1})", num, GetErrStr(num));
                mInfoMutex.ReleaseMutex();
                return num;
            }
            mInfoMutex.ReleaseMutex();
            return num;
        }

        public static int CreatePackage(string packageFile, string dirForPack)
        {
            int num = _scePsmDevCreatePackage(packageFile, dirForPack);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevCreatePackage(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int Disconnect(Guid deviceGuid)
        {
            mInfoMutex.WaitOne();
            int num = _scePsmDevDisconnect(deviceGuid);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevDisconnect(0x{0:X8} : {1})", num, GetErrStr(num));
                mInfoMutex.ReleaseMutex();
                return num;
            }
            mInfoMutex.ReleaseMutex();
            return num;
        }

        public static int ExistAppExeKey(Guid deviceGuid, long accountId, string titleIdentifier, string env)
        {
            int num = _scePsmDevExistAppExeKey(deviceGuid, accountId, titleIdentifier, env);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevExistAppExeKey(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int ExtractPackage(string dirExtract, string packageFile)
        {
            int num = _scePsmDevExtractPackage(dirExtract, packageFile);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevExtractPackage(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int GetDeviceSeed(Guid deviceGuid, string filename)
        {
            int num = _scePsmDevGetDeviceSeed(deviceGuid, filename);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevGetDeviceSeed(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static string GetErrStr(int code)
        {
            string[] strArray = new string[] { 
            "SCE_PSM_DEVICE_OK", "SCE_PSM_DEVICE_NO_CONNECTION", "SCE_PSM_DEVICE_INVALID_PACKAGE", "SCE_PSM_DEVICE_INVALID_APPID", "SCE_PSM_DEVICE_INVALID_FILEPATH", "SCE_PSM_DEVICE_CANNOT_ACCESS_STORAGE", "SCE_PSM_DEVICE_STORAGE_FULL", "SCE_PSM_DEVICE_CONNECT_ERROR", "SCE_PSM_DEVICE_CREATE_PACKAGE", "SCE_PSM_DEVICE_CONNECTED_DEVICE", "SCE_PSM_DEVICE_TIMEOUT", "SCE_PSM_DEVICE_NO_LAUNCH_TARGET", "SCE_PSM_DEVICE_VERSION_HOST", "SCE_PSM_DEVICE_VERSION_TARGET", "SCE_PSM_DEVICE_INVALID_PACKET", "SCE_PSM_DEVICE_TARGET_LAUNCHED", 
            "SCE_PSM_DEVICE_PSMDEVICE_ERROR", "SCE_PSM_DEVICE_PSMDEVICE_OPTION"
         };
            string str = "<Define not found>";
            if (code != 0)
            {
                code -= -2147418112;
                if ((code > 0) && (code < strArray.Length))
                {
                    str = strArray[code];
                }
                return str;
            }
            return strArray[0];
        }

        public static int GetErrStr(Guid deviceGuid, ref string errstr)
        {
            StringBuilder builder = new StringBuilder();
            int length = _scePsmDevGetErrStr(deviceGuid, builder);
            errstr = (length == 0) ? "" : builder.ToString().Substring(0, length);
            if (length < 0)
            {
                Defiler.ErrLine("Error. scePsmDevGetErrStr(0x{0:X8} : {1})", length, GetErrStr(length));
            }
            return length;
        }

        public static int GetLog(Guid deviceGuid, ref string logstr)
        {
            StringBuilder builder = new StringBuilder();
            int length = _scePsmDevGetLog(deviceGuid, builder);
            logstr = (length == 0) ? "" : builder.ToString().Substring(0, length);
            if (length < 0)
            {
                Defiler.ErrLine("Error. scePsmDevGetLog(0x{0:X8} : {1})", length, GetErrStr(length));
            }
            return length;
        }

        private static Mutex GetMutex(Guid guid)
        {
            if (mutexTable.ContainsKey(guid))
            {
                return mutexTable[guid];
            }
            Mutex mutex = new Mutex();
            mInfoMutex.WaitOne();
            mutexTable[guid] = mutex;
            mInfoMutex.ReleaseMutex();
            return mutex;
        }

        public static int GetPsmAppStatus(Guid deviceGuid)
        {
            return _scePsmDevGetPsmAppStatus(deviceGuid);
        }

        public static void Initialize()
        {
            if (IntPtr.Size == 4)
            {
                _scePsmDevSetAdbExePath = new scePsmDevSetAdbExePath(scePsmDevSetAdbExePath32);
                _scePsmDevListDevices = new scePsmDevListDevices(scePsmDevListDevices32);
                _scePsmDevConnect = new scePsmDevConnect(scePsmDevConnect32);
                _scePsmDevDisconnect = new scePsmDevDisconnect(scePsmDevDisconnect32);
                _scePsmDevCreatePackage = new scePsmDevCreatePackage(scePsmDevCreatePackage32);
                _scePsmDevExtractPackage = new scePsmDevExtractPackage(scePsmDevExtractPackage32);
                _scePsmDevPickFileFromPackage = new scePsmDevPickFileFromPackage(scePsmDevPickFileFromPackage32);
                _scePsmDevInstall = new scePsmDevInstall(scePsmDevInstall32);
                _scePsmDevUninstall = new scePsmDevUninstall(scePsmDevUninstall32);
                _scePsmDevLaunch = new scePsmDevLaunch(scePsmDevLaunch32);
                _scePsmDevKill = new scePsmDevKill(scePsmDevKill32);
                _scePsmDevSetConsoleWrite = new scePsmDevSetConsoleWrite(scePsmDevSetConsoleWrite32);
                _scePsmDevGetLog = new scePsmDevGetLog(scePsmDevGetLog32);
                _scePsmDevListApplications = new scePsmDevListApplications(scePsmDevListApplications32);
                _scePsmDevGetDeviceSeed = new scePsmDevGetDeviceSeed(scePsmDevGetDeviceSeed32);
                _scePsmDevSetAppExeKey = new scePsmDevSetAppExeKey(scePsmDevSetAppExeKey32);
                _scePsmDevExistAppExeKey = new scePsmDevExistAppExeKey(scePsmDevExistAppExeKey32);
                _scePsmDevGetPsmAppStatus = new scePsmDevGetPsmAppStatus(scePsmDevGetPsmAppStatus32);
                _scePsmDevRequestEndPsmApp = new scePsmDevRequestEndPsmApp(scePsmDevRequestEndPsmApp32);
                _scePsmDevResponseEndPsmApp = new scePsmDevResponseEndPsmApp(scePsmDevResponseEndPsmApp32);
                _scePsmDevVersion = new scePsmDevVersion(scePsmDevVersion32);
                _scePsmDevGetErrStr = new scePsmDevGetErrStr(scePsmDevGetErrStr32);
                _scePsmDevGetAgentVersion = new scePsmDevGetAgentVersion(scePsmDevGetAgentVersion32);
                _scePsmDevLaunchUnity = new scePsmDevLaunchUnity(scePsmDevLaunchUnity32);
            }
            else
            {
                _scePsmDevSetAdbExePath = new scePsmDevSetAdbExePath(scePsmDevSetAdbExePath64);
                _scePsmDevListDevices = new scePsmDevListDevices(scePsmDevListDevices64);
                _scePsmDevConnect = new scePsmDevConnect(scePsmDevConnect64);
                _scePsmDevDisconnect = new scePsmDevDisconnect(scePsmDevDisconnect64);
                _scePsmDevCreatePackage = new scePsmDevCreatePackage(scePsmDevCreatePackage64);
                _scePsmDevExtractPackage = new scePsmDevExtractPackage(scePsmDevExtractPackage64);
                _scePsmDevPickFileFromPackage = new scePsmDevPickFileFromPackage(scePsmDevPickFileFromPackage64);
                _scePsmDevInstall = new scePsmDevInstall(scePsmDevInstall64);
                _scePsmDevUninstall = new scePsmDevUninstall(scePsmDevUninstall64);
                _scePsmDevLaunch = new scePsmDevLaunch(scePsmDevLaunch64);
                _scePsmDevKill = new scePsmDevKill(scePsmDevKill64);
                _scePsmDevSetConsoleWrite = new scePsmDevSetConsoleWrite(scePsmDevSetConsoleWrite64);
                _scePsmDevGetLog = new scePsmDevGetLog(scePsmDevGetLog64);
                _scePsmDevListApplications = new scePsmDevListApplications(scePsmDevListApplications64);
                _scePsmDevGetDeviceSeed = new scePsmDevGetDeviceSeed(scePsmDevGetDeviceSeed64);
                _scePsmDevSetAppExeKey = new scePsmDevSetAppExeKey(scePsmDevSetAppExeKey64);
                _scePsmDevExistAppExeKey = new scePsmDevExistAppExeKey(scePsmDevExistAppExeKey64);
                _scePsmDevGetPsmAppStatus = new scePsmDevGetPsmAppStatus(scePsmDevGetPsmAppStatus64);
                _scePsmDevRequestEndPsmApp = new scePsmDevRequestEndPsmApp(scePsmDevRequestEndPsmApp64);
                _scePsmDevResponseEndPsmApp = new scePsmDevResponseEndPsmApp(scePsmDevResponseEndPsmApp64);
                _scePsmDevVersion = new scePsmDevVersion(scePsmDevVersion64);
                _scePsmDevGetErrStr = new scePsmDevGetErrStr(scePsmDevGetErrStr64);
                _scePsmDevGetAgentVersion = new scePsmDevGetAgentVersion(scePsmDevGetAgentVersion64);
                _scePsmDevLaunchUnity = new scePsmDevLaunchUnity(scePsmDevLaunchUnity64);
            }
        }

        public static int Install(Guid deviceGuid, string packageFile, string appId)
        {
            int num = _scePsmDevInstall(deviceGuid, packageFile, appId);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevInstall(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int Kill(Guid deviceGuid)
        {
            int num = _scePsmDevKill(deviceGuid);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevKill(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int Launch(Guid deviceGuid, string appId, bool debug, bool profile, bool keepnet, bool logwaiting, string arg)
        {
            int num = _scePsmDevLaunch(deviceGuid, appId, debug, profile, keepnet, logwaiting, arg);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevLaunch(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int ListApplications(Guid deviceGuid, ScePsmApplication[] list)
        {
            int num = _scePsmDevListApplications(deviceGuid, list);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevListApplications(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int ListDevices(out ScePsmDevice[] devices)
        {
            mDevices = new ScePsmDevice[8];
            int num = -1;
            try
            {
                mInfoMutex.WaitOne();
                num = _scePsmDevListDevices(mDevices);
            }
            finally
            {
                mInfoMutex.ReleaseMutex();
            }
            mDeviceNum = num;
            devices = mDevices;
            return num;
        }

        public static void LockConnection(Guid guid)
        {
            GetMutex(guid).WaitOne();
        }

        public static int PickFileFromPackage(string outName, string packageFile, string inName)
        {
            int num = _scePsmDevPickFileFromPackage(outName, packageFile, inName);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevPickFileFromPackage(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int RequestEndPsmApp(Guid deviceGuid, string msg)
        {
            return _scePsmDevRequestEndPsmApp(deviceGuid, msg);
        }

        public static int ResponseEndPsmApp(Guid deviceGuid, int response, string option)
        {
            return _scePsmDevResponseEndPsmApp(deviceGuid, response, option);
        }

        [DllImport(dll32, EntryPoint = "scePsmDevConnect")]
        public static extern int scePsmDevConnect32([MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll64, EntryPoint = "scePsmDevConnect")]
        public static extern int scePsmDevConnect64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll32, EntryPoint = "scePsmDevCreatePackage")]
        public static extern int scePsmDevCreatePackage32([MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string dirForPack);
        [DllImport(dll64, EntryPoint = "scePsmDevCreatePackage")]
        public static extern int scePsmDevCreatePackage64([MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string dirForPack);
        [DllImport(dll32, EntryPoint = "scePsmDevDisconnect")]
        public static extern int scePsmDevDisconnect32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll64, EntryPoint = "scePsmDevDisconnect")]
        public static extern int scePsmDevDisconnect64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll32, EntryPoint = "scePsmDevExistAppExeKey")]
        public static extern int scePsmDevExistAppExeKey32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, long accountId, [MarshalAs(UnmanagedType.LPStr)] string titleIdentifier, [MarshalAs(UnmanagedType.LPStr)] string env);
        [DllImport(dll64, EntryPoint = "scePsmDevExistAppExeKey")]
        public static extern int scePsmDevExistAppExeKey64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, long accountId, [MarshalAs(UnmanagedType.LPStr)] string titleIdentifier, [MarshalAs(UnmanagedType.LPStr)] string env);
        [DllImport(dll32, EntryPoint = "scePsmDevExtractPackage")]
        public static extern int scePsmDevExtractPackage32([MarshalAs(UnmanagedType.LPStr)] string dirExtract, [MarshalAs(UnmanagedType.LPStr)] string packageFile);
        [DllImport(dll64, EntryPoint = "scePsmDevExtractPackage")]
        public static extern int scePsmDevExtractPackage64([MarshalAs(UnmanagedType.LPStr)] string dirExtract, [MarshalAs(UnmanagedType.LPStr)] string packageFile);
        [DllImport(dll32, EntryPoint = "scePsmDevGetDeviceSeed")]
        public static extern int scePsmDevGetDeviceSeed32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string filename);
        [DllImport(dll64, EntryPoint = "scePsmDevGetDeviceSeed")]
        public static extern int scePsmDevGetDeviceSeed64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string filename);
        [DllImport(dll32, EntryPoint = "scePsmDevGetErrStr")]
        public static extern int scePsmDevGetErrStr32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder errstr);
        [DllImport(dll64, EntryPoint = "scePsmDevGetErrStr")]
        public static extern int scePsmDevGetErrStr64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder errstr);
        [DllImport(dll32, EntryPoint = "scePsmDevGetLog")]
        public static extern int scePsmDevGetLog32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder logstr);
        [DllImport(dll64, EntryPoint = "scePsmDevGetLog")]
        public static extern int scePsmDevGetLog64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder logstr);
        [DllImport(dll32, EntryPoint = "scePsmDevGetPsmAppStatus")]
        public static extern int scePsmDevGetPsmAppStatus32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll64, EntryPoint = "scePsmDevGetPsmAppStatus")]
        public static extern int scePsmDevGetPsmAppStatus64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll32, EntryPoint = "scePsmDevInstall")]
        public static extern int scePsmDevInstall32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string appId);
        [DllImport(dll64, EntryPoint = "scePsmDevInstall")]
        public static extern int scePsmDevInstall64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string appId);
        [DllImport(dll32, EntryPoint = "scePsmDevKill")]
        public static extern int scePsmDevKill32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll64, EntryPoint = "scePsmDevKill")]
        public static extern int scePsmDevKill64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll32, EntryPoint = "scePsmDevLaunch")]
        public static extern int scePsmDevLaunch32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appId, bool debug, bool profile, bool keepnet, bool logwaiting, [MarshalAs(UnmanagedType.LPStr)] string arg);
        [DllImport(dll64, EntryPoint = "scePsmDevLaunch")]
        public static extern int scePsmDevLaunch64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appId, bool debug, bool profile, bool keepnet, bool logwaiting, [MarshalAs(UnmanagedType.LPStr)] string arg);
        [DllImport(dll32, EntryPoint = "scePsmDevListApplications")]
        public static extern int scePsmDevListApplications32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, SizeConst = APPLICATION_NUM)] ScePsmApplication[] appArray);
        [DllImport(dll64, EntryPoint = "scePsmDevListApplications")]
        public static extern int scePsmDevListApplications64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, SizeConst = APPLICATION_NUM)] ScePsmApplication[] appArray);
        [DllImport(dll32, EntryPoint = "scePsmDevListDevices")]
        public static extern int scePsmDevListDevices32([In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, SizeConst = DEVICE_NUM)] ScePsmDevice[] deviceArray);
        [DllImport(dll64, EntryPoint = "scePsmDevListDevices")]
        public static extern int scePsmDevListDevices64([In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0, SizeConst = DEVICE_NUM)] ScePsmDevice[] deviceArray);
        [DllImport(dll32, EntryPoint = "scePsmDevPickFileFromPackage")]
        public static extern int scePsmDevPickFileFromPackage32([MarshalAs(UnmanagedType.LPStr)] string outName, [MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string inName);
        [DllImport(dll64, EntryPoint = "scePsmDevPickFileFromPackage")]
        public static extern int scePsmDevPickFileFromPackage64([MarshalAs(UnmanagedType.LPStr)] string outName, [MarshalAs(UnmanagedType.LPStr)] string packageFile, [MarshalAs(UnmanagedType.LPStr)] string inName);
        [DllImport(dll32, EntryPoint = "scePsmDevRequestEndPsmApp")]
        public static extern int scePsmDevRequestEndPsmApp32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, string msg);
        [DllImport(dll32, EntryPoint = "scePsmDevRequestEndPsmApp")]
        public static extern int scePsmDevRequestEndPsmApp64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, string msg);
        [DllImport(dll32, EntryPoint = "scePsmDevResponseEndPsmApp")]
        public static extern int scePsmDevResponseEndPsmApp32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, int response, string option);
        [DllImport(dll32, EntryPoint = "scePsmDevResponseEndPsmApp")]
        public static extern int scePsmDevResponseEndPsmApp64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, int response, string option);
        [DllImport(dll32, EntryPoint = "scePsmDevSetAdbExePath", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int scePsmDevSetAdbExePath32(string path);
        [DllImport(dll64, EntryPoint = "scePsmDevSetAdbExePath", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int scePsmDevSetAdbExePath64(string path);
        [DllImport(dll32, EntryPoint = "scePsmDevSetAppExeKey")]
        public static extern int scePsmDevSetAppExeKey32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string filename);
        [DllImport(dll64, EntryPoint = "scePsmDevSetAppExeKey")]
        public static extern int scePsmDevSetAppExeKey64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string filename);
        [DllImport(dll32, EntryPoint = "scePsmDevSetConsoleWrite")]
        public static extern int scePsmDevSetConsoleWrite32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, IntPtr proc);
        [DllImport(dll64, EntryPoint = "scePsmDevSetConsoleWrite")]
        public static extern int scePsmDevSetConsoleWrite64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, IntPtr proc);
        [DllImport(dll32, EntryPoint = "scePsmDevUninstall")]
        public static extern int scePsmDevUninstall32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appId);
        [DllImport(dll64, EntryPoint = "scePsmDevUninstall")]
        public static extern int scePsmDevUninstall64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appId);
        [DllImport(dll32, EntryPoint = "scePsmDevVersion")]
        public static extern int scePsmDevVersion32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll64, EntryPoint = "scePsmDevVersion")]
        public static extern int scePsmDevVersion64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid);
        [DllImport(dll32, EntryPoint = "scePsmDevGetAgentVersion")]
        public static extern int scePsmDevGetAgentVersion32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] psm_devagent, [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] host_transport);
        [DllImport(dll64, EntryPoint = "scePsmDevGetAgentVersion")]
        public static extern int scePsmDevGetAgentVersion64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] psm_devagent, [In, Out, MarshalAs(UnmanagedType.LPArray)] byte[] host_transport);
        [DllImport(dll32, EntryPoint = "scePsmDevLaunchUnity")]
        public static extern int scePsmDevLaunchUnity32([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appName, int argnum, [MarshalAs(UnmanagedType.LPArray)] string[] argstr);
        [DllImport(dll64, EntryPoint = "scePsmDevLaunchUnity")]
        public static extern int scePsmDevLaunchUnity64([In, MarshalAs(UnmanagedType.LPStruct)] Guid deviceGuid, [MarshalAs(UnmanagedType.LPStr)] string appName, int argnum, [MarshalAs(UnmanagedType.LPArray)] string[] argstr);

        public static int SetAdbExePath(string path)
        {
            return _scePsmDevSetAdbExePath(path);
        }

        public static int SetAppExeKey(Guid deviceGuid, string filename)
        {
            int num = _scePsmDevSetAppExeKey(deviceGuid, filename);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevSetAppExeKey(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int SetConsoleWrite(Guid deviceGuid, IntPtr proc)
        {
            int num = _scePsmDevSetConsoleWrite(deviceGuid, proc);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevSetConsoleWrite(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int Uninstall(Guid deviceGuid, string appId)
        {
            int num = _scePsmDevUninstall(deviceGuid, appId);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevUninstall(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static void UnlockConnection(Guid guid)
        {
            GetMutex(guid).ReleaseMutex();
        }

        public static int Version(Guid deviceGuid)
        {
            int num = _scePsmDevVersion(deviceGuid);
            if (num < 0)
            {
                Defiler.ErrLine("Error. scePsmDevVersion(0x{0:X8} : {1})", num, GetErrStr(num));
            }
            return num;
        }

        public static int GetAgentVersion(Guid deviceGuid, ref string psm_devagent_verstr, ref string host_transport_verstr)
        {
            byte[] buffer = new byte[0x20];
            byte[] buffer2 = new byte[0x20];
            int code = _scePsmDevGetAgentVersion(deviceGuid, buffer, buffer2);
            if (code < 0)
            {
                Defiler.ErrLine("Error. scePsmDevGetAgentVersion: {0}", GetErrStr(code));
                return code;
            }
            psm_devagent_verstr = Encoding.ASCII.GetString(buffer).TrimEnd(new char[1]);
            host_transport_verstr = Encoding.ASCII.GetString(buffer2).TrimEnd(new char[1]);
            return code;
        }

        public static int LaunchUnity(Guid deviceGuid, string appName, int argnum, string[] argstr)
        {
            int code = _scePsmDevLaunchUnity(deviceGuid, appName, argnum, argstr);
            if (code < 0)
            {
                Defiler.ErrLine("Error. scePsmDevLaunchUnity: {0}", GetErrStr(code));
            }
            return code;
        }

        // Nested Types
        public enum TargetType
        {
            Simulater,
            Android,
            Vita
        }
    }

    public class DataConverter
    {
        public static unsafe byte[] GetBytesBE(double value)
        {
            return GetBytesSwap(BitConverter.IsLittleEndian, (byte*)&value, 8);
        }

        private static unsafe byte[] GetBytesSwap(bool swap, byte* ptr, int count)
        {
            int num2;
            byte[] buffer = new byte[count];
            if (swap)
            {
                int num = count - 1;
                for (num2 = 0; num2 < count; num2++)
                {
                    buffer[num - num2] = ptr[num2];
                }
                return buffer;
            }
            for (num2 = 0; num2 < count; num2++)
            {
                buffer[num2] = ptr[num2];
            }
            return buffer;
        }

        public static unsafe float FloatFromBE(byte[] data, int index)
        {
            float num;
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            if ((data.Length - index) < 4)
            {
                throw new ArgumentException("index");
            }
            if (index < 0)
            {
                throw new ArgumentException("index");
            }
            fixed (byte* numRef = &(data[index]))
            {
                PutBytesBE((byte*)&num, numRef, 4);
            }
            return num;
        }

        public static unsafe double DoubleFromBE(byte[] data, int index)
        {
            double num;
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            if ((data.Length - index) < 8)
            {
                throw new ArgumentException("index");
            }
            if (index < 0)
            {
                throw new ArgumentException("index");
            }
            fixed (byte* numRef = &(data[index]))
            {
                PutBytesBE((byte*)&num, numRef, 8);
            }
            return num;
        }

        private static unsafe void PutBytesBE(byte* dest, byte* src, int count)
        {
            int num = 0;
            if (!BitConverter.IsLittleEndian)
            {
                while (num < count)
                {
                    dest++;
                    src++;
                    dest[0] = src[0];
                    num++;
                }
            }
            else
            {
                dest += count;
                while (num < count)
                {
                    src++;
                    *
                    (--dest) = src[0];
                    num++;
                }
            }
        }
    }

    public class VersionInfo
    {
        public string VMVersion
        {
            get;
            set;
        }

        public int MajorVersion
        {
            get;
            set;
        }

        public int MinorVersion
        {
            get;
            set;
        }

        /*
         * Check that this version is at least major:minor
         */
        public bool AtLeast(int major, int minor)
        {
            if ((MajorVersion > major) || ((MajorVersion == major && MinorVersion >= minor)))
                return true;
            else
                return false;
        }
    }

    class DebugInfo
    {
        public int max_il_offset;
        public string filename;
        public int[] il_offsets;
        public int[] line_numbers;
    }

    struct FrameInfo
    {
        public long id;
        public long method;
        public int il_offset;
        public StackFrameFlags flags;
    }

    class TypeInfo
    {
        public string ns, name, full_name;
        public long assembly, module, base_type, element_type;
        public int token, rank, attributes;
        public bool is_byref, is_pointer, is_primitive, is_valuetype, is_enum;
        public long[] nested;
    }

    class MethodInfo
    {
        public int attributes, iattributes, token;
    }

    class MethodBodyInfo
    {
        public byte[] il;
    }

    struct ParamInfo
    {
        public int call_conv;
        public int param_count;
        public int generic_param_count;
        public long ret_type;
        public long[] param_types;
        public string[] param_names;
    }

    struct LocalsInfo
    {
        public long[] types;
        public string[] names;
        public int[] live_range_start;
        public int[] live_range_end;
    }

    struct PropInfo
    {
        public long id;
        public string name;
        public long get_method, set_method;
        public int attrs;
    }

    class CattrNamedArgInfo
    {
        public bool is_property;
        public long id;
        public ValueImpl value;
    }

    class CattrInfo
    {
        public long ctor_id;
        public ValueImpl[] ctor_args;
        public CattrNamedArgInfo[] named_args;
    }

    class ThreadInfo
    {
        public bool is_thread_pool;
    }

    struct ObjectRefInfo
    {
        public long type_id;
        public long domain_id;
    }

    enum ValueTypeId
    {
        VALUE_TYPE_ID_NULL = 0xf0,
        VALUE_TYPE_ID_TYPE = 0xf1
    }

    enum InvokeFlags
    {
        NONE = 0x0,
        DISABLE_BREAKPOINTS = 0x1,
        SINGLE_THREADED = 0x2
    }

    enum ElementType
    {
        End = 0x00,
        Void = 0x01,
        Boolean = 0x02,
        Char = 0x03,
        I1 = 0x04,
        U1 = 0x05,
        I2 = 0x06,
        U2 = 0x07,
        I4 = 0x08,
        U4 = 0x09,
        I8 = 0x0a,
        U8 = 0x0b,
        R4 = 0x0c,
        R8 = 0x0d,
        String = 0x0e,
        Ptr = 0x0f,
        ByRef = 0x10,
        ValueType = 0x11,
        Class = 0x12,
        Var = 0x13,
        Array = 0x14,
        GenericInst = 0x15,
        TypedByRef = 0x16,
        I = 0x18,
        U = 0x19,
        FnPtr = 0x1b,
        Object = 0x1c,
        SzArray = 0x1d,
        MVar = 0x1e,
        CModReqD = 0x1f,
        CModOpt = 0x20,
        Internal = 0x21,
        Modifier = 0x40,
        Sentinel = 0x41,
        Pinned = 0x45,

        Type = 0x50,
        Boxed = 0x51,
        Enum = 0x55
    }

    class ValueImpl
    {
        public ElementType Type; /* or one of the VALUE_TYPE_ID constants */
        public long Objid;
        public object Value;
        public long Klass; // For ElementType.ValueType
        public ValueImpl[] Fields; // for ElementType.ValueType
        public bool IsEnum; // For ElementType.ValueType
        public long Id; /* For VALUE_TYPE_ID_TYPE */
    }

    class ModuleInfo
    {
        public string Name, ScopeName, FQName, Guid;
        public long Assembly;
    }

    enum TokenType
    {
        STRING = 0,
        TYPE = 1,
        FIELD = 2,
        METHOD = 3,
        UNKNOWN = 4
    }

    enum StackFrameFlags
    {
        DEBUGGER_INVOKE = 1
    }

    class ResolvedToken
    {
        public TokenType Type;
        public string Str;
        public long Id;
    }

    class Modifier
    {
    }

    class CountModifier : Modifier
    {
        public int Count
        {
            get;
            set;
        }
    }

    class LocationModifier : Modifier
    {
        public long Method
        {
            get;
            set;
        }

        public long Location
        {
            get;
            set;
        }
    }

    class StepModifier : Modifier
    {
        public long Thread
        {
            get;
            set;
        }

        public int Depth
        {
            get;
            set;
        }

        public int Size
        {
            get;
            set;
        }
    }

    class ThreadModifier : Modifier
    {
        public long Thread
        {
            get;
            set;
        }
    }

    class ExceptionModifier : Modifier
    {
        public long Type
        {
            get;
            set;
        }

        public bool Caught
        {
            get;
            set;
        }

        public bool Uncaught
        {
            get;
            set;
        }
    }

    class AssemblyModifier : Modifier
    {
        public long[] Assemblies
        {
            get;
            set;
        }
    }

    class EventInfo
    {
        public EventType EventType
        {
            get;
            set;
        }

        public int ReqId
        {
            get;
            set;
        }

        public SuspendPolicy SuspendPolicy
        {
            get;
            set;
        }

        public long ThreadId
        {
            get;
            set;
        }

        public long Id
        {
            get;
            set;
        }

        public long Location
        {
            get;
            set;
        }

        public int Level
        {
            get;
            set;
        }

        public string Category
        {
            get;
            set;
        }

        public string Message
        {
            get;
            set;
        }

        public EventInfo(EventType type, int req_id)
        {
            EventType = type;
            ReqId = req_id;
        }
    }

    public enum ErrorCode
    {
        NONE = 0,
        INVALID_OBJECT = 20,
        INVALID_FIELDID = 25,
        INVALID_FRAMEID = 30,
        NOT_IMPLEMENTED = 100,
        NOT_SUSPENDED = 101,
        INVALID_ARGUMENT = 102,
        ERR_UNLOADED = 103,
        ERR_NO_INVOCATION = 104,
        ABSENT_INFORMATION = 105,
        NO_SEQ_POINT_AT_IL_OFFSET = 106
    }

    public class ErrorHandlerEventArgs : EventArgs
    {

        public ErrorCode ErrorCode
        {
            get;
            set;
        }
    }

    /*
     * Represents the connection to the debuggee
     */
    public abstract class Connection
    {
        /*
         * The protocol and the packet format is based on JDWP, the differences 
         * are in the set of supported events, and the commands.
         */
        internal const string HANDSHAKE_STRING = "DWP-Handshake";
        internal const int HEADER_LENGTH = 11;

        /*
         * Th version of the wire-protocol implemented by the library. The library
         * and the debuggee can communicate if they implement the same major version.
         * If they implement a different minor version, they can communicate, but some
         * features might not be available. This allows older clients to communicate
         * with newer runtimes, and vice versa.
         */
        internal const int MAJOR_VERSION = 2;
        internal const int MINOR_VERSION = 5;

        enum WPSuspendPolicy
        {
            NONE = 0,
            EVENT_THREAD = 1,
            ALL = 2
        }

        enum CommandSet
        {
            VM = 1,
            OBJECT_REF = 9,
            STRING_REF = 10,
            THREAD = 11,
            ARRAY_REF = 13,
            EVENT_REQUEST = 15,
            STACK_FRAME = 16,
            APPDOMAIN = 20,
            ASSEMBLY = 21,
            METHOD = 22,
            TYPE = 23,
            MODULE = 24,
            EVENT = 64
        }

        enum EventKind
        {
            VM_START = 0,
            VM_DEATH = 1,
            THREAD_START = 2,
            THREAD_DEATH = 3,
            APPDOMAIN_CREATE = 4, // Not in JDI
            APPDOMAIN_UNLOAD = 5, // Not in JDI
            METHOD_ENTRY = 6,
            METHOD_EXIT = 7,
            ASSEMBLY_LOAD = 8,
            ASSEMBLY_UNLOAD = 9,
            BREAKPOINT = 10,
            STEP = 11,
            TYPE_LOAD = 12,
            EXCEPTION = 13,
            KEEPALIVE = 14,
            USER_BREAK = 15,
            USER_LOG = 16
        }

        enum ModifierKind
        {
            COUNT = 1,
            THREAD_ONLY = 3,
            LOCATION_ONLY = 7,
            EXCEPTION_ONLY = 8,
            STEP = 10,
            ASSEMBLY_ONLY = 11
        }

        enum CmdVM
        {
            VERSION = 1,
            ALL_THREADS = 2,
            SUSPEND = 3,
            RESUME = 4,
            EXIT = 5,
            DISPOSE = 6,
            INVOKE_METHOD = 7,
            SET_PROTOCOL_VERSION = 8,
            ABORT_INVOKE = 9,
            SET_KEEPALIVE = 10
        }

        enum CmdEvent
        {
            COMPOSITE = 100
        }

        enum CmdThread
        {
            GET_FRAME_INFO = 1,
            GET_NAME = 2,
            GET_STATE = 3,
            GET_INFO = 4,
            /* FIXME: Merge into GET_INFO when the major protocol version is increased */
            GET_ID = 5,
            /* Ditto */
            GET_TID = 6
        }

        enum CmdEventRequest
        {
            SET = 1,
            CLEAR = 2,
            CLEAR_ALL_BREAKPOINTS = 3
        }

        enum CmdAppDomain
        {
            GET_ROOT_DOMAIN = 1,
            GET_FRIENDLY_NAME = 2,
            GET_ASSEMBLIES = 3,
            GET_ENTRY_ASSEMBLY = 4,
            CREATE_STRING = 5,
            GET_CORLIB = 6,
            CREATE_BOXED_VALUE = 7
        }

        enum CmdAssembly
        {
            GET_LOCATION = 1,
            GET_ENTRY_POINT = 2,
            GET_MANIFEST_MODULE = 3,
            GET_OBJECT = 4,
            GET_TYPE = 5,
            GET_NAME = 6
        }

        enum CmdModule
        {
            GET_INFO = 1,
        }

        enum CmdMethod
        {
            GET_NAME = 1,
            GET_DECLARING_TYPE = 2,
            GET_DEBUG_INFO = 3,
            GET_PARAM_INFO = 4,
            GET_LOCALS_INFO = 5,
            GET_INFO = 6,
            GET_BODY = 7,
            RESOLVE_TOKEN = 8
        }

        enum CmdType
        {
            GET_INFO = 1,
            GET_METHODS = 2,
            GET_FIELDS = 3,
            GET_VALUES = 4,
            GET_OBJECT = 5,
            GET_SOURCE_FILES = 6,
            SET_VALUES = 7,
            IS_ASSIGNABLE_FROM = 8,
            GET_PROPERTIES = 9,
            GET_CATTRS = 10,
            GET_FIELD_CATTRS = 11,
            GET_PROPERTY_CATTRS = 12,
            /* FIXME: Merge into GET_SOURCE_FILES when the major protocol version is increased */
            GET_SOURCE_FILES_2 = 13,
            /* FIXME: Merge into GET_VALUES when the major protocol version is increased */
            GET_VALUES_2 = 14
        }

        enum CmdStackFrame
        {
            GET_VALUES = 1,
            GET_THIS = 2,
            SET_VALUES = 3
        }

        enum CmdArrayRef
        {
            GET_LENGTH = 1,
            GET_VALUES = 2,
            SET_VALUES = 3
        }

        enum CmdStringRef
        {
            GET_VALUE = 1
        }

        enum CmdObjectRef
        {
            GET_TYPE = 1,
            GET_VALUES = 2,
            IS_COLLECTED = 3,
            GET_ADDRESS = 4,
            GET_DOMAIN = 5,
            SET_VALUES = 6,
            GET_INFO = 7,
        }

        class Header
        {
            public int id;
            public int command_set;
            public int command;
            public int flags;
        }

        internal static int GetPacketLength(byte[] header)
        {
            int offset = 0;
            return decode_int(header, ref offset);
        }

        internal static bool IsReplyPacket(byte[] packet)
        {
            int offset = 8;
            return decode_byte(packet, ref offset) == 0x80;
        }

        internal static int GetPacketId(byte[] packet)
        {
            int offset = 4;
            return decode_int(packet, ref offset);
        }

        static int decode_byte(byte[] packet, ref int offset)
        {
            return packet[offset++];
        }

        static int decode_short(byte[] packet, ref int offset)
        {
            int res = ((int)packet[offset] << 8) | (int)packet[offset + 1];
            offset += 2;
            return res;
        }

        static int decode_int(byte[] packet, ref int offset)
        {
            int res = ((int)packet[offset] << 24) | ((int)packet[offset + 1] << 16) | ((int)packet[offset + 2] << 8) | (int)packet[offset + 3];
            offset += 4;
            return res;
        }

        static long decode_id(byte[] packet, ref int offset)
        {
            return decode_int(packet, ref offset);
        }

        static long decode_long(byte[] packet, ref int offset)
        {
            uint high = (uint)decode_int(packet, ref offset);
            uint low = (uint)decode_int(packet, ref offset);

            return (long)(((ulong)high << 32) | (ulong)low);
        }

        internal static SuspendPolicy decode_suspend_policy(int suspend_policy)
        {
            switch ((WPSuspendPolicy)suspend_policy)
            {
                case WPSuspendPolicy.NONE:
                    return SuspendPolicy.None;
                case WPSuspendPolicy.EVENT_THREAD:
                    return SuspendPolicy.EventThread;
                case WPSuspendPolicy.ALL:
                    return SuspendPolicy.All;
                default:
                    throw new NotImplementedException();
            }
        }

        static Header decode_command_header(byte[] packet)
        {
            int offset = 0;
            Header res = new Header();

            decode_int(packet, ref offset);
            res.id = decode_int(packet, ref offset);
            res.flags = decode_byte(packet, ref offset);
            res.command_set = decode_byte(packet, ref offset);
            res.command = decode_byte(packet, ref offset);

            return res;
        }

        static void encode_byte(byte[] buf, int b, ref int offset)
        {
            buf[offset] = (byte)b;
            offset++;
        }

        static void encode_int(byte[] buf, int i, ref int offset)
        {
            buf[offset] = (byte)((i >> 24) & 0xff);
            buf[offset + 1] = (byte)((i >> 16) & 0xff);
            buf[offset + 2] = (byte)((i >> 8) & 0xff);
            buf[offset + 3] = (byte)((i >> 0) & 0xff);
            offset += 4;
        }

        static void encode_id(byte[] buf, long id, ref int offset)
        {
            encode_int(buf, (int)id, ref offset);
        }

        static void encode_long(byte[] buf, long l, ref int offset)
        {
            encode_int(buf, (int)((l >> 32) & 0xffffffff), ref offset);
            encode_int(buf, (int)(l & 0xffffffff), ref offset);
        }

        internal static byte[] EncodePacket(int id, int commandSet, int command, byte[] data, int dataLen)
        {
            byte[] buf = new byte[dataLen + 11];
            int offset = 0;

            encode_int(buf, buf.Length, ref offset);
            encode_int(buf, id, ref offset);
            encode_byte(buf, 0, ref offset);
            encode_byte(buf, commandSet, ref offset);
            encode_byte(buf, command, ref offset);

            for (int i = 0; i < dataLen; ++i)
                buf[offset + i] = data[i];

            return buf;
        }

        class PacketReader
        {
            byte[] packet;
            int offset;

            public PacketReader(byte[] packet)
            {
                this.packet = packet;

                // For event packets
                Header header = decode_command_header(packet);
                CommandSet = (CommandSet)header.command_set;
                Command = header.command;

                // For reply packets
                offset = 0;
                ReadInt(); // length
                ReadInt(); // id
                ReadByte(); // flags
                ErrorCode = ReadShort();
            }

            public CommandSet CommandSet
            {
                get;
                set;
            }

            public int Command
            {
                get;
                set;
            }

            public int ErrorCode
            {
                get;
                set;
            }

            public int Offset
            {
                get
                {
                    return offset;
                }
            }

            public int ReadByte()
            {
                return decode_byte(packet, ref offset);
            }

            public int ReadShort()
            {
                return decode_short(packet, ref offset);
            }

            public int ReadInt()
            {
                return decode_int(packet, ref offset);
            }

            public long ReadId()
            {
                return decode_id(packet, ref offset);
            }

            public long ReadLong()
            {
                return decode_long(packet, ref offset);
            }

            public float ReadFloat()
            {
                float f = DataConverter.FloatFromBE(packet, offset);
                offset += 4;
                return f;
            }

            public double ReadDouble()
            {
                double d = DataConverter.DoubleFromBE(packet, offset);
                offset += 8;
                return d;
            }

            public string ReadString()
            {
                int len = decode_int(packet, ref offset);
                string res = new String(Encoding.UTF8.GetChars(packet, offset, len));
                offset += len;
                return res;
            }

            public ValueImpl ReadValue()
            {
                ElementType etype = (ElementType)ReadByte();

                switch (etype)
                {
                    case ElementType.Void:
                        return new ValueImpl { Type = etype };
                    case ElementType.I1:
                        return new ValueImpl { Type = etype, Value = (sbyte)ReadInt() };
                    case ElementType.U1:
                        return new ValueImpl { Type = etype, Value = (byte)ReadInt() };
                    case ElementType.Boolean:
                        return new ValueImpl { Type = etype, Value = ReadInt() != 0 };
                    case ElementType.I2:
                        return new ValueImpl { Type = etype, Value = (short)ReadInt() };
                    case ElementType.U2:
                        return new ValueImpl { Type = etype, Value = (ushort)ReadInt() };
                    case ElementType.Char:
                        return new ValueImpl { Type = etype, Value = (char)ReadInt() };
                    case ElementType.I4:
                        return new ValueImpl { Type = etype, Value = ReadInt() };
                    case ElementType.U4:
                        return new ValueImpl { Type = etype, Value = (uint)ReadInt() };
                    case ElementType.I8:
                        return new ValueImpl { Type = etype, Value = ReadLong() };
                    case ElementType.U8:
                        return new ValueImpl { Type = etype, Value = (ulong)ReadLong() };
                    case ElementType.R4:
                        return new ValueImpl { Type = etype, Value = ReadFloat() };
                    case ElementType.R8:
                        return new ValueImpl { Type = etype, Value = ReadDouble() };
                    case ElementType.I:
                    case ElementType.U:
                    case ElementType.Ptr:
                        // FIXME: The client and the debuggee might have different word sizes
                        return new ValueImpl { Type = etype, Value = ReadLong() };
                    case ElementType.String:
                    case ElementType.SzArray:
                    case ElementType.Class:
                    case ElementType.Array:
                    case ElementType.Object:
                        long objid = ReadId();
                        return new ValueImpl() { Type = etype, Objid = objid };
                    case ElementType.ValueType:
                        bool is_enum = ReadByte() == 1;
                        long klass = ReadId();
                        long nfields = ReadInt();
                        ValueImpl[] fields = new ValueImpl[nfields];
                        for (int i = 0; i < nfields; ++i)
                            fields[i] = ReadValue();
                        return new ValueImpl() { Type = etype, Klass = klass, Fields = fields, IsEnum = is_enum };
                    case (ElementType)ValueTypeId.VALUE_TYPE_ID_NULL:
                        return new ValueImpl { Type = etype };
                    case (ElementType)ValueTypeId.VALUE_TYPE_ID_TYPE:
                        return new ValueImpl() { Type = etype, Id = ReadId() };
                    default:
                        throw new NotImplementedException("Unable to handle type " + etype);
                }
            }
        }

        class PacketWriter
        {

            byte[] data;
            int offset;

            public PacketWriter()
            {
                data = new byte[1024];
                offset = 0;
            }

            void MakeRoom(int size)
            {
                if (offset + size >= data.Length)
                {
                    int new_len = data.Length * 2;
                    while (new_len < offset + size)
                    {
                        new_len *= 2;
                    }
                    byte[] new_data = new byte[new_len];
                    Array.Copy(data, new_data, data.Length);
                    data = new_data;
                }
            }

            public PacketWriter WriteByte(byte val)
            {
                MakeRoom(1);
                encode_byte(data, val, ref offset);
                return this;
            }

            public PacketWriter WriteInt(int val)
            {
                MakeRoom(4);
                encode_int(data, val, ref offset);
                return this;
            }

            public PacketWriter WriteId(long id)
            {
                MakeRoom(8);
                encode_id(data, id, ref offset);
                return this;
            }

            public PacketWriter WriteLong(long val)
            {
                MakeRoom(8);
                encode_long(data, val, ref offset);
                return this;
            }

            public PacketWriter WriteFloat(float f)
            {
                MakeRoom(8);
                byte[] b = DataConverter.GetBytesBE(f);
                for (int i = 0; i < 4; ++i)
                    data[offset + i] = b[i];
                offset += 4;
                return this;
            }

            public PacketWriter WriteDouble(double d)
            {
                MakeRoom(8);
                byte[] b = DataConverter.GetBytesBE(d);
                for (int i = 0; i < 8; ++i)
                    data[offset + i] = b[i];
                offset += 8;
                return this;
            }

            public PacketWriter WriteInts(int[] ids)
            {
                for (int i = 0; i < ids.Length; ++i)
                    WriteInt(ids[i]);
                return this;
            }

            public PacketWriter WriteIds(long[] ids)
            {
                for (int i = 0; i < ids.Length; ++i)
                    WriteId(ids[i]);
                return this;
            }

            public PacketWriter WriteString(string s)
            {
                encode_int(data, s.Length, ref offset);
                byte[] b = Encoding.UTF8.GetBytes(s);
                MakeRoom(b.Length);
                Buffer.BlockCopy(b, 0, data, offset, b.Length);
                offset += b.Length;
                return this;
            }

            public PacketWriter WriteBool(bool val)
            {
                WriteByte(val ? (byte)1 : (byte)0);
                return this;
            }

            public PacketWriter WriteValue(ValueImpl v)
            {
                ElementType t;

                if (v.Value != null)
                    t = TypeCodeToElementType(Type.GetTypeCode(v.Value.GetType()));
                else
                    t = v.Type;
                WriteByte((byte)t);
                switch (t)
                {
                    case ElementType.Boolean:
                        WriteInt((bool)v.Value ? 1 : 0);
                        break;
                    case ElementType.Char:
                        WriteInt((int)(char)v.Value);
                        break;
                    case ElementType.I1:
                        WriteInt((int)(sbyte)v.Value);
                        break;
                    case ElementType.U1:
                        WriteInt((int)(byte)v.Value);
                        break;
                    case ElementType.I2:
                        WriteInt((int)(short)v.Value);
                        break;
                    case ElementType.U2:
                        WriteInt((int)(ushort)v.Value);
                        break;
                    case ElementType.I4:
                        WriteInt((int)(int)v.Value);
                        break;
                    case ElementType.U4:
                        WriteInt((int)(uint)v.Value);
                        break;
                    case ElementType.I8:
                        WriteLong((long)(long)v.Value);
                        break;
                    case ElementType.U8:
                        WriteLong((long)(ulong)v.Value);
                        break;
                    case ElementType.R4:
                        WriteFloat((float)v.Value);
                        break;
                    case ElementType.R8:
                        WriteDouble((double)v.Value);
                        break;
                    case ElementType.String:
                    case ElementType.SzArray:
                    case ElementType.Class:
                    case ElementType.Array:
                    case ElementType.Object:
                        WriteId(v.Objid);
                        break;
                    case ElementType.ValueType:
                        // FIXME: 
                        if (v.IsEnum)
                            throw new NotImplementedException();
                        WriteByte(0);
                        WriteId(v.Klass);
                        WriteInt(v.Fields.Length);
                        for (int i = 0; i < v.Fields.Length; ++i)
                            WriteValue(v.Fields[i]);
                        break;
                    case (ElementType)ValueTypeId.VALUE_TYPE_ID_NULL:
                        break;
                    default:
                        throw new NotImplementedException();
                }

                return this;
            }

            public PacketWriter WriteValues(ValueImpl[] values)
            {
                for (int i = 0; i < values.Length; ++i)
                    WriteValue(values[i]);
                return this;
            }

            public byte[] Data
            {
                get
                {
                    return data;
                }
            }

            public int Offset
            {
                get
                {
                    return offset;
                }
            }
        }

        delegate void ReplyCallback(int packet_id, byte[] packet);

        bool closed;
        Thread receiver_thread;
        Dictionary<int, byte[]> reply_packets;
        Dictionary<int, ReplyCallback> reply_cbs;
        object reply_packets_monitor;

        internal event EventHandler<ErrorHandlerEventArgs> ErrorHandler;

        protected Connection()
        {
            closed = false;
            reply_packets = new Dictionary<int, byte[]>();
            reply_cbs = new Dictionary<int, ReplyCallback>();
            reply_packets_monitor = new Object();
        }

        protected abstract int TransportReceive(byte[] buf, int buf_offset, int len);

        protected abstract int TransportSend(byte[] buf, int buf_offset, int len);

        protected abstract void TransportSetTimeouts(int send_timeout, int receive_timeout);

        protected abstract void TransportClose();

        internal VersionInfo Version;

        int Receive(byte[] buf, int buf_offset, int len)
        {
            int offset = 0;

            while (offset < len)
            {
                int n = TransportReceive(buf, buf_offset + offset, len - offset);

                if (n == 0)
                    return offset;
                offset += n;
            }

            return offset;
        }

        // Do the wire protocol handshake
        internal void Connect(out Thread reciever)
        {
            byte[] buf = new byte[HANDSHAKE_STRING.Length];
            char[] cbuf = new char[buf.Length];

            // FIXME: Add a timeout
            int n = Receive(buf, 0, buf.Length);
            if (n == 0)
                throw new IOException("DWP Handshake failed.");
            for (int i = 0; i < buf.Length; ++i)
                cbuf[i] = (char)buf[i];

            if (new String(cbuf) != HANDSHAKE_STRING)
                throw new IOException("DWP Handshake failed.");

            TransportSend(buf, 0, buf.Length);

            receiver_thread = new Thread(new ThreadStart(receiver_thread_main));
            reciever = receiver_thread;
            receiver_thread.Start();

            Version = VM_GetVersion();

            //
            // Tell the debuggee our protocol version, so newer debuggees can work
            // with older clients
            //

            //
            // Older debuggees might not support this request
            EventHandler<ErrorHandlerEventArgs> OrigErrorHandler = ErrorHandler;
            ErrorHandler = null;
            ErrorHandler += delegate(object sender, ErrorHandlerEventArgs args)
            {
                throw new NotSupportedException();
            };
            try
            {
                VM_SetProtocolVersion(MAJOR_VERSION, MINOR_VERSION);
            }
            catch (NotSupportedException)
            {
            }
            ErrorHandler = OrigErrorHandler;
        }

        internal byte[] ReadPacket()
        {
            // FIXME: Throw ClosedConnectionException () if the connection is closed
            // FIXME: Throw ClosedConnectionException () if another thread closes the connection
            // FIXME: Locking
            byte[] header = new byte[HEADER_LENGTH];

            int len = Receive(header, 0, header.Length);
            if (len == 0)
                return new byte[0];
            if (len != HEADER_LENGTH)
            {
                // FIXME:
                throw new IOException("Packet of length " + len + " is read.");
            }

            int packetLength = GetPacketLength(header);
            if (packetLength < 11)
                throw new IOException("Invalid packet length.");

            if (packetLength == 11)
            {
                return header;
            }
            else
            {
                byte[] buf = new byte[packetLength];
                for (int i = 0; i < header.Length; ++i)
                    buf[i] = header[i];
                len = Receive(buf, header.Length, packetLength - header.Length);
                if (len != packetLength - header.Length)
                    throw new IOException();
                return buf;
            }
        }

        internal void WritePacket(byte[] packet)
        {
            // FIXME: Throw ClosedConnectionException () if the connection is closed
            // FIXME: Throw ClosedConnectionException () if another thread closes the connection
            // FIXME: Locking
            TransportSend(packet, 0, packet.Length);
        }

        internal void Close()
        {
            closed = true;
        }

        internal bool IsClosed
        {
            get
            {
                return closed;
            }
        }

        bool disconnected;

        void receiver_thread_main()
        {
            while (!closed)
            {
                try
                {
                    bool res = ReceivePacket();
                    if (!res)
                        break;
                }
                catch (Exception ex)
                {
                    Defiler.ErrLine(ex.Message);
                    break;
                }
            }

            lock (reply_packets_monitor)
            {
                disconnected = true;
                Monitor.PulseAll(reply_packets_monitor);
                TransportClose();
            }
            EventHandler.VMDisconnect(0, 0, null);
        }

        bool ReceivePacket()
        {
            byte[] packet = ReadPacket();

            if (packet.Length == 0)
            {
                return false;
            }

            if (IsReplyPacket(packet))
            {
                int id = GetPacketId(packet);
                ReplyCallback cb = null;
                lock (reply_packets_monitor)
                {
                    reply_cbs.TryGetValue(id, out cb);
                    if (cb == null)
                    {
                        reply_packets[id] = packet;
                        Monitor.PulseAll(reply_packets_monitor);
                    }
                }

                if (cb != null)
                    cb.Invoke(id, packet);
            }
            else
            {
                PacketReader r = new PacketReader(packet);

                if (r.CommandSet == CommandSet.EVENT && r.Command == (int)CmdEvent.COMPOSITE)
                {
                    int spolicy = r.ReadByte();
                    int nevents = r.ReadInt();

                    SuspendPolicy suspend_policy = decode_suspend_policy(spolicy);

                    EventInfo[] events = new EventInfo[nevents];

                    for (int i = 0; i < nevents; ++i)
                    {
                        EventKind kind = (EventKind)r.ReadByte();
                        int req_id = r.ReadInt();

                        EventType etype = (EventType)kind;

                        if (kind == EventKind.VM_START)
                        {
                            long thread_id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id };
                            //EventHandler.VMStart (req_id, thread_id, null);
                        }
                        else if (kind == EventKind.VM_DEATH)
                        {
                            //EventHandler.VMDeath (req_id, 0, null);
                            events[i] = new EventInfo(etype, req_id) { };
                        }
                        else if (kind == EventKind.THREAD_START)
                        {
                            long thread_id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = thread_id };
                            //EventHandler.ThreadStart (req_id, thread_id, thread_id);
                        }
                        else if (kind == EventKind.THREAD_DEATH)
                        {
                            long thread_id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = thread_id };
                            //EventHandler.ThreadDeath (req_id, thread_id, thread_id);
                        }
                        else if (kind == EventKind.ASSEMBLY_LOAD)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id };
                            //EventHandler.AssemblyLoad (req_id, thread_id, id);
                        }
                        else if (kind == EventKind.ASSEMBLY_UNLOAD)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id };
                            //EventHandler.AssemblyUnload (req_id, thread_id, id);
                        }
                        else if (kind == EventKind.TYPE_LOAD)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id };
                            //EventHandler.TypeLoad (req_id, thread_id, id);
                        }
                        else if (kind == EventKind.METHOD_ENTRY)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id };
                            //EventHandler.MethodEntry (req_id, thread_id, id);
                        }
                        else if (kind == EventKind.METHOD_EXIT)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id };
                            //EventHandler.MethodExit (req_id, thread_id, id);
                        }
                        else if (kind == EventKind.BREAKPOINT)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            long loc = r.ReadLong();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id, Location = loc };
                            //EventHandler.Breakpoint (req_id, thread_id, id, loc);
                        }
                        else if (kind == EventKind.STEP)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            long loc = r.ReadLong();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id, Location = loc };
                            //EventHandler.Step (req_id, thread_id, id, loc);
                        }
                        else if (kind == EventKind.EXCEPTION)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            long loc = 0; // FIXME
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id, Location = loc };
                            //EventHandler.Exception (req_id, thread_id, id, loc);
                        }
                        else if (kind == EventKind.APPDOMAIN_CREATE)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id };
                            //EventHandler.AppDomainCreate (req_id, thread_id, id);
                        }
                        else if (kind == EventKind.APPDOMAIN_UNLOAD)
                        {
                            long thread_id = r.ReadId();
                            long id = r.ReadId();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id };
                            //EventHandler.AppDomainUnload (req_id, thread_id, id);
                        }
                        else if (kind == EventKind.USER_BREAK)
                        {
                            long thread_id = r.ReadId();
                            long id = 0;
                            long loc = 0;
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Id = id, Location = loc };
                            //EventHandler.Exception (req_id, thread_id, id, loc);
                        }
                        else if (kind == EventKind.USER_LOG)
                        {
                            long thread_id = r.ReadId();
                            int level = r.ReadInt();
                            string category = r.ReadString();
                            string message = r.ReadString();
                            events[i] = new EventInfo(etype, req_id) { ThreadId = thread_id, Level = level, Category = category, Message = message };
                            //EventHandler.Exception (req_id, thread_id, id, loc);
                        }
                        else if (kind == EventKind.KEEPALIVE)
                        {
                            events[i] = new EventInfo(etype, req_id) { };
                        }
                        else
                        {
                            throw new NotImplementedException("Unknown event kind: " + kind);
                        }
                    }

                    EventHandler.Events(suspend_policy, events);
                }
            }

            return true;
        }

        internal IEventHandler EventHandler
        {
            get;
            set;
        }

        /* Send a request and call cb when a result is received */
        int Send(CommandSet command_set, int command, PacketWriter packet, Action<PacketReader> cb)
        {
            int id = IdGenerator;

            lock (reply_packets_monitor)
            {
                reply_cbs[id] = delegate(int packet_id, byte[] p)
                {
                    /* Run the callback on a tp thread to avoid blocking the receive thread */
                    PacketReader r = new PacketReader(p);
                    cb.BeginInvoke(r, null, null);
                };
            }

            if (packet == null)
                WritePacket(EncodePacket(id, (int)command_set, command, null, 0));
            else
                WritePacket(EncodePacket(id, (int)command_set, command, packet.Data, packet.Offset));

            return id;
        }

        PacketReader SendReceive(CommandSet command_set, int command, PacketWriter packet)
        {
            int id = IdGenerator;

            if (disconnected)
                throw new VMDisconnectedException();

            if (packet == null)
                WritePacket(EncodePacket(id, (int)command_set, command, null, 0));
            else
                WritePacket(EncodePacket(id, (int)command_set, command, packet.Data, packet.Offset));

            int packetId = id;

            /* Wait for the reply packet */
            while (true)
            {
                lock (reply_packets_monitor)
                {
                    if (reply_packets.ContainsKey(packetId))
                    {
                        byte[] reply = reply_packets[packetId];
                        reply_packets.Remove(packetId);
                        PacketReader r = new PacketReader(reply);
                        if (r.ErrorCode != 0)
                        {
                            if (ErrorHandler != null)
                                ErrorHandler(this, new ErrorHandlerEventArgs() { ErrorCode = (ErrorCode)r.ErrorCode });
                            throw new NotImplementedException("No error handler set.");
                        }
                        else
                        {
                            return r;
                        }
                    }
                    else
                    {
                        if (disconnected)
                            throw new VMDisconnectedException();
                        Monitor.Wait(reply_packets_monitor);
                    }
                }
            }
        }

        PacketReader SendReceive(CommandSet command_set, int command)
        {
            return SendReceive(command_set, command, null);
        }

        int packet_id_generator;

        int IdGenerator
        {
            get
            {
                return Interlocked.Increment(ref packet_id_generator);
            }
        }

        CattrInfo[] ReadCattrs(PacketReader r)
        {
            CattrInfo[] res = new CattrInfo[r.ReadInt()];
            for (int i = 0; i < res.Length; ++i)
            {
                CattrInfo info = new CattrInfo();
                info.ctor_id = r.ReadId();
                info.ctor_args = new ValueImpl[r.ReadInt()];
                for (int j = 0; j < info.ctor_args.Length; ++j)
                {
                    info.ctor_args[j] = r.ReadValue();
                }
                info.named_args = new CattrNamedArgInfo[r.ReadInt()];
                for (int j = 0; j < info.named_args.Length; ++j)
                {
                    CattrNamedArgInfo arg = new CattrNamedArgInfo();
                    int arg_type = r.ReadByte();
                    arg.is_property = arg_type == 0x54;
                    arg.id = r.ReadId();
                    arg.value = r.ReadValue();
                    info.named_args[j] = arg;
                }
                res[i] = info;
            }
            return res;
        }

        static ElementType TypeCodeToElementType(TypeCode c)
        {
            switch (c)
            {
                case TypeCode.Boolean:
                    return ElementType.Boolean;
                case TypeCode.Char:
                    return ElementType.Char;
                case TypeCode.SByte:
                    return ElementType.I1;
                case TypeCode.Byte:
                    return ElementType.U1;
                case TypeCode.Int16:
                    return ElementType.I2;
                case TypeCode.UInt16:
                    return ElementType.U2;
                case TypeCode.Int32:
                    return ElementType.I4;
                case TypeCode.UInt32:
                    return ElementType.U4;
                case TypeCode.Int64:
                    return ElementType.I8;
                case TypeCode.UInt64:
                    return ElementType.U8;
                case TypeCode.Single:
                    return ElementType.R4;
                case TypeCode.Double:
                    return ElementType.R8;
                default:
                    throw new NotImplementedException();
            }
        }

        /*
         * Implementation of debugger commands
         */

        internal VersionInfo VM_GetVersion()
        {
            var res = SendReceive(CommandSet.VM, (int)CmdVM.VERSION, null);
            VersionInfo info = new VersionInfo();
            info.VMVersion = res.ReadString();
            info.MajorVersion = res.ReadInt();
            info.MinorVersion = res.ReadInt();
            return info;
        }

        internal void VM_SetProtocolVersion(int major, int minor)
        {
            SendReceive(CommandSet.VM, (int)CmdVM.SET_PROTOCOL_VERSION, new PacketWriter().WriteInt(major).WriteInt(minor));
        }

        internal long[] VM_GetThreads()
        {
            var res = SendReceive(CommandSet.VM, (int)CmdVM.ALL_THREADS, null);
            int len = res.ReadInt();
            long[] arr = new long[len];
            for (int i = 0; i < len; ++i)
                arr[i] = res.ReadId();
            return arr;
        }

        internal void VM_Suspend()
        {
            SendReceive(CommandSet.VM, (int)CmdVM.SUSPEND);
        }

        internal void VM_Resume()
        {
            SendReceive(CommandSet.VM, (int)CmdVM.RESUME);
        }

        internal void VM_Exit(int exitCode)
        {
            SendReceive(CommandSet.VM, (int)CmdVM.EXIT, new PacketWriter().WriteInt(exitCode));
        }

        internal void VM_Dispose()
        {
            SendReceive(CommandSet.VM, (int)CmdVM.DISPOSE);
        }

        internal ValueImpl VM_InvokeMethod(long thread, long method, ValueImpl this_arg, ValueImpl[] arguments, InvokeFlags flags, out ValueImpl exc)
        {
            exc = null;
            PacketReader r = SendReceive(CommandSet.VM, (int)CmdVM.INVOKE_METHOD, new PacketWriter().WriteId(thread).WriteInt((int)flags).WriteId(method).WriteValue(this_arg).WriteInt(arguments.Length).WriteValues(arguments));
            if (r.ReadByte() == 0)
            {
                exc = r.ReadValue();
                return null;
            }
            else
            {
                return r.ReadValue();
            }
        }

        internal delegate void InvokeMethodCallback(ValueImpl v, ValueImpl exc, ErrorCode error, object state);

        internal int VM_BeginInvokeMethod(long thread, long method, ValueImpl this_arg, ValueImpl[] arguments, InvokeFlags flags, InvokeMethodCallback callback, object state)
        {
            return Send(CommandSet.VM, (int)CmdVM.INVOKE_METHOD, new PacketWriter().WriteId(thread).WriteInt((int)flags).WriteId(method).WriteValue(this_arg).WriteInt(arguments.Length).WriteValues(arguments), delegate(PacketReader r)
            {
                ValueImpl v, exc;

                if (r.ErrorCode != 0)
                {
                    callback(null, null, (ErrorCode)r.ErrorCode, state);
                }
                else
                {
                    if (r.ReadByte() == 0)
                    {
                        exc = r.ReadValue();
                        v = null;
                    }
                    else
                    {
                        v = r.ReadValue();
                        exc = null;
                    }

                    callback(v, exc, 0, state);
                }
            });
        }

        internal void VM_AbortInvoke(long thread, int id)
        {
            SendReceive(CommandSet.VM, (int)CmdVM.ABORT_INVOKE, new PacketWriter().WriteId(thread).WriteInt(id));
        }

        internal void SetSocketTimeouts(int send_timeout, int receive_timeout, int keepalive_interval)
        {
            TransportSetTimeouts(send_timeout, receive_timeout);
            SendReceive(CommandSet.VM, (int)CmdVM.SET_KEEPALIVE, new PacketWriter().WriteId(keepalive_interval));
        }

        /*
         * DOMAIN
         */

        internal long RootDomain
        {
            get
            {
                return SendReceive(CommandSet.APPDOMAIN, (int)CmdAppDomain.GET_ROOT_DOMAIN, null).ReadId();
            }
        }

        internal string Domain_GetName(long id)
        {
            return SendReceive(CommandSet.APPDOMAIN, (int)CmdAppDomain.GET_FRIENDLY_NAME, new PacketWriter().WriteId(id)).ReadString();
        }

        internal long[] Domain_GetAssemblies(long id)
        {
            var res = SendReceive(CommandSet.APPDOMAIN, (int)CmdAppDomain.GET_ASSEMBLIES, new PacketWriter().WriteId(id));
            int count = res.ReadInt();
            long[] assemblies = new long[count];
            for (int i = 0; i < count; ++i)
                assemblies[i] = res.ReadId();
            return assemblies;
        }

        internal long Domain_GetEntryAssembly(long id)
        {
            return SendReceive(CommandSet.APPDOMAIN, (int)CmdAppDomain.GET_ENTRY_ASSEMBLY, new PacketWriter().WriteId(id)).ReadId();
        }

        internal long Domain_GetCorlib(long id)
        {
            return SendReceive(CommandSet.APPDOMAIN, (int)CmdAppDomain.GET_CORLIB, new PacketWriter().WriteId(id)).ReadId();
        }

        internal long Domain_CreateString(long id, string s)
        {
            return SendReceive(CommandSet.APPDOMAIN, (int)CmdAppDomain.CREATE_STRING, new PacketWriter().WriteId(id).WriteString(s)).ReadId();
        }

        internal long Domain_CreateBoxedValue(long id, long type_id, ValueImpl v)
        {
            return SendReceive(CommandSet.APPDOMAIN, (int)CmdAppDomain.CREATE_BOXED_VALUE, new PacketWriter().WriteId(id).WriteId(type_id).WriteValue(v)).ReadId();
        }

        /*
         * METHOD
         */

        internal string Method_GetName(long id)
        {
            return SendReceive(CommandSet.METHOD, (int)CmdMethod.GET_NAME, new PacketWriter().WriteId(id)).ReadString();
        }

        internal long Method_GetDeclaringType(long id)
        {
            return SendReceive(CommandSet.METHOD, (int)CmdMethod.GET_DECLARING_TYPE, new PacketWriter().WriteId(id)).ReadId();
        }

        internal DebugInfo Method_GetDebugInfo(long id)
        {
            var res = SendReceive(CommandSet.METHOD, (int)CmdMethod.GET_DEBUG_INFO, new PacketWriter().WriteId(id));

            DebugInfo info = new DebugInfo();
            info.max_il_offset = res.ReadInt();
            info.filename = res.ReadString();

            int n_il_offsets = res.ReadInt();
            info.il_offsets = new int[n_il_offsets];
            info.line_numbers = new int[n_il_offsets];
            for (int i = 0; i < n_il_offsets; ++i)
            {
                info.il_offsets[i] = res.ReadInt();
                info.line_numbers[i] = res.ReadInt();
            }

            return info;
        }

        internal ParamInfo Method_GetParamInfo(long id)
        {
            var res = SendReceive(CommandSet.METHOD, (int)CmdMethod.GET_PARAM_INFO, new PacketWriter().WriteId(id));

            ParamInfo info = new ParamInfo();
            info.call_conv = res.ReadInt();
            info.param_count = res.ReadInt();
            info.generic_param_count = res.ReadInt();
            info.ret_type = res.ReadId();
            info.param_types = new long[info.param_count];
            for (int i = 0; i < info.param_count; ++i)
                info.param_types[i] = res.ReadId();
            info.param_names = new string[info.param_count];
            for (int i = 0; i < info.param_count; ++i)
                info.param_names[i] = res.ReadString();

            return info;
        }

        internal LocalsInfo Method_GetLocalsInfo(long id)
        {
            var res = SendReceive(CommandSet.METHOD, (int)CmdMethod.GET_LOCALS_INFO, new PacketWriter().WriteId(id));

            LocalsInfo info = new LocalsInfo();
            int nlocals = res.ReadInt();
            info.types = new long[nlocals];
            for (int i = 0; i < nlocals; ++i)
                info.types[i] = res.ReadId();
            info.names = new string[nlocals];
            for (int i = 0; i < nlocals; ++i)
                info.names[i] = res.ReadString();
            info.live_range_start = new int[nlocals];
            info.live_range_end = new int[nlocals];
            for (int i = 0; i < nlocals; ++i)
            {
                info.live_range_start[i] = res.ReadInt();
                info.live_range_end[i] = res.ReadInt();
            }

            return info;
        }

        internal MethodInfo Method_GetInfo(long id)
        {
            var res = SendReceive(CommandSet.METHOD, (int)CmdMethod.GET_INFO, new PacketWriter().WriteId(id));

            MethodInfo info = new MethodInfo();
            info.attributes = res.ReadInt();
            info.iattributes = res.ReadInt();
            info.token = res.ReadInt();

            return info;
        }

        internal MethodBodyInfo Method_GetBody(long id)
        {
            var res = SendReceive(CommandSet.METHOD, (int)CmdMethod.GET_BODY, new PacketWriter().WriteId(id));

            MethodBodyInfo info = new MethodBodyInfo();
            info.il = new byte[res.ReadInt()];
            for (int i = 0; i < info.il.Length; ++i)
                info.il[i] = (byte)res.ReadByte();

            return info;
        }

        internal ResolvedToken Method_ResolveToken(long id, int token)
        {
            var res = SendReceive(CommandSet.METHOD, (int)CmdMethod.RESOLVE_TOKEN, new PacketWriter().WriteId(id).WriteInt(token));

            TokenType type = (TokenType)res.ReadByte();
            switch (type)
            {
                case TokenType.STRING:
                    return new ResolvedToken() { Type = type, Str = res.ReadString() };
                case TokenType.TYPE:
                case TokenType.METHOD:
                case TokenType.FIELD:
                    return new ResolvedToken() { Type = type, Id = res.ReadId() };
                case TokenType.UNKNOWN:
                    return new ResolvedToken() { Type = type };
                default:
                    throw new NotImplementedException();
            }
        }

        /*
         * THREAD
         */

        internal string Thread_GetName(long id)
        {
            return SendReceive(CommandSet.THREAD, (int)CmdThread.GET_NAME, new PacketWriter().WriteId(id)).ReadString();
        }

        internal FrameInfo[] Thread_GetFrameInfo(long id, int start_frame, int length)
        {
            var res = SendReceive(CommandSet.THREAD, (int)CmdThread.GET_FRAME_INFO, new PacketWriter().WriteId(id).WriteInt(start_frame).WriteInt(length));
            int count = res.ReadInt();

            var frames = new FrameInfo[count];
            for (int i = 0; i < count; ++i)
            {
                frames[i].id = res.ReadInt();
                frames[i].method = res.ReadId();
                frames[i].il_offset = res.ReadInt();
                frames[i].flags = (StackFrameFlags)res.ReadByte();
            }
            return frames;
        }

        internal int Thread_GetState(long id)
        {
            return SendReceive(CommandSet.THREAD, (int)CmdThread.GET_STATE, new PacketWriter().WriteId(id)).ReadInt();
        }

        internal ThreadInfo Thread_GetInfo(long id)
        {
            PacketReader r = SendReceive(CommandSet.THREAD, (int)CmdThread.GET_INFO, new PacketWriter().WriteId(id));

            ThreadInfo res = new ThreadInfo() { is_thread_pool = r.ReadByte() > 0 ? true : false };

            return res;
        }

        internal long Thread_GetId(long id)
        {
            return SendReceive(CommandSet.THREAD, (int)CmdThread.GET_ID, new PacketWriter().WriteId(id)).ReadLong();
        }

        internal long Thread_GetTID(long id)
        {
            return SendReceive(CommandSet.THREAD, (int)CmdThread.GET_TID, new PacketWriter().WriteId(id)).ReadLong();
        }

        /*
         * MODULE
         */

        internal ModuleInfo Module_GetInfo(long id)
        {
            PacketReader r = SendReceive(CommandSet.MODULE, (int)CmdModule.GET_INFO, new PacketWriter().WriteId(id));
            ModuleInfo info = new ModuleInfo { Name = r.ReadString(), ScopeName = r.ReadString(), FQName = r.ReadString(), Guid = r.ReadString(), Assembly = r.ReadId() };
            return info;
        }

        /*
         * ASSEMBLY
         */

        internal string Assembly_GetLocation(long id)
        {
            return SendReceive(CommandSet.ASSEMBLY, (int)CmdAssembly.GET_LOCATION, new PacketWriter().WriteId(id)).ReadString();
        }

        internal long Assembly_GetEntryPoint(long id)
        {
            return SendReceive(CommandSet.ASSEMBLY, (int)CmdAssembly.GET_ENTRY_POINT, new PacketWriter().WriteId(id)).ReadId();
        }

        internal long Assembly_GetManifestModule(long id)
        {
            return SendReceive(CommandSet.ASSEMBLY, (int)CmdAssembly.GET_MANIFEST_MODULE, new PacketWriter().WriteId(id)).ReadId();
        }

        internal long Assembly_GetObject(long id)
        {
            return SendReceive(CommandSet.ASSEMBLY, (int)CmdAssembly.GET_OBJECT, new PacketWriter().WriteId(id)).ReadId();
        }

        internal long Assembly_GetType(long id, string name, bool ignoreCase)
        {
            return SendReceive(CommandSet.ASSEMBLY, (int)CmdAssembly.GET_TYPE, new PacketWriter().WriteId(id).WriteString(name).WriteBool(ignoreCase)).ReadId();
        }

        internal string Assembly_GetName(long id)
        {
            return SendReceive(CommandSet.ASSEMBLY, (int)CmdAssembly.GET_NAME, new PacketWriter().WriteId(id)).ReadString();
        }

        /*
         * TYPE
         */

        internal TypeInfo Type_GetInfo(long id)
        {
            PacketReader r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_INFO, new PacketWriter().WriteId(id));
            TypeInfo res = new TypeInfo();

            res.ns = r.ReadString();
            res.name = r.ReadString();
            res.full_name = r.ReadString();
            res.assembly = r.ReadId();
            res.module = r.ReadId();
            res.base_type = r.ReadId();
            res.element_type = r.ReadId();
            res.token = r.ReadInt();
            res.rank = r.ReadByte();
            res.attributes = r.ReadInt();
            int b = r.ReadByte();
            res.is_byref = (b & 1) != 0;
            res.is_pointer = (b & 2) != 0;
            res.is_primitive = (b & 4) != 0;
            res.is_valuetype = (b & 8) != 0;
            res.is_enum = (b & 16) != 0;

            int nested_len = r.ReadInt();
            res.nested = new long[nested_len];
            for (int i = 0; i < nested_len; ++i)
                res.nested[i] = r.ReadId();

            return res;
        }

        internal long[] Type_GetMethods(long id)
        {
            PacketReader r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_METHODS, new PacketWriter().WriteId(id));

            int n = r.ReadInt();
            long[] res = new long[n];
            for (int i = 0; i < n; ++i)
                res[i] = r.ReadId();
            return res;
        }

        internal long[] Type_GetFields(long id, out string[] names, out long[] types, out int[] attrs)
        {
            PacketReader r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_FIELDS, new PacketWriter().WriteId(id));

            int n = r.ReadInt();
            long[] res = new long[n];
            names = new string[n];
            types = new long[n];
            attrs = new int[n];
            for (int i = 0; i < n; ++i)
            {
                res[i] = r.ReadId();
                names[i] = r.ReadString();
                types[i] = r.ReadId();
                attrs[i] = r.ReadInt();
            }
            return res;
        }

        internal PropInfo[] Type_GetProperties(long id)
        {
            PacketReader r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_PROPERTIES, new PacketWriter().WriteId(id));

            int n = r.ReadInt();
            PropInfo[] res = new PropInfo[n];
            for (int i = 0; i < n; ++i)
            {
                res[i] = new PropInfo();
                res[i].id = r.ReadId();
                res[i].name = r.ReadString();
                res[i].get_method = r.ReadId();
                res[i].set_method = r.ReadId();
                res[i].attrs = r.ReadInt();
            }

            return res;
        }

        internal long Type_GetObject(long id)
        {
            return SendReceive(CommandSet.TYPE, (int)CmdType.GET_OBJECT, new PacketWriter().WriteId(id)).ReadId();
        }

        internal ValueImpl[] Type_GetValues(long id, long[] fields, long thread_id)
        {
            int len = fields.Length;
            PacketReader r;
            if (thread_id != 0)
                r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_VALUES_2, new PacketWriter().WriteId(id).WriteId(thread_id).WriteInt(len).WriteIds(fields));
            else
                r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_VALUES, new PacketWriter().WriteId(id).WriteInt(len).WriteIds(fields));

            ValueImpl[] res = new ValueImpl[len];
            for (int i = 0; i < len; ++i)
                res[i] = r.ReadValue();
            return res;
        }

        internal void Type_SetValues(long id, long[] fields, ValueImpl[] values)
        {
            SendReceive(CommandSet.TYPE, (int)CmdType.SET_VALUES, new PacketWriter().WriteId(id).WriteInt(fields.Length).WriteIds(fields).WriteValues(values));
        }

        internal string[] Type_GetSourceFiles(long id, bool return_full_paths)
        {
            var r = SendReceive(CommandSet.TYPE, return_full_paths ? (int)CmdType.GET_SOURCE_FILES_2 : (int)CmdType.GET_SOURCE_FILES, new PacketWriter().WriteId(id));
            int len = r.ReadInt();
            string[] res = new string[len];
            for (int i = 0; i < len; ++i)
                res[i] = r.ReadString();
            return res;
        }

        internal bool Type_IsAssignableFrom(long id, long c_id)
        {
            return SendReceive(CommandSet.TYPE, (int)CmdType.IS_ASSIGNABLE_FROM, new PacketWriter().WriteId(id).WriteId(c_id)).ReadByte() > 0;
        }

        internal CattrInfo[] Type_GetCustomAttributes(long id, long attr_type_id, bool inherit)
        {
            PacketReader r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_CATTRS, new PacketWriter().WriteId(id).WriteId(attr_type_id));
            return ReadCattrs(r);
        }

        internal CattrInfo[] Type_GetFieldCustomAttributes(long id, long field_id, long attr_type_id, bool inherit)
        {
            PacketReader r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_FIELD_CATTRS, new PacketWriter().WriteId(id).WriteId(field_id).WriteId(attr_type_id));
            return ReadCattrs(r);
        }

        internal CattrInfo[] Type_GetPropertyCustomAttributes(long id, long field_id, long attr_type_id, bool inherit)
        {
            PacketReader r = SendReceive(CommandSet.TYPE, (int)CmdType.GET_PROPERTY_CATTRS, new PacketWriter().WriteId(id).WriteId(field_id).WriteId(attr_type_id));
            return ReadCattrs(r);
        }

        /*
         * EVENTS
         */

        internal int EnableEvent(EventType etype, SuspendPolicy suspend_policy, List<Modifier> mods)
        {
            var w = new PacketWriter().WriteByte((byte)etype).WriteByte((byte)suspend_policy);
            if (mods != null)
            {
                if (mods.Count > 255)
                    throw new NotImplementedException();
                w.WriteByte((byte)mods.Count);
                foreach (Modifier mod in mods)
                {
                    if (mod is CountModifier)
                    {
                        w.WriteByte((byte)ModifierKind.COUNT);
                        w.WriteInt((mod as CountModifier).Count);
                    }
                    else if (mod is LocationModifier)
                    {
                        w.WriteByte((byte)ModifierKind.LOCATION_ONLY);
                        w.WriteId((mod as LocationModifier).Method);
                        w.WriteLong((mod as LocationModifier).Location);
                    }
                    else if (mod is StepModifier)
                    {
                        w.WriteByte((byte)ModifierKind.STEP);
                        w.WriteId((mod as StepModifier).Thread);
                        w.WriteInt((mod as StepModifier).Size);
                        w.WriteInt((mod as StepModifier).Depth);
                    }
                    else if (mod is ThreadModifier)
                    {
                        w.WriteByte((byte)ModifierKind.THREAD_ONLY);
                        w.WriteId((mod as ThreadModifier).Thread);
                    }
                    else if (mod is ExceptionModifier)
                    {
                        var em = mod as ExceptionModifier;
                        w.WriteByte((byte)ModifierKind.EXCEPTION_ONLY);
                        w.WriteId(em.Type);
                        if (Version.MajorVersion > 2 || Version.MinorVersion > 0)
                        {
                            /* This is only supported in protocol version 2.1 */
                            w.WriteBool(em.Caught);
                            w.WriteBool(em.Uncaught);
                        }
                        else if (!em.Caught || !em.Uncaught)
                        {
                            throw new NotSupportedException("This request is not supported by the protocol version implemented by the debuggee.");
                        }
                    }
                    else if (mod is AssemblyModifier)
                    {
                        w.WriteByte((byte)ModifierKind.ASSEMBLY_ONLY);
                        var amod = (mod as AssemblyModifier);
                        w.WriteInt(amod.Assemblies.Length);
                        foreach (var id in amod.Assemblies)
                            w.WriteId(id);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }
            else
            {
                w.WriteByte(0);
            }
            return SendReceive(CommandSet.EVENT_REQUEST, (int)CmdEventRequest.SET, w).ReadInt();
        }

        internal void ClearEventRequest(EventType etype, int req_id)
        {
            SendReceive(CommandSet.EVENT_REQUEST, (int)CmdEventRequest.CLEAR, new PacketWriter().WriteByte((byte)etype).WriteInt(req_id));
        }

        internal void ClearAllBreakpoints()
        {
            SendReceive(CommandSet.EVENT_REQUEST, (int)CmdEventRequest.CLEAR_ALL_BREAKPOINTS, new PacketWriter());
        }

        /*
         * STACK FRAME
         */
        internal ValueImpl StackFrame_GetThis(long thread_id, long id)
        {
            PacketReader r = SendReceive(CommandSet.STACK_FRAME, (int)CmdStackFrame.GET_THIS, new PacketWriter().WriteId(thread_id).WriteId(id));
            return r.ReadValue();
        }

        internal ValueImpl[] StackFrame_GetValues(long thread_id, long id, int[] pos)
        {
            /* pos < 0 -> argument at pos (-pos) - 1 */
            /* pos >= 0 -> local at pos */
            int len = pos.Length;
            PacketReader r = SendReceive(CommandSet.STACK_FRAME, (int)CmdStackFrame.GET_VALUES, new PacketWriter().WriteId(thread_id).WriteId(id).WriteInt(len).WriteInts(pos));

            ValueImpl[] res = new ValueImpl[len];
            for (int i = 0; i < len; ++i)
                res[i] = r.ReadValue();
            return res;
        }

        internal void StackFrame_SetValues(long thread_id, long id, int[] pos, ValueImpl[] values)
        {
            /* pos < 0 -> argument at pos (-pos) - 1 */
            /* pos >= 0 -> local at pos */
            int len = pos.Length;
            SendReceive(CommandSet.STACK_FRAME, (int)CmdStackFrame.SET_VALUES, new PacketWriter().WriteId(thread_id).WriteId(id).WriteInt(len).WriteInts(pos).WriteValues(values));
        }

        /*
         * ARRAYS
         */
        internal int[] Array_GetLength(long id, out int rank, out int[] lower_bounds)
        {
            var r = SendReceive(CommandSet.ARRAY_REF, (int)CmdArrayRef.GET_LENGTH, new PacketWriter().WriteId(id));
            rank = r.ReadInt();
            int[] res = new int[rank];
            lower_bounds = new int[rank];
            for (int i = 0; i < rank; ++i)
            {
                res[i] = r.ReadInt();
                lower_bounds[i] = r.ReadInt();
            }
            return res;
        }

        internal ValueImpl[] Array_GetValues(long id, int index, int len)
        {
            var r = SendReceive(CommandSet.ARRAY_REF, (int)CmdArrayRef.GET_VALUES, new PacketWriter().WriteId(id).WriteInt(index).WriteInt(len));
            ValueImpl[] res = new ValueImpl[len];
            for (int i = 0; i < len; ++i)
                res[i] = r.ReadValue();
            return res;
        }

        internal void Array_SetValues(long id, int index, ValueImpl[] values)
        {
            SendReceive(CommandSet.ARRAY_REF, (int)CmdArrayRef.SET_VALUES, new PacketWriter().WriteId(id).WriteInt(index).WriteInt(values.Length).WriteValues(values));
        }

        /*
         * STRINGS
         */
        internal string String_GetValue(long id)
        {
            return SendReceive(CommandSet.STRING_REF, (int)CmdStringRef.GET_VALUE, new PacketWriter().WriteId(id)).ReadString();
        }

        /*
         * OBJECTS
         */
        internal long Object_GetType(long id)
        {
            return SendReceive(CommandSet.OBJECT_REF, (int)CmdObjectRef.GET_TYPE, new PacketWriter().WriteId(id)).ReadId();
        }

        internal long Object_GetDomain(long id)
        {
            return SendReceive(CommandSet.OBJECT_REF, (int)CmdObjectRef.GET_DOMAIN, new PacketWriter().WriteId(id)).ReadId();
        }

        internal ValueImpl[] Object_GetValues(long id, long[] fields)
        {
            int len = fields.Length;
            PacketReader r = SendReceive(CommandSet.OBJECT_REF, (int)CmdObjectRef.GET_VALUES, new PacketWriter().WriteId(id).WriteInt(len).WriteIds(fields));

            ValueImpl[] res = new ValueImpl[len];
            for (int i = 0; i < len; ++i)
                res[i] = r.ReadValue();
            return res;
        }

        internal void Object_SetValues(long id, long[] fields, ValueImpl[] values)
        {
            SendReceive(CommandSet.OBJECT_REF, (int)CmdObjectRef.SET_VALUES, new PacketWriter().WriteId(id).WriteInt(fields.Length).WriteIds(fields).WriteValues(values));
        }

        internal bool Object_IsCollected(long id)
        {
            return SendReceive(CommandSet.OBJECT_REF, (int)CmdObjectRef.IS_COLLECTED, new PacketWriter().WriteId(id)).ReadInt() == 1;
        }

        internal long Object_GetAddress(long id)
        {
            return SendReceive(CommandSet.OBJECT_REF, (int)CmdObjectRef.GET_ADDRESS, new PacketWriter().WriteId(id)).ReadLong();
        }

        internal ObjectRefInfo Object_GetInfo(long id)
        {
            ObjectRefInfo res = new ObjectRefInfo();
            PacketReader r = SendReceive(CommandSet.OBJECT_REF, (int)CmdObjectRef.GET_INFO, new PacketWriter().WriteId(id));

            res.type_id = r.ReadId();
            res.domain_id = r.ReadId();
            return res;
        }

    }

    /* This is the interface exposed by the debugger towards the debugger agent */
    interface IEventHandler
    {
        void Events(SuspendPolicy suspend_policy, EventInfo[] events);

        void VMDisconnect(int req_id, long thread_id, string vm_uri);
    }
}
