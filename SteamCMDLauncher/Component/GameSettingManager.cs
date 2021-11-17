using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Controls;

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

        /// <summary>
        /// If the current loaded config (either language or file) is unedited from release build
        /// </summary>
        public bool ConfigOffical { get; private set; }
        
        /// <summary>
        /// If at any point during initialization stage the required embedded file failed
        /// </summary>
        public bool BrokenEmbededResource { get; private set; }

        /// <summary>
        /// If it detected multiple variations of the same supported type
        /// </summary>
        public bool MultipleConfigurationsFound => Available_Configurations?.Count > 1;

        private List<string> Available_Configurations { get; set; }

        public bool BrokenCFGFile { get; private set; }
        public string BrokenCFGFileReason { get; private set; }

        private string _appid = string.Empty;

        private string targetExecutable, targetDictionary, PreArguments, JoinConnectString;

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
            _appid = appid;
            
            string lang = GetLanguageType();
            string resource = Path.Combine(Directory.GetCurrentDirectory(), "Resources");
            Archive arch = null;
            string[] files = null;

            if (Available_Configurations is null)
                Available_Configurations = new List<string>(5);

            try
            {
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
                files = Directory.GetFiles(resource, $"*{Archive.DEFAULT_EXTENTION_CFG}");

                int fLength = files.Length;

                for (int i = 0; i < fLength; i++)
                {
                    arch = new Archive(files[i]);

                    if (arch.GetArchiveDetails.GameID == _appid)
                    {
                        Available_Configurations.Add(files[i]);
                    }
                }
            }
            finally
            {
                resource = null;
                lang = null;
                arch = null;
                files = null;
            }
        }

        /// <summary>
        /// Starts the configuration building process
        /// </summary>
        /// <param name="host"></param>
        public void DoWork(ref UIComponents.DialogHostContent host)
        {
            string resource = Path.Combine(Directory.GetCurrentDirectory(), "Resources"),
            lang = GetLanguageType(), langFile = null, gameFile = null,
            cfgHash = null, correct_file = null, langJson = null;
            string[] contents = null;
            
            Archive arch = null;
            StringComparer comparer = null;
            
            try
            {

                if (Available_Configurations.Count == 1) { correct_file = Available_Configurations[0]; }
                else
                {
                    if (Available_Configurations.Count < 1)
                    {
                        Supported = false;
                        return;
                    }

                    System.Threading.Tasks.ValueTask<string> eee = host.ShowComponentCombo("Multiple Configurations Found",
                    $"There are {Available_Configurations.Count} configurations found for this game.\nPlease select the one you want to use:",
                    Available_Configurations.Select(x => Path.GetFileNameWithoutExtension(x)).ToArray());

                    int idx = Available_Configurations.FindIndex(x => x.Contains(eee.Result));

                    if (idx < 0 || idx > Available_Configurations.Count)
                        throw new Exception($"Unexpected Index return from GameSettingManager Constructor: Got {idx} (Max is: {Available_Configurations.Count})");

                    correct_file = Available_Configurations[idx];

                    Available_Configurations = null;
                }

                //string gameJson = $"game_setting_{appid}.json";
                langJson = $"lang/lang_{lang}.json";

                Supported = !string.IsNullOrWhiteSpace(correct_file);
                LanguageSupported = false;

                arch = new Archive(correct_file);

                if (Supported)
                {
                    // T1: File name, T2: Contents
                    foreach ((string, string) item in arch.GetFiles())
                    {
                        if (item.Item1 == "settings.json")
                        {
                            gameFile = item.Item2; continue;
                        }

                        if (item.Item1 == langJson)
                        {
                            langFile = item.Item2;
                            LanguageSupported = true; break;
                        }
                    }
                }

                //LanguageSupported = !string.IsNullOrWhiteSpace(langFile);

                // Set config to true by default
                ConfigOffical = true;

                if (LanguageSupported)
                {
                    if (!SetLanguageByContent(langFile)) { Supported = false; return; }
                }

                if (Supported)
                {
                    if (!SetControls(gameFile)) { Supported = false; return; }
                }

                Config.Log("[GSM] Validating if the config file is unaltered");

                if (Config.GetEmbededResource("res_hash.txt", out contents))
                {
                    contents = contents.Where(x => !x.StartsWith("#")).ToArray();
                    BrokenEmbededResource = false;
                }
                else
                {
                    BrokenEmbededResource = true;
                    return;
                }

                cfgHash = Config.GetSHA256Sum(correct_file);

                // Now we compare
                comparer = StringComparer.OrdinalIgnoreCase;

                if (!contents.Any(x => x.Contains(cfgHash)))
                {
                    Config.Log("[GSM-SHA] The config loaded isn't offical, showing UI warning to user.");
                    ConfigOffical = false;
                }
            }
            finally
            {
                comparer = null; cfgHash = null; arch = null;
                contents = null; langFile = null; gameFile = null;
                langJson = null; resource = null;
                lang = null;
            }
        }
        
        /// <summary>
        /// Gets the current computer languages version of the referenced string
        /// </summary>
        /// <param name="ref_t">The reference string (#) to translate</param>
        /// <returns>Returns its translated self if exists, else it returns its natural unedited form</returns>
        private string GetLangRef(string ref_t) => language.ContainsKey(ref_t) ? language[ref_t] : ref_t;

        private string GetLanguageType()
        {
            // Get the current systems language
            CultureInfo currentLang = CultureInfo.InstalledUICulture;
            
            // Get the 2 letters of that language selected
            return currentLang.TwoLetterISOLanguageName;
        }

        #region Control Related
        private bool SetLanguage(string path)
        {
            string lFile;

            // Remove any comments if any
            lFile = string.Join(null, File.ReadAllLines(path)
                .Where(x => !x.Contains("//"))
                .ToArray());

            language = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(lFile);

            lFile = null;

            return !ReferenceEquals(language, null);
        }

        private bool SetLanguageByContent(string content)
        {
            language = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(content);

            return !ReferenceEquals(language, null);
        }

        private bool SetControls(string path)
        {
            Config.Log("[GSM] Got JSON to read properties from...");

            if (string.IsNullOrEmpty(path))
            {
                Config.Log("[GSM] [!] Argument given for SetControls were empty - should be the case! [!]");
                return false;
            }

            // Remove any comments if any
            JObject cont = (JObject)Newtonsoft.Json.JsonConvert.DeserializeObject(File.Exists(path) ? string.Join(null, File.ReadAllLines(path)
                .Where(x => !x.Contains("//"))
                .ToArray()) : path);

            if (cont is null)
            {
                Config.Log("[GSM] Loaded Control Config is empty (null) - Ensure it has data in it!");
                return false;
            }

            controls = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

            JToken target_kv;

            if (cont.ContainsKey("setup"))
            {
                Config.Log("[GSM] Parsing Setup Data...");

                target_kv = cont["setup"]["target"];

                if (target_kv == null)
                {                   
                    Config.Log("[GSM] No existing \"target\" key found in json - this is needed to run the server application!");
                    Supported = false; return false;
                }

                targetExecutable = target_kv.ToString();

                target_kv = null;

            } 
            else
            {
                BrokenCFGFile = true;
                BrokenCFGFileReason = "Configuration was compiled without a setup key assigned, this is an issue\ncaused by ConfigBuilder or Saving Issue";
                Config.Log("[GSM] No existing \"setup\" key found in json - this is needed to run the server application!");
                Supported = false; return false;
            }

            //PreArguments
            if(cont.ContainsKey("setup"))
            {
                Config.Log("[GSM] Fetching pre-commands set by game config json...");

                target_kv = cont["setup"]["precommands"];

                if (target_kv == null)
                {
                    Config.Log("[GSM] No existing \"precommands\" key found in json - Ignoring, this may cause launch issues!");
                }
                else
                { 
                    PreArguments = target_kv.ToString();

                    // Check if the 'PreArguments' contains an IPv4 tag ($IP4)
                    string ipCondition = "$IP4";
                    if(PreArguments.Contains(ipCondition))
                    {
                        string ip_local = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                            .AddressList
                            .FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            .ToString();

                        if(string.IsNullOrEmpty(ip_local))
                        {
                            Config.Log($"[GSM] IPv4 address was invalid or is wrong, result was: '{ip_local}'");
                        } 
                        else
                        {
                            Config.Log("[GSM] IPv4 address was found and is being used as the server ip root");
                            PreArguments = PreArguments.Replace(ipCondition, ip_local);
                        }

                        ip_local = null;
                    }
                    ipCondition = null;
                }

                target_kv = cont["setup"]["server_join_command"];

                if (target_kv == null)
                {
                    Config.Log("[GSM] No existing \"server_join_command\" key found in json - Ignoring, this may cause launch issues!");
                }
                else
                {
                    try
                    {
                        JoinConnectString = cont["setup"]["server_join_command"].ToString();
                    }
                    catch(Exception ex)
                    {
                        Config.Log("[GSM] JoinConnectString threw error, bellow states why. This may make it hard to tell user the ip or password!");
                        Config.Log($"[GSM] Couldn't assign property due to: {ex.Message}");
                    }
                }
                
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

            cont = null; carry_dir = null; iterable_fields = null;
            return true;
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

            validate = null;

            return isFolder ? Directory.Exists(entity) : File.Exists(entity);
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
            string[] output;

            foreach (UIComponents.GameSettingControl comp in componenets)
            {
                if (!comp.canBeBlank)
                {
                    if (comp.IsEmpty())
                        r.Add(comp.blank_error);
                }
            }

            output = r.ToArray();
            r = null;

            return output;
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
    
        public void SetConfigFiles(string[] contents, Action OnComplete = null, Action OnFail = null)
        {
            IEnumerable<string[]> iteration = contents
                .Where(x => !x.StartsWith('#')) // Remove any lines with comments
                .Select(x => x.Split(new[] { '=' }, 2));

            // Do a check-run if any controls here aren't available to the current game selected/viewed
            if(iteration.Any(x => !componenets.Any(y => y.name == x[0]) ))
            {
                Config.Log("[CFG] Loaded config wasn't suited for current game settings");
                if (OnFail != null) OnFail();
            }
            else
            { 
                foreach (string[] ctrl in iteration)
                {
                    UIComponents.GameSettingControl elem = componenets.FirstOrDefault(x => x.name == ctrl[0]);

                    if (elem is null)
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

            iteration = null;
        }

        public string GetConnectCommand()
        {
            if (string.IsNullOrEmpty(JoinConnectString)) return string.Empty;

            string ip_local = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName())
                        .AddressList
                        .FirstOrDefault(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .ToString();

            UIComponents.GameSettingControl port_t = componenets.FirstOrDefault(x => x.tag == "PORT");
            UIComponents.GameSettingControl pass_t = componenets.FirstOrDefault(x => x.tag == "PASS");

            // Then replace the command to get the output string
            string port = null, pass = null;
            
            // We can just get the value but performing a substring based on the 'command' attribute
            if(port_t != null)
                port = port_t.GetArg()[(port_t.Command.Length - 1)..];

            if(pass_t != null)
                pass = pass_t.GetArg()[(pass_t.Command.Length - 1)..];

            string ip_str = string.Concat(ip_local, port != null ? $":{port}" : string.Empty);

            string output = JoinConnectString.Replace("$IP", ip_str);

            output = output.Replace("$P", pass);
            
            ip_str = null;
            pass = null;
            port = null;
            ip_local = null;
            pass_t = null; port_t = null;

            return output;
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