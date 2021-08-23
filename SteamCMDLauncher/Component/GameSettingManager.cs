using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Windows.Controls;
using System.Globalization;

namespace SteamCMDLauncher.Component
{
    public class GameSettingManager
    {
        #region Properties/Variables
        /// <summary>
        /// If the current game its processing its supported
        /// </summary>
        public bool Supported { get; private set; }
        
        /// <summary>
        /// If the '/resources/' folder is located near the exe path of execution
        /// </summary>
        public bool ResourceFolderFound { get; private set; }
        
        /// <summary>
        /// If the game config file supports the language of the user running the program
        /// </summary>
        public bool LanguageSupported { get; private set; }

        private string targetExecutable, targetDictionary, PreArguments;

        private Dictionary<string, string> language;
        
        private Dictionary<string, Dictionary<string, Dictionary<string, string>>> controls;
        
        private UIComponents.GameSettingControl[] componenets;

        /// <summary>
        /// Get the exe path to launch the games dedicated server
        /// </summary>
        public string GetExePath => Path.Combine(targetDictionary, targetExecutable);

        /// <summary>
        /// Gets any additional arguments to append to the start if given by the json file
        /// </summary>
        public string GetPreArg => PreArguments;
        
        #endregion

        /// <summary>
        /// Sets up the folder on where it will executable and what settings to fetch
        /// </summary>
        /// <param name="appid">The id to get the correct .json to read the settings from</param>
        /// <param name="folderLocation">The folder will the directory location to run the server from</param>
        public GameSettingManager(string appid, string folderLocation)
        {
            string lang = GetLanguageType();

            string resource = Path.Combine(Directory.GetCurrentDirectory(), "Resources");

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
            string[] files = Directory.GetFiles(resource);
            string langFile, gameFile = string.Empty;

            string gameJson = $"game_setting_{appid}.json";
            string langJson = $"game_setting_{appid}_{lang}.json";

            gameFile = files.FirstOrDefault(x => x.EndsWith(gameJson));
            langFile = files.FirstOrDefault(x => x.EndsWith(langJson));

            Supported = !string.IsNullOrWhiteSpace(gameFile);
            LanguageSupported = !string.IsNullOrWhiteSpace(langFile);

            if (LanguageSupported)
            {
                SetLanguage(langFile);
            }

            if (Supported)
            {
                SetControls(gameFile);
            }

            langFile = null; gameFile = null;
            files = null; resource = null;
            gameJson = null; langJson = null;
            lang = null;
        }
        
        /// <summary>
        /// Gets the current computer languages version of the referenced string
        /// </summary>
        /// <param name="ref_t">The reference string (#) to translate</param>
        /// <returns>Returns its translated self if exists, else it returns its natural unedited form</returns>
        private string GetLangRef(string ref_t) => (language.ContainsKey(ref_t)) ? language[ref_t] : ref_t;

        private string GetLanguageType()
        {
            // Get the current systems language
            CultureInfo currentLang = CultureInfo.InstalledUICulture;
            
            // Get the 2 letters of that language selected
            return currentLang.TwoLetterISOLanguageName;
        }

        #region Control Related
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
            
            // Remove any comments if any
            string lFile = String.Join(null, File.ReadAllLines(path)
            .Where(x => !x.Contains("//"))
            .ToArray());

            JObject cont = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(lFile);

            controls = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();
            
            lFile = null;

            JToken target_kv;

            if (cont.ContainsKey("setup"))
            {
                Config.Log("[GSM] Parsing Setup Data...");

                target_kv = cont["setup"]["target"];

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

                target_kv = cont["setup"]["precommands"];

                if (target_kv == null)
                {
                    Config.Log("[GSM] No existing \"precommands\" key found in json - Ignoring, this may cause launch issues!");
                    Supported = false; return;
                }

                PreArguments = target_kv.ToString();

                target_kv = null;
            }

            Config.Log("[GSM] Parsing Data...");

            string fixed_name_tab;
            string fixed_control_name;
            string carry_dir = "carry-dir";

            // [NEW] As non-hashtag now exists "setup", best to filter prior to iteration
            IEnumerator<JProperty> iterable_fields = cont.Properties().Where(x => x.Name[0] == '#').GetEnumerator();

            IEnumerator<JProperty> tab_control_parent = null;
            IEnumerator<JProperty> tab_control_children = null;
            JProperty tab = null;
            JArray to_fix = null;

            while(iterable_fields.MoveNext())
            {
                // Get the property instance (control to created)
                tab = iterable_fields.Current;
                
                Config.Log($"[GSM] Handling {tab.Name}");

                // Get the translated text version of the control tab
                fixed_name_tab = GetLangRef(tab.Name);
                
                // Add the control to the dictionary, and initialize the next information from the control attributes 
                controls.Add(fixed_name_tab, new Dictionary<string, Dictionary<string, string>>());

                // Now we get all the controls, this will be the parent of the children               
                tab_control_parent = tab.Children<JObject>().Properties().GetEnumerator();

                while(tab_control_parent.MoveNext())
                {
                    // Get the components label in the users language (if support)
                    fixed_control_name = GetLangRef(tab_control_parent.Current.Name);

                    // Assign this to the control
                    controls[fixed_name_tab].Add(fixed_control_name, new Dictionary<string, string>());

                    // If the control type is just an array, assign the value and move on
                    if (tab_control_parent.Current.Value.GetType() == typeof(JArray))
                    {
                        // Get the array object and cast it to an 'JArray' object
                        to_fix = (JArray)tab_control_parent.Current.Value;

                        // Add to the control value
                        controls[fixed_name_tab][fixed_control_name].Add(string.Empty, string.Join('|', to_fix));
                    }

                    // TODO: Should continue 'if' array is assigned?

                    // Now we get the controls individual attributes
                    tab_control_children = tab_control_parent.Current.Children<JObject>().Properties().GetEnumerator();

                    while (tab_control_children.MoveNext())
                    {
                        controls[fixed_name_tab][fixed_control_name].Add(
                            GetLangRef(tab_control_children.Current.Name), 
                            GetLangRef(tab_control_children.Current.Value.ToString()));

                        // If "carry_dir" key is present, pass in the root folder as value
                        if(tab_control_children.Current.Name == carry_dir)
                        {
                            controls[fixed_name_tab][fixed_control_name].Add("dir", targetDictionary);
                        }
                    }
                }

                tab_control_children.Dispose();
                tab_control_parent.Dispose();
            }

            Config.Log("[GSM] Handling Complete");
            tab = null;
            tab_control_parent = null;
            tab_control_children = null;
            to_fix = null;

            cont = null; lFile = null; carry_dir = null; iterable_fields = null; 
        }

        public TabControl GetControls()
        {
            // Create the control
            TabControl returnControl = new TabControl();

            TabItem currentTab = new TabItem();

            StackPanel grid = new StackPanel();

            UIComponents.GameSettingControl ctrl_apnd;
            System.Windows.UIElement ctrl_output;

            grid.Margin = new System.Windows.Thickness(5, 10, 5, 5);

            List<UIComponents.GameSettingControl> compList = new List<UIComponents.GameSettingControl>();

            // Now get the elements
            foreach (var tab in controls)
            {
                // Add the tab category
                currentTab.Header = tab.Key;

                if(tab.Value.ContainsKey("validate-folder"))
                {
                    // Safely (Try-less catch) the files to validate
                    if(!tab.Value["validate-folder"].TryGetValue(string.Empty, out string rule_set))
                    {
                        throw new Exception($"Broken 'validate-folder' rule from tab '{tab.Key}'");
                    }

                    // Set to false by default
                    bool found = false;

                    // Validate the files and folder
                    foreach (string item in rule_set.Split('|'))
                    {
                        found = ValidateIO(item);

                        if (found)
                        {
                            Config.Log($"[GSM] Found required file to enable {currentTab.Header} tab");
                            break;
                        }
                    }

                    if(!found)
                    {
                        Config.Log($"[GSM] Not found required file to enable {currentTab.Header} tab - disabling it");
                        currentTab.IsEnabled = false;
                    }
                }

                // Now we'll add the controls onto here
                foreach (var ctrl in tab.Value)
                {
                    Config.Log($"[GSM] Rendering {ctrl.Key}");

                    ctrl_apnd = new UIComponents.GameSettingControl(ctrl.Key, ctrl.Value);

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
            controls = null; 
            ctrl_apnd = null; 
            ctrl_apnd = null;

            // Store reference it
            componenets = compList.ToArray();
            compList = null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            return returnControl;
        }
        #endregion

        #region Config Related
        private bool ValidateIO(string validate)
        {
            bool isFolder = !validate.Contains('.');

            string entity = string.Concat(targetDictionary, validate);

            return (isFolder) ? Directory.Exists(entity) : File.Exists(entity);
        }
        
        public string GetRunArgs()
        {
            StringBuilder sb = new StringBuilder();

            char space = ' ';

            foreach (var item in componenets) {

                if(item.GetStringType() == "gsinput")
                {
                    UIComponents.GameSettingComponent.GSInput instance = item.GetControlAsInstance<UIComponents.GameSettingComponent.GSInput>();
                    
                    if(instance.WriteFilePath?.Length > 0)
                        sb.Append(item.GetArg(targetDictionary));
                    else
                        sb.Append(item.GetArg());
                }
                else 
                { 
                    sb.Append(item.GetArg());
                }

                sb.Append(space);
            }

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

        public string[] GetSafeConfig(Action OnComplete = null)
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

            if (OnComplete != null) OnComplete();

            return output.ToArray();
        }
    
        public void SetConfigFiles(string[] contents, Action OnComplete = null)
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
            
            if (OnComplete != null) OnComplete();
        }
        #endregion

        // Ensures that all objects created gets destroyed
        public void Destory()
        {

            // Clear all possible strings
            targetExecutable = null; 
            targetDictionary = null; 
            PreArguments = null;

            // Clear the component array
            componenets = null;

            // Clear the language dictionary
            language = null;
            
        }
    }
}