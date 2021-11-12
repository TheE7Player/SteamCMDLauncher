using System;
using SteamCMDLauncher.Component;
using System.IO;
using System.Linq;

namespace SteamCMDLauncherDriverTest
{
    class Program
    {
        static void Main(string[] args)
        {
            /*string target = $"C:\\Users\\james\\Desktop\\CSGO.zip";

            if (!System.IO.File.Exists(target))
                CreateInstace(target);

            DoSave(target);

            DoFileFetch(target, ".metadata");

            DoFileSaveAmend(target, ".metadata", "example_folder>.new_file_data", "This has been written");*/

            // Create the game config files

            //GenerateGameConfig("C:\\Users\\james\\Desktop\\TF2", "232250", "game_setting_232250");
        }

        static void GenerateGameConfig(string name, string appid, string target)
        {
            var eeee = Directory.GetFiles(@"C:\Users\james\source\repos\SteamCMDLauncher\SteamCMDLauncher\Resources")
                .Where(x => x.Contains(target))
                .ToArray();

            var ooo = new Archive($"{name}{Archive.DEFAULT_EXTENTION_CFG}", appid, true);

            // Amend the config first
            foreach (string file in eeee)
            {
                // This is the default setting file
                if(file.Contains($"{target}.json"))
                {
                    var a1 = ooo.SetFileContents("settings.json", File.ReadAllText(file));
                }
                else
                {
                    // This means it is a language file
                    string nm = Path.GetFileNameWithoutExtension(file);

                    // Get the lang type only
                    nm = nm[(target.Length + 1)..];

                    var a2 = ooo.SetFileContents($"lang>lang_{nm}.json", File.ReadAllText(file));
                }
            }

            ooo.SaveFile();

            ooo.Cleanup = true;
            ooo.ForceClear();
            ooo = null;

            name = null;
            appid = null;
            target = null;
        }

        static void CreateInstace(string target)
        {
            var aaa = new Archive(target, "740");

            System.IO.File.Delete(target);

            aaa.SaveFile();

            aaa.LoadFile();
        }
        
        static void DoSave(string target)
        {
            var aaa = new Archive(target, "740", true);
            
            aaa.SaveFile();
        }
    
        static void DoFileFetch(string target, string file)
        {
            var aaa = new Archive(target, "740", true);

            var result = aaa.GetFileContents(file);

            Console.WriteLine("Result Number: {0}, Result Return: {1}", result.Item1, result.Item2);

            file = null;
            target = null;
        }

        static void DoFileSaveAmend(string target, string file, string new_file, string new_file_content)
        {
            var aaa = new Archive(target, "740", true);

            var result = aaa.GetFileContents(file);

            var result2 = aaa.SetFileContents(new_file, new_file_content);

            Console.WriteLine(result2.Item1 ? "Successful Write" : "Failed Write");

            aaa.SaveFile();

            aaa.Cleanup = true;
            aaa.ForceClear();

            file = null;
            target = null;
            new_file = null; 
            new_file_content = null;
        }
    }
}
