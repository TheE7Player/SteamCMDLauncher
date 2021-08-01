using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Controls;

namespace SteamCMDLauncher.Component.Validation
{
    public class ServerAliasValidation : ValidationRule
    {

        public ServerAliasValidation()
        {
        }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo)
        {
            string text = (string)value;

            if(string.IsNullOrWhiteSpace(text)) return new ValidationResult(false, "Alias cannot be empty");

            if (text.Contains(".")) return new ValidationResult(false, "Alias cannot contain any full stops '.'");

            text = null;
            return ValidationResult.ValidResult;
        }
    }
}
