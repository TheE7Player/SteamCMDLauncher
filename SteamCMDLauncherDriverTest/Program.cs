using System;
using SteamCMDLauncher.Component;

namespace SteamCMDLauncherDriverTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string target = @"C:\Users\james\Desktop\file_test.zip";

            if (!System.IO.File.Exists(target))
                CreateInstace(target);

            DoSave(target);

            DoFileFetch(target, ".metadata");

            DoFileSaveAmend(target, ".metadata", "example_folder>.new_file_data", "This has been written");

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

            file = null;
            target = null;
            new_file = null; 
            new_file_content = null;
        }
    }
}
