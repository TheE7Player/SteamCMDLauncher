﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Text;
using Newtonsoft.Json;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;

namespace SteamCMDLauncher.Component
{
    public class GameSettingManager
    {
        public bool Supported { get; private set; }
        public bool ResourceFolderFound { get; private set; }
        public bool LanguageSupported { get; private set; }

        private Dictionary<string, string> language;
        private Dictionary<string, Dictionary<string, Dictionary<string, string>>> controls;

        public GameSettingManager(string appid)
        {
            var resource = Path.Combine(Directory.GetCurrentDirectory(), "Resources");

            if(!Directory.Exists(resource))
            {
                Config.Log($"[GSM] Resource folder was not found - Got '{resource}'");
                Supported = false; ResourceFolderFound = false; return;
            }

            ResourceFolderFound = true;
            //game_setting_
            
            // Get all the files in the current directory
            var files = Directory.GetFiles(resource);

            Supported = files.Any(x => x.EndsWith($"game_setting_{appid}.json"));
            
            //TODO: Go language setting validation here
            LanguageSupported = files.Any(x => x.EndsWith($"game_setting_{appid}_en.json"));
            
            string langFile, lFile;

            if (LanguageSupported)
            {
                langFile = files.First(x => x.EndsWith($"game_setting_{appid}_en.json"));
                
                // Remove any comments if any
                lFile = String.Join(null, File.ReadAllLines(langFile)
                    .Where(x => !x.Contains("//"))
                    .ToArray());

                language = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string,string>>(lFile);
                
                lFile = null; langFile = null;
            }

            if(Supported)
            {
                langFile = files.First(x => x.EndsWith($"game_setting_{appid}.json"));

                // Remove any comments if any
                lFile = String.Join(null, File.ReadAllLines(langFile)
                    .Where(x => !x.Contains("//"))
                    .ToArray());

                var cont = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(lFile);

                controls = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

                string fixed_name_tab;
                string fixed_control_name;
                foreach (var tab in cont.Properties())
                {
                    fixed_name_tab = GetLangRef(tab.Name);

                    controls.Add(fixed_name_tab, new Dictionary<string, Dictionary<string, string>>());

                    foreach (var ctrl in tab.Children<JObject>().Properties())
                    {
                        fixed_control_name = GetLangRef(ctrl.Name);
                        controls[fixed_name_tab].Add(fixed_control_name, new Dictionary<string, string>());
                        foreach (var attr in ctrl.Children<JObject>().Properties())
                        {
                            controls[fixed_name_tab][fixed_control_name].Add(GetLangRef(attr.Name), GetLangRef(attr.Value.ToString()));
                        }
                    }
                }

                lFile = null; langFile = null;
            }
        }   
    
        /// <summary>
        /// Gets the current computer languages version of the referenced string
        /// </summary>
        /// <param name="ref_t">The reference string (#) to translate</param>
        /// <returns>Returns its translated self if exists, else it returns its natural unedited form</returns>
        private string GetLangRef(string ref_t) => (language.ContainsKey(ref_t)) ? language[ref_t] : ref_t;
    }
}
