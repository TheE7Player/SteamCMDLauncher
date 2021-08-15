using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SteamCMDLauncher.Component
{
    public class SteamCMD
    {

        private string launch_location;

        private string additional_arg = string.Empty;
        private string pre_arg = string.Empty;

        public SteamCMD(string exe_directory, bool steamCMD = true)
        {
            string location = steamCMD ? Path.Combine(exe_directory, "steamcmd.exe") : exe_directory;

            this.launch_location = location;

            if(!File.Exists(location))
            {
                throw new Exception($"SteamCMD location of '{location}' is invalid to find 'steamcmd.exe'");
            }
        }

        /// <summary>
        /// Needed if multiple games share the same ID
        /// </summary>
        /// <param name="arg">Argument to download the current version</param>
        public void AddArgument(string arg, string pre_arg = "")
        {
            additional_arg = arg;
            this.pre_arg = pre_arg;
        }

        public void Run()
        {
            Process process = new Process();
            process.StartInfo.FileName = this.launch_location;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.Arguments = $"{pre_arg} {additional_arg}";
            process.Start();

            process.WaitForExit();
        }

        /// <summary>
        /// Creates an update or verify window for the execution if supported
        /// </summary>
        /// <param name="id">The appid to verify or update</param>
        /// <param name="folder">The folder of where it will perform the actions</param>
        /// <param name="update">True to update and ignore verifying, False to verify files as well (as updating - takes longer)</param>
        public void Verify(int id, string folder, bool update = false)
        {
            Process process = new Process();
            process.StartInfo.FileName = this.launch_location;
            process.StartInfo.CreateNoWindow = false;
            process.StartInfo.UseShellExecute = false;

            string extra_tag = string.Empty;

            if (id >= 90 && id <= 99)
            {
                string game_name = Config.GetGameByAppId(id.ToString());
                switch (game_name)
                {
                    case "Counter-Strike: Condition Zero": extra_tag = "+app_set_config \"90 mod czero\""; break;
                    case "Day of Defeat": extra_tag = "+app_set_config \"90 mod dod\""; break;
                    case "Deathmatch Classic": extra_tag = "+app_set_config \"90 mod dmc\""; break;
                    case "Ricochet": extra_tag = "+app_set_config \"90 mod ricochet\""; break;
                    case "Team Fortress Classic": extra_tag = "	+app_set_config \"90 mod tfc\""; break;
                    case "Half-Life: Opposing Force": extra_tag = "+app_set_config \"90 mod gearbox\""; break;
                }
            }

            if(update)
                process.StartInfo.Arguments = $"+login anonymous +force_install_dir \"{folder}\" +app_update {id} {extra_tag} +exit".Trim();
            else
                process.StartInfo.Arguments = $"+login anonymous +force_install_dir \"{folder}\" +app_update {id} {extra_tag} validate +exit".Trim();

            process.Start();

            process.WaitForExit();
        }

        /// <summary>
        /// Runs the "steamcmd.exe" prior to installation, allows the exe to update itself if needs be
        /// </summary>
        /// <returns></returns>
        public void PreRun()
        {
            Process process = new Process();
            process.StartInfo.FileName = this.launch_location;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.Arguments = "+login anonymous +quit";
            process.Start();

            process.WaitForExit();
        }

        /// <summary>
        /// Installs the required game (id) to folder (location) - Callback feature is available.
        /// </summary>
        /// <param name="id">The game ID for steamcmd to download</param>
        /// <returns></returns>
        public void InstallGame(int id, string location)
        {
            Process process = new Process();
            process.StartInfo.FileName = this.launch_location;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;

            // ID correction for mutliple games with same ID
            if (id >= 90 && id <= 99)
            {
                id = 90;
            }

            process.StartInfo.Arguments = $"+login anonymous +force_install_dir \"{location}\" {additional_arg} +app_update {id} +quit";

            process.Start();

            process.WaitForExit();        
        }
    }
}
