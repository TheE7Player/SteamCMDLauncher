using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;

namespace SteamCMDLauncher
{
    public static class Extension
    {
        /// <summary>
        /// Updates the (server) card to reflect if the directory still exists without creating a new card
        /// </summary>
        /// <param name="card">The card to update/reflect</param>
        public static void UpdateCard(this MaterialDesignThemes.Wpf.Card card)
        {
            StackPanel content = card.Content as StackPanel;
            
            string current_location = card.Tag.ToString();
            bool current_folder = System.IO.Directory.Exists(current_location);

            MaterialDesignThemes.Wpf.PackIcon icon = (MaterialDesignThemes.Wpf.PackIcon)content.Children[3];
            ((ToolTip)icon.ToolTip).Content = (current_folder) ? "Folder still exists" : $"Couldn't find '{current_location}'";

            icon.Kind = (current_folder) ?
                MaterialDesignThemes.Wpf.PackIconKind.Link :
                MaterialDesignThemes.Wpf.PackIconKind.LinkVariantOff;
        }

        /// <summary>
        /// Compares if two strings are same: First by length, then by characters
        /// </summary>
        /// <param name="A">The string you want to check with</param>
        /// <param name="B">The string you want to compare to the string to check</param>
        /// <returns>True if both strings are the exact same (characters and length)</returns>
        public static bool Same(this string A, string B)
        {
            if (A.Length != B.Length) return false;

            if (!A.Trim().ToLower().Equals(B.Trim().ToLower())) return false;

            return true;
        }

        /// <summary>
        /// Returns the string of current date and time into UTC format
        /// </summary>
        /// <param name="self">The date and time to change to</param>
        /// <returns></returns>
        public static string UTC_String (this DateTime self)
        {
            return self
                .ToUniversalTime()
                .ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK");
        }
    }
}
