using System;
using System.Runtime.InteropServices;

namespace Sce.Psm
{
    internal static class ScePsmDev32
    {
        // Fields
        public const int DeviceMax = 8;
        private const string NATIVE_DLL = "psm_device32.dll";

        // Methods
        [DllImport(NATIVE_DLL, EntryPoint = "scePsmDevCreatePackage", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern ScePsmDevErrorCode CreatePackage(string packageFile, string dirForPack);
    }

    internal static class ScePsmDev64
    {
        // Fields
        public const int DeviceMax = 8;
        private const string NATIVE_DLL = "psm_device64.dll";

        // Methods
        [DllImport(NATIVE_DLL, EntryPoint = "scePsmDevCreatePackage", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern ScePsmDevErrorCode CreatePackage(string packageFile, string dirForPack);
    }

    internal enum ScePsmDevErrorCode
    {
        CannotAccessStorage = -2147418107,
        InvalidAppID = -2147418109,
        InvalidFilepath = -2147418108,
        InvalidPackage = -2147418110,
        Invalidpacket = -2147418098,
        NoConnection = -2147418111,
        Ok = 0,
        StorageFull = -2147418106,
        TargetLaunched = -2147418097,
        VersionHost = -2147418100,
        VersionTarget = -2147418099
    }

    public class PsmDevice
    {
        // Methods
        public static void CreatePackage(string packageFile, string directoryToPackage)
        {
            ScePsmDevErrorCode code = (IntPtr.Size == 8) ? ScePsmDev64.CreatePackage(packageFile, directoryToPackage) : ScePsmDev32.CreatePackage(packageFile, directoryToPackage);
            if (code != ScePsmDevErrorCode.Ok)
            {
                throw new Exception(string.Format("\nCreation package failed.\n({0}), 0x{0:X}", (int)code));
            }
        }
    }
}
