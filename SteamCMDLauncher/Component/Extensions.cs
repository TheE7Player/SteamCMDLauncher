using System;
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
            if (A?.Length != B?.Length) return false;

            if (!(bool)A?.Trim().ToLower().Equals(B?.Trim().ToLower())) return false;

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
    
        /// <summary>
        /// Returns the SHA-256 of a file, assuming 'path' is an actual file
        /// </summary>
        /// <param name="path">The file to get the SHA-256 hash from</param>
        /// <returns>The 65 length hash SHA-256 of the file - 'string.Empty' if not a file</returns>
        public static string GetSHA256Sum(this string path)
        {
            Config.Log($"[SHA-256] Getting hash from file: {path}");
            if (!System.IO.File.Exists(path)) return string.Empty;

            // https://stackoverflow.com/questions/38474362/get-a-file-sha256-hash-code-and-checksum/51966515#51966515
            // Same idea but different logic (this comment means to only output the B64 string, not the hex we need!)

            // Good resource: https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.hashalgorithm.computehash?view=net-5.0

            string output = null;

            using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256Managed.Create())
            {
                byte[] encr = hash.ComputeHash(System.IO.File.ReadAllBytes(path));

                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                System.Collections.IEnumerator bEnum = encr.GetEnumerator();

                string hex_cvrt = "x2";

                while (bEnum.MoveNext())
                {
                    sb.Append(((byte)bEnum.Current).ToString(hex_cvrt));
                }

                output = sb.ToString();
                hex_cvrt = null;
                sb = null;
                bEnum = null;
                encr = null;
            }

            return output;
        }

        // Quick wrapper for string
        public static string[] GetArrayFromText(this string text, char spliter) => text.Split(spliter);       
    }
}
