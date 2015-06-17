using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;

namespace Sce.Psm
{
    internal enum ScePsmEdataStatus
    {
        ERROR_BADF = -2138111991,
        ERROR_CRYPTO = -2138111806,
        ERROR_ECONTENTID = -2138111802,
        ERROR_EFWRITE = -2138111803,
        ERROR_EGENRANDOM = -2138111801,
        ERROR_EISDIR = -2138111979,
        ERROR_FATAL = -2138111804,
        ERROR_FFORMAT = -2138111953,
        ERROR_FINDSEED = -2138111807,
        ERROR_FREAD = -2138111805,
        ERROR_INVAL = -2138111978,
        ERROR_MFILE = -2138111976,
        ERROR_NOENT = -2138111998,
        ERROR_NOT_IMPLEMENTED = -2138111808,
        ERROR_OVERFLOW = -2138111861,
        OK = 0
    }

    public enum ScePsmEdataType
    {
        INVALID = 0,
        READONLY_TYPE1 = 1,
        READONLY_TYPE2 = 2,
        READONLY_TYPE3 = 3,
        READONLY_TYPE4 = 4,
        WRITABLE_TYPE1 = -2147483647,
        WRITABLE_TYPE2 = -2139095038,
        WRITABLE_TYPE3 = -2147483645,
        WRITABLE_TYPE4 = -2147483644
    }

    internal static class ScePsmEdata32
    {
        private const string NATIVE_DLL = @"..\lib\psm_encrypter32.dll";

        [DllImport(NATIVE_DLL, EntryPoint = "scePsmEdataEncrypt", CharSet = CharSet.Ansi)]
        public static extern ScePsmEdataStatus Encrypt(string inFile, string outFile, string installPath, ScePsmEdataType type, IntPtr devPkcs12, int devPkcs12Size, IntPtr hostKdbg, int hostKdbgSize);
        [DllImport(NATIVE_DLL, EntryPoint = "scePsmEdataEncrypt", CharSet = CharSet.Ansi)]
        public static extern ScePsmEdataStatus Encrypt(string inFile, string outFile, string installPath, ScePsmEdataType type, string devPkcs12, int devPkcs12Size, string hostKdbg, int hostKdbgSize);

    }

    internal static class ScePsmEdata64
    {
        // Fields
        private const string NATIVE_DLL = @"..\lib\psm_encrypter64.dll";

        // Methods
        [DllImport(NATIVE_DLL, EntryPoint = "scePsmEdataEncrypt", CharSet = CharSet.Ansi)]
        public static extern ScePsmEdataStatus Encrypt(string inFile, string outFile, string installPath, ScePsmEdataType type, IntPtr devPkcs12, int devPkcs12Size, IntPtr hostKdbg, int hostKdbgSize);
        [DllImport(NATIVE_DLL, EntryPoint = "scePsmEdataEncrypt", CharSet = CharSet.Ansi)]
        public static extern ScePsmEdataStatus Encrypt(string inFile, string outFile, string installPath, ScePsmEdataType type, string devPkcs12, int devPkcs12Size, string hostKdbg, int hostKdbgSize);
    }

    public enum EdataType
    {
        Invalid = 0,
        ReadonlyIcv = 3,
        ReadonlyIcvAndCrypto = 1,
        ReadonlyIcvAndScramble = 2,
        ReadonlyWholeSignature = 4,
        WritableIcv = -2147483645,
        WritableIcvAndCrypto = -2147483647,
        WritableIcvAndScramble = -2139095038,
        WritableWholeSignature = -2147483644
    }

    public class ScePsmEdataException : Exception
    {
        // Fields
        public static Dictionary<PsmEdataStatus, string> dicPsmEdataErrorMessageEn;

        // Methods
        static ScePsmEdataException()
        {
            Dictionary<PsmEdataStatus, string> dictionary = new Dictionary<PsmEdataStatus, string>();
            dictionary.Add(PsmEdataStatus.SCE_OK, "Success.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_NOENT, "No such file or directory.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_BADF, "Bad file handler.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_EISDIR, "Is a directory.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_INVAL, "Invalid argument.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_ECONTENTID, "Content ID is invalid.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_MFILE, "Too many open files.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FFORMAT, "Illegal file format for EData.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_OVERFLOW, "Value too large to be stored in data type.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_ALREADY_INITIALIZED, "Already initalized.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_NOT_INITIALIZED, "Not initalized.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FATAL, "System fatal.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_NOT_IMPLEMENTED, "not implemented.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FINDSEED, "Seed not found.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_CRYPTO, "Crypt error.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_OPENSSL, "Please remake App Key Ring file from PSM Publishing Utility.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_VERIFY_ICV, "Verify ICV error.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_HEADER_SIGNATULRE, "Header Signature verify error.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_WHOLE_SIGNATULRE, "Whole Signature verify error.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FILE_NOT_OPENED, "File not opened.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_PLAIN_TO_LARGE, "The plain file is too large.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_EDATA_TO_LARGE, "The edata file is too large.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_KEY_FILE_OPEN, "Cannot open a key file .");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_KEY_FILE_READ, "Cannot read a key file .");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FOPEN, "Cannot open a file stream.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FOPENED, "File is already open.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FSEEK, "Cannot seek a file stream.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FSTAT, "Cannot stat a file stream.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_FREAD, "Cannot read a file stream.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_EFWRITE, "Cannot write a file stream.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_EGENRANDOM, "Cannot Random number generat.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_THREAD_MODULE, "Create or destroy thread.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_THREAD_MALLOC, "Cannot malloc a thread memory.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_THREAD, "Thead Error.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_INVALID_LICENSE, "License file is invalid.");
            dictionary.Add(PsmEdataStatus.SCE_PSM_EDATA_ERROR_NOT_ACTIVATED, "Device is not activated.");
            dicPsmEdataErrorMessageEn = dictionary;
        }

        public ScePsmEdataException(int errnum, string message)
            : base(PsmEdataMessage((PsmEdataStatus)errnum, message))
        {
        }

        public static string PsmEdataMessage(PsmEdataStatus errCode, string message)
        {
            string str = "";
            if (dicPsmEdataErrorMessageEn.ContainsKey(errCode))
            {
                str = dicPsmEdataErrorMessageEn[errCode];
            }
            else
            {
                str = "Unknown Error Message";
            }
            return string.Format("{0} ({1}), 0x{1:X} \"{2}\"", message, (int)errCode, str);
        }

        // Nested Types
        public enum PsmEdataStatus
        {
            SCE_OK = 0,
            SCE_PSM_EDATA_ERROR_ALREADY_INITIALIZED = -2138111168,
            SCE_PSM_EDATA_ERROR_BADF = -2138111223,
            SCE_PSM_EDATA_ERROR_CRYPTO = -2138111069,
            SCE_PSM_EDATA_ERROR_ECONTENTID = -2138111209,
            SCE_PSM_EDATA_ERROR_EDATA_TO_LARGE = -2138111062,
            SCE_PSM_EDATA_ERROR_EFWRITE = -2138111049,
            SCE_PSM_EDATA_ERROR_EGENRANDOM = -2138111048,
            SCE_PSM_EDATA_ERROR_EISDIR = -2138111211,
            SCE_PSM_EDATA_ERROR_FATAL = -2138111072,
            SCE_PSM_EDATA_ERROR_FFORMAT = -2138111185,
            SCE_PSM_EDATA_ERROR_FILE_NOT_OPENED = -2138111064,
            SCE_PSM_EDATA_ERROR_FINDSEED = -2138111070,
            SCE_PSM_EDATA_ERROR_FOPEN = -2138111054,
            SCE_PSM_EDATA_ERROR_FOPENED = -2138111053,
            SCE_PSM_EDATA_ERROR_FREAD = -2138111050,
            SCE_PSM_EDATA_ERROR_FSEEK = -2138111052,
            SCE_PSM_EDATA_ERROR_FSTAT = -2138111051,
            SCE_PSM_EDATA_ERROR_HEADER_SIGNATULRE = -2138111066,
            SCE_PSM_EDATA_ERROR_INVAL = -2138111210,
            SCE_PSM_EDATA_ERROR_INVALID_LICENSE = -2138111024,
            SCE_PSM_EDATA_ERROR_KEY_FILE_OPEN = -2138111056,
            SCE_PSM_EDATA_ERROR_KEY_FILE_READ = -2138111055,
            SCE_PSM_EDATA_ERROR_MFILE = -2138111208,
            SCE_PSM_EDATA_ERROR_NOENT = -2138111230,
            SCE_PSM_EDATA_ERROR_NOT_ACTIVATED = -2138111023,
            SCE_PSM_EDATA_ERROR_NOT_IMPLEMENTED = -2138111071,
            SCE_PSM_EDATA_ERROR_NOT_INITIALIZED = -2138111167,
            SCE_PSM_EDATA_ERROR_OPENSSL = -2138111068,
            SCE_PSM_EDATA_ERROR_OVERFLOW = -2138111093,
            SCE_PSM_EDATA_ERROR_PLAIN_TO_LARGE = -2138111063,
            SCE_PSM_EDATA_ERROR_THREAD = -2138111038,
            SCE_PSM_EDATA_ERROR_THREAD_MALLOC = -2138111039,
            SCE_PSM_EDATA_ERROR_THREAD_MODULE = -2138111040,
            SCE_PSM_EDATA_ERROR_VERIFY_ICV = -2138111067,
            SCE_PSM_EDATA_ERROR_WHOLE_SIGNATULRE = -2138111065
        }
    }

    public static class Edata
    {
        // Methods
        public static void Encrypt(string inFile, string outFile, string installPath, ScePsmEdataType type, IntPtr devPkcs12, int devPkcs12Size, IntPtr hostKdbg, int hostKdbgSize)
        {
            ScePsmEdataStatus status = (IntPtr.Size == 8) ? ScePsmEdata64.Encrypt(inFile, outFile, installPath, type, devPkcs12, devPkcs12Size, hostKdbg, hostKdbgSize) : ScePsmEdata32.Encrypt(inFile, outFile, installPath, type, devPkcs12, devPkcs12Size, hostKdbg, hostKdbgSize);
            if (status != ScePsmEdataStatus.OK)
            {
                throw new ScePsmEdataException((int)status, "Error encrypting file.");
            }
        }

        public static void Run(string input, string output, string targetPath, EdataType type, string fileOfPublisherKey, string fileOfAppDevKey)
        {
            int cb = 0;
            IntPtr destination = new IntPtr();
            int length = 0;
            IntPtr ptr2 = new IntPtr();
            try
            {
                if (File.Exists(fileOfPublisherKey) && File.Exists(fileOfAppDevKey))
                {
                    byte[] source = File.ReadAllBytes(fileOfPublisherKey);
                    cb = source.Length;
                    destination = Marshal.AllocHGlobal(cb);
                    Marshal.Copy(source, 0, destination, source.Length);
                    byte[] buffer2 = File.ReadAllBytes(fileOfAppDevKey);
                    length = buffer2.Length;
                    ptr2 = Marshal.AllocHGlobal(length);
                    Marshal.Copy(buffer2, 0, ptr2, buffer2.Length);
                    Encrypt(input, output, targetPath, (ScePsmEdataType)type, destination, cb, ptr2, length);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(destination);
                Marshal.FreeHGlobal(ptr2);
            }
        }
    }
}
