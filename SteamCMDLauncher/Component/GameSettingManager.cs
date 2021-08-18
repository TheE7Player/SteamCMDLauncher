using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Windows.Controls;

namespace SteamCMDLauncher.Component
{
    public class GameSettingManager
    {
        public bool Supported { get; private set; }
        public bool ResourceFolderFound { get; private set; }
        public bool LanguageSupported { get; private set; }

        private string targetExecutable;
        private string targetDictionary;
        private string PreArguments;

        private Dictionary<string, string> language;
        private Dictionary<string, Dictionary<string, Dictionary<string, string>>> controls;
        private UIComponents.GameSettingControl[] componenets;

        public delegate void view_hint_dialog(string hint);
        public event view_hint_dialog View_Dialog;

        public string GetExePath => Path.Combine(targetDictionary, targetExecutable);

        /// <summary>
        /// Gets any additional arguments to append to the start if given by the json file
        /// </summary>
        public string GetPreArg => PreArguments;

        private void SetLanguage(string path)
        {
            string lFile;

            // Remove any comments if any
            lFile = String.Join(null, File.ReadAllLines(path)
                .Where(x => !x.Contains("//"))
                .ToArray());

            language = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(lFile);

            lFile = null;
        }

        private void SetControls(string path)
        {
            Config.Log("[GSM] Got JSON to read properties from...");

            string lFile;

            // Remove any comments if any
            lFile = String.Join(null, File.ReadAllLines(path)
                .Where(x => !x.Contains("//"))
                .ToArray());

            var cont = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(lFile);

            controls = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
      
            if(cont.ContainsKey("setup"))
            {
                Config.Log("[GSM] Parsing Setup Data...");

                JToken target_kv = cont["setup"]["target"];

                if (target_kv == null)
                {
                    Config.Log("[GSM] No existing \"target\" key found in json - this is needed to run the server application!");
                    Supported = false; return;
                }

                targetExecutable = target_kv.ToString();

                target_kv = null;

            } 
            else
            {
                Config.Log("[GSM] No existing \"setup\" key found in json - this is needed to run the server application!");
                Supported = false; return;
            }

            //PreArguments
            if(cont.ContainsKey("setup"))
            {
                Config.Log("[GSM] Fetching pre-commands set by game config json...");

                JToken target_kv = cont["setup"]["precommands"];

                if (target_kv == null)
                {
                    Config.Log("[GSM] No existing \"precommands\" key found in json - Ignoring, this may cause lanuch issues!");
                    Supported = false; return;
                }

                PreArguments = target_kv.ToString();

                target_kv = null;
            }

            Config.Log("[GSM] Parsing Data...");

            string fixed_name_tab;
            string fixed_control_name;

            // [NEW] As non-hashtag now exists "setup", best to filter prior to iteration
            IEnumerable<JProperty> iterable_fields = cont.Properties().Where(x => x.Name[0] == '#');

            string carry_dir = "carry-dir";

            foreach (var tab in iterable_fields)
            {
                Config.Log($"[GSM] Hanlding {tab.Name}");

                fixed_name_tab = GetLangRef(tab.Name);

                controls.Add(fixed_name_tab, new Dictionary<string, Dictionary<string, string>>());

                foreach (var ctrl in tab.Children<JObject>().Properties())
                {
                    fixed_control_name = GetLangRef(ctrl.Name);
                    controls[fixed_name_tab].Add(fixed_control_name, new Dictionary<string, string>());
                    foreach (var attr in ctrl.Children<JObject>().Properties())
                    {
                        controls[fixed_name_tab][fixed_control_name].Add(GetLangRef(attr.Name), GetLangRef(attr.Value.ToString()));
                    
                        // If "carry_dir" key is present, pass in the root folder as value
                        if(attr.Name == carry_dir)
                        {
                            controls[fixed_name_tab][fixed_control_name].Add("dir", targetDictionary);
                        }
                    }
                }
            }
            Config.Log("[GSM] Handling Complete");
            lFile = null; carry_dir = null;
        }

        /// <summary>
        /// Sets up the folder on where it will executable and what settings to fetch
        /// </summary>
        /// <param name="appid">The id to get the correct .json to read the settings from</param>
        /// <param name="folderLocation">The folder will the directory location to run the server from</param>
        public GameSettingManager(string appid, string folderLocation)
        {
            var resource = Path.Combine(Directory.GetCurrentDirectory(), "Resources");

            if (!Directory.Exists(resource))
            {
                Config.Log($"[GSM] Resource folder was not found - Got '{resource}'");
                Supported = false; ResourceFolderFound = false; return;
            }

            ResourceFolderFound = true;

            // Store where the server folder is at root
            if(!Directory.Exists(folderLocation))
            {
                Config.Log($"[GSM] Server Root Folder is invalid or missing - Got '{folderLocation}'");
                Supported = false; return;
            }
            targetDictionary = folderLocation;
            
            // Get all the files in the current directory
            var files = Directory.GetFiles(resource);

            Supported = files.Any(x => x.EndsWith($"game_setting_{appid}.json"));

            //TODO: Go language setting validation here (based on windows running language)
            LanguageSupported = files.Any(x => x.EndsWith($"game_setting_{appid}_en.json"));

            string langFile;

            if (LanguageSupported)
            {
                langFile = files.First(x => x.EndsWith($"game_setting_{appid}_en.json"));
                SetLanguage(langFile);
            }

            if (Supported)
            {
                langFile = files.First(x => x.EndsWith($"game_setting_{appid}.json"));

                SetControls(langFile);
            }

            langFile = null;
        }

        /// <summary>
        /// Gets the current computer languages version of the referenced string
        /// </summary>
        /// <param name="ref_t">The reference string (#) to translate</param>
        /// <returns>Returns its translated self if exists, else it returns its natural unedited form</returns>
        private string GetLangRef(string ref_t) => (language.ContainsKey(ref_t)) ? language[ref_t] : ref_t;

        private void PassDialog(string hint) => View_Dialog.Invoke(hint);

        public TabControl GetControls()
        {
            Config.Log("[GSM] Rendering the components to main view...");

            // Create the control
            var returnControl = new TabControl();

            TabItem currentTab = new TabItem();

            StackPanel grid = new StackPanel();

            UIComponents.GameSettingControl ctrl_apnd;
            System.Windows.UIElement ctrl_output;

            grid.Margin = new System.Windows.Thickness(5, 10, 5, 5);

            List<UIComponents.GameSettingControl> compList = new List<UIComponents.GameSettingControl>();

            // Now get the elemements
            foreach (var tab in controls)
            {
                // Add the tab category
                currentTab.Header = tab.Key;

                // Now we'll add the controls onto here
                foreach (var ctrl in tab.Value)
                {
                    Config.Log($"[GSM] Rendering {ctrl.Key}");

                    ctrl_apnd = new UIComponents.GameSettingControl(ctrl.Key, ctrl.Value);

                    ctrl_apnd.View_Dialog += PassDialog;

                    ctrl_output = ctrl_apnd.GetComponent();

                    if (ctrl_output != null)
                    { 
                        grid.Children.Add(ctrl_output);
                        compList.Add(ctrl_apnd.DeepClone());
                    }
                }

                // Then assign it
                currentTab.Content = new ScrollViewer()
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    Content = grid
                };

                returnControl.Items.Add(currentTab);

                // Reset for the next one if any
                currentTab = new TabItem();
                grid = new StackPanel();
            }

            Config.Log("[GSM] Rendering complete");
            controls = null; ctrl_apnd = null; ctrl_apnd = null;

            // Store reference it
            componenets = compList.ToArray();
            compList.Clear(); compList = null;

            return returnControl;
        }

        public string GetRunArgs()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var item in componenets) { sb.Append($"{item.GetArg()} "); }

            return sb.ToString();
        }

        public string[] RequiredFields()
        {
            List<string> r = new List<string>();

            foreach (UIComponents.GameSettingControl comp in componenets)
            {
                if (!comp.canBeBlank)
                {
                    if (comp.IsEmpty())
                        r.Add(comp.blank_error);
                }
            }

            return r.ToArray();
        }

        public string[] GetSafeConfig()
        {
            List<string> output = new List<string>();

            output.Add("# Config - Please do not modify anyone (including the hash values) as it may corrupt it on load");
            output.Add("# Only values that contain a default or added value will be amended");
            
            string safe_value = string.Empty;

            foreach (var ctrl in componenets)
            {
                safe_value = ctrl.SaveValue();
                
                if (string.IsNullOrEmpty(safe_value)) continue;
                
                output.Add($"{ctrl.name}={safe_value}");
            }

            return output.ToArray();
        }
    
        public void SetConfigFiles(string[] contents)
        {
            IEnumerable<string[]> iteration = contents
                .Where(x => !x.StartsWith('#')) // Remove any lines with comments
                .Select(x => x.Split(new[] { '=' }, 2));

            foreach (string[] ctrl in iteration)
            {
                UIComponents.GameSettingControl elem = componenets.FirstOrDefault(x => x.name == ctrl[0]);

                if(elem is null)
                {
                    Config.Log($"[CFG] Couldn't find control called '{ctrl[0]}' with value '{ctrl[1]}' - Double check this exists or wrong/old file!");
                    continue;
                }

                Config.Log($"[CFG] Loading value for {elem.name}");
                
                elem.LoadValue(ctrl[1]);
            }

            Config.Log("[CFG] Loading has finished");
        }
    }
}