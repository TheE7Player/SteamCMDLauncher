using System;
using SteamCMDLauncher.Component;

namespace SteamCMDLauncherDriverTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string target = @"C:\Users\james\Desktop\file_test.zip";

            DoSave(target);

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
    }
}
