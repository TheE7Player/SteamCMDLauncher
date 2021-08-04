using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;

namespace SteamCMDLauncher.UIComponents
{
    public class DialogHostContent
    {

        public StackPanel YesNoDialog(string message)
        {
            Button Yes, No;
            var st = new StackPanel();

            st.Children.Add(new TextBlock { Text = message });

            st.Children.Add(new Separator());

            Yes = new Button
            {
                Content = "Yes",
            };

            No = new Button
            {
                Content = "No"
            };

            Yes.Click += (s, e) =>
            {
                var dumb = (MaterialDesignThemes.Wpf.PopupEx)((Grid)((MaterialDesignThemes.Wpf.Card)((StackPanel)((Button)s).Parent).Parent).Parent).Parent;

                dumb.StaysOpen = false;
                dumb.ReleaseMouseCapture();
                e.Handled = true;
                dumb.PointToScreen(new System.Windows.Point(0,0));
                Config.Log(s.ToString());
                Config.Log(e.ToString());
            };

            st.Children.Add(Yes);

            st.Children.Add(No);

            return st;
        }

    }
}
