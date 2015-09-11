#if !__MOBILE__
using System;
using System.Collections.Generic;
using System.Text;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

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
}
#endif
