using System;
using SteamCMDLauncher.Component;

namespace SteamCMDLauncherDriverTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string target = @"C:\Users\james\Desktop\file_test.zip";

            var aaa = new Archive("A", "740");

            System.IO.File.Delete(target);

            aaa.SaveFile(target);

            var result = aaa.LoadFile(target);

        }
    }
}
