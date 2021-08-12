using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Text;
using Newtonsoft.Json;
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

        private Dictionary<string, string> language;
        private Dictionary<string, Dictionary<string, Dictionary<string, string>>> controls;
        private UIComponents.GameSettingControl[] componenets;

        public delegate void view_hint_dialog(string hint);
        public event view_hint_dialog View_Dialog;

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

            Config.Log("[GSM] Parsing Data...");

            string fixed_name_tab;
            string fixed_control_name;
            foreach (var tab in cont.Properties())
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
                    }
                }
            }
            Config.Log("[GSM] Handling Complete");
            lFile = null;
        }

        public GameSettingManager(string appid)
        {
            var resource = Path.Combine(Directory.GetCurrentDirectory(), "Resources");

            if (!Directory.Exists(resource))
            {
                Config.Log($"[GSM] Resource folder was not found - Got '{resource}'");
                Supported = false; ResourceFolderFound = false; return;
            }

            ResourceFolderFound = true;

            // Get all the files in the current directory
            var files = Directory.GetFiles(resource);

            Supported = files.Any(x => x.EndsWith($"game_setting_{appid}.json"));

            //TODO: Go language setting validation here
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

        private void PassDialog(string hint)
        {
            View_Dialog.Invoke(hint);
        }

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

            var compList = new List<UIComponents.GameSettingControl>();

            // Now get the elemements
            foreach (var tab in controls)
            {
                // Add the tab category
                currentTab.Header = tab.Key;

                // Now we'll add the controls onto here
                foreach (var ctrl in tab.Value)
                {
                    Config.Log($"[GSM] Rendering {ctrl.Key}");

                    ctrl_apnd = new UIComponents.GameSettingControl(ctrl.Value);

                    ctrl_apnd.View_Dialog += PassDialog;

                    ctrl_output = ctrl_apnd.GetComponent();

                    if (ctrl_output != null)
                    { 
                        grid.Children.Add(ctrl_output);
                        compList.Add(ctrl_apnd.DeepClone());
                    }
                }

                // Then assign it
                currentTab.Content = grid;

                returnControl.Items.Add(currentTab);
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
            var sb = new StringBuilder();

            foreach (var item in componenets)
            {
                sb.Append($"{item.GetArg()} ");
            }

            return sb.ToString();
        }
    }
}