using System;
using System.IO;
using Sce.Psm;

namespace PsmSigner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 4)
            {
                Console.Error.WriteLine("usage: PsmSigner.exe appkey pubkey input output");
                Console.Error.WriteLine("   appkey  .khapp key for project name (in app.xml)");
                Console.Error.WriteLine("   pubkey  kdev.p12 publisher key");
                Console.Error.WriteLine("   input   directory containing unsigned files");
                Console.Error.WriteLine("   output  .psdp PSM package output");
                return;
            }

            string appkey = args[0];
            string pubkey = args[1];
            string indir = args[2];
            string outpath = args[3];

            if (!File.Exists(appkey))
            {
                Console.Error.WriteLine("Cannot find appkey");
                return;
            }
            if (!File.Exists(pubkey))
            {
                Console.Error.WriteLine("Cannot find appkey");
                return;
            }
            if (!Directory.Exists(indir))
            {
                Console.Error.WriteLine("input invalid");
                return;
            }

            // create temporary directory
            string tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            // encrypt files
            if (!EncryptFiles(indir, tmp, pubkey, appkey))
            {
                Console.Error.WriteLine("Failed to encrypt files.");
                return;
            }

            // first create edata.list
            if (!CreateEdataList(Path.Combine(indir, "Application"), Path.Combine(tmp, "Application\\edata.list")))
            {
                Console.Error.WriteLine("Cannot create edata.list");
                return;
            }

            // next encrypt edata.list to psse.list
            Edata.Run(Path.Combine(tmp, "Application\\edata.list"), Path.Combine(tmp, "Application\\psse.list"), "/Application/psse.list", EdataType.ReadonlyIcvAndCrypto, pubkey, appkey);

            // next create the package
            PsmDevice.CreatePackage(outpath, tmp);

            Console.ReadLine();
        }

        public static bool NeedsEncryption(string path)
        {
            string ext = Path.GetExtension(path);
            return (ext == ".cgx") || (ext == ".exe") || (ext == ".dll");
        }

        public static bool EncryptFiles(string inpath, string outpath, string pubkey, string appkey)
        {
            try
            {
                foreach (string file in Directory.EnumerateFiles(inpath, "*", SearchOption.AllDirectories))
                {
                    string relpath = file.Substring(inpath.Length + 1);
                    string outfile = Path.Combine(outpath, relpath);
                    if (!Directory.Exists(Path.GetDirectoryName(outfile)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(outfile));
                    }
                    if (NeedsEncryption(file))
                    {
                        Edata.Run(file, outfile, "/" + relpath.Replace('\\','/'), EdataType.ReadonlyIcvAndCrypto, pubkey, appkey);
                    }
                    else
                    {
                        File.Copy(file, outfile);
                    }
                }
                return true;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return false;
            }
        }

        public static bool CreateEdataList(string inpath, string outfile)
        {
            try
            {
                if (!Directory.Exists(Path.GetDirectoryName(outfile)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(outfile));
                }
                using (StreamWriter write = new StreamWriter(outfile))
                {
                    foreach (string file in Directory.EnumerateFiles(inpath, "*", SearchOption.AllDirectories))
                    {
                        if (NeedsEncryption(file))
                        {
                            write.WriteLine(file.Substring(inpath.Length + 1).Replace('\\','/'));
                        }
                    }
                }
                return true;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
