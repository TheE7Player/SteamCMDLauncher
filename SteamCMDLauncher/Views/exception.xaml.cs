using System;
using System.Text;
using System.Windows;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace SteamCMDLauncher.Views
{
    /// <summary>
    /// Interaction logic for exception.xaml
    /// </summary>
    public partial class exception : Window
    {
        System.Exception exp = null;

        string type = string.Empty;
        string message = string.Empty;

        public exception(Exception e)
        {
            this.exp = e;
            type = exp.GetType().ToString().Replace("System.", "").Trim();
            message = exp.Message;

            InitializeComponent();

            ExceptionType.Text = type;
            ExceptionMessage.Text = message;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string file_dump_loc = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stderr", $"stderr-{DateTime.Now.Day}-{DateTime.Now.Month}-{DateTime.Now.Year}-{DateTime.Now.Ticks}.txt");

            StringBuilder sb = new StringBuilder();

            TimeSpan execution_time = DateTime.Now - App.StartTime;

            StackTrace st = new StackTrace(exp, true);
            
            StackFrame fault_file = st.GetFrame(1);

            sb.AppendLine(DateTime.Now.ToShortDateString());
            sb.AppendLine($"Run time: {execution_time.Duration()}");
            sb.AppendLine($"Exception type: {type}");
            sb.AppendLine($"Exception Message: {message}");

            sb.AppendLine($"Fault parent holder: {exp.Source}");
            sb.AppendLine($"Fault file: \"{fault_file.GetFileName()}\" @ {fault_file.GetMethod()} ({fault_file.GetFileLineNumber()}, {fault_file.GetFileColumnNumber()})");

            sb.AppendLine($"Call stack (len = [{st.FrameCount-1}])");
            
            for (int i = 1; i < st.FrameCount; i++)
            {
                fault_file = st.GetFrame(i);
                sb.Append($"    [{i}] {fault_file}");
            }

            File.WriteAllText(file_dump_loc, sb.ToString());

            // Now we append the current log file with it
            DirectoryInfo logFolder = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"));
            FileInfo myFile = logFolder.GetFiles().OrderByDescending(f => f.LastWriteTime).First();

            if(!ReferenceEquals(myFile, null))
            {
                List<string> log_file = File.ReadAllLines(myFile.FullName).ToList();

                log_file.Insert(0, "\nCorresponding log file:");
                File.AppendAllLines(file_dump_loc, log_file);

                log_file = null;
            }

            logFolder = null;
            myFile = null;
            file_dump_loc = null;
            sb = null;
            st = null;
            fault_file = null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}