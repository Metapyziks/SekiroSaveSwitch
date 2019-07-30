using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CommandLine;

namespace SekiroSaveSwitch
{
    class Program
    {
        class Options
        {
            [Option('i', "input", Required = true, HelpText = "Source save file to convert.")]
            public string Input { get; set; }

            [Option('o', "output", Required = true, HelpText = "Destination to store to converted file.")]
            public string Output { get; set; }

            [Option('s', "steam-id", Required = true, HelpText = "Steam ID to inject into the converted file.")]
            public ulong SteamId { get; set; }
        }

        static int Main(string[] args)
        {
            try
            {
                Parser.Default
                    .ParseArguments<Options>(args)
                    .WithParsed(Convert);

                return 0;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
        }

        static void Convert(Options options)
        {
            if (!File.Exists(options.Input))
            {
                throw new Exception("Input file does not exist.");
            }

            if (File.Exists(options.Output))
            {
                // Backup old save if it exists

                var saveDir = Path.GetDirectoryName(options.Output);
                var backupDir = Path.Combine(saveDir, "backup");
                var ext = Path.GetExtension(options.Output);

                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                var backupName = $"{Path.GetFileNameWithoutExtension(options.Output)}.{DateTime.Now:yyyy-MM-dd-hh-mm-ss}{ext}";
                var backupPath = Path.Combine(backupDir, backupName);

                Console.WriteLine($"Backing up old save to \"{backupPath}\"");

                File.Copy(options.Output, backupPath, false);
            }

            Console.WriteLine("Copying input to output path");
            File.Copy(options.Input, options.Output, true);

            const long steamIdOffset1 = 0x34164;
            const long steamIdOffset2 = 0xA003D4;

            const long hashedRangeStart = 0x310;
            const int hashedRangeLength = 0x100000;

            using (var stream = File.Open(options.Output, FileMode.Open, FileAccess.ReadWrite))
            {
                stream.Seek(steamIdOffset1, SeekOrigin.Begin);

                var buffer = new byte[sizeof(ulong)];
                stream.Read(buffer, 0, sizeof(ulong));

                var oldSteamId = BitConverter.ToUInt64(buffer, 0);

                Console.WriteLine($"Old SteamID: {oldSteamId}");

                buffer = BitConverter.GetBytes(options.SteamId);

                Console.WriteLine($"Writing new SteamID: {options.SteamId}");

                stream.Seek(steamIdOffset1, SeekOrigin.Begin);
                stream.Write(buffer, 0, sizeof(ulong));

                stream.Seek(steamIdOffset2, SeekOrigin.Begin);
                stream.Write(buffer, 0, sizeof(ulong));

                var md5 = MD5.Create();

                stream.Seek(hashedRangeStart, SeekOrigin.Begin);

                buffer = new byte[hashedRangeLength];
                stream.Read(buffer, 0, hashedRangeLength);

                var hash = md5.ComputeHash(buffer);

                Console.WriteLine($"Writing newly computed hash: {string.Join(" ", hash.Select(x => x.ToString("x2")))}");

                stream.Seek(0x300, SeekOrigin.Begin);
                stream.Write(hash, 0, hash.Length);
            }
        }
    }
}
