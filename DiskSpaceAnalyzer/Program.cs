using System;
using System.IO;
using System.Windows.Forms;

namespace DiskSpaceAnalyzer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                // Log startup info to help diagnose ScreenConnect issues
                string logPath = Path.Combine(Path.GetTempPath(), "DiskSpaceAnalyzer_Error.log");
                File.WriteAllText(logPath, $"Starting at: {DateTime.Now}\n");
                File.AppendAllText(logPath, $"Current Directory: {Directory.GetCurrentDirectory()}\n");
                File.AppendAllText(logPath, $"EXE Location: {Application.ExecutablePath}\n");
                File.AppendAllText(logPath, $"User: {Environment.UserName}\n");
                File.AppendAllText(logPath, $"Machine: {Environment.MachineName}\n\n");

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                // Write full error to log file in temp folder (accessible even from SYSTEM account)
                try
                {
                    string logPath = Path.Combine(Path.GetTempPath(), "DiskSpaceAnalyzer_Error.log");
                    File.AppendAllText(logPath, $"\n\nERROR: {ex.Message}\n");
                    File.AppendAllText(logPath, $"Stack Trace:\n{ex.StackTrace}\n");
                    File.AppendAllText(logPath, $"Inner Exception: {ex.InnerException?.Message}\n");

                    MessageBox.Show(
                        $"Error starting application.\n\n" +
                        $"Log file created at:\n{logPath}\n\n" +
                        $"Error: {ex.Message}",
                        "Disk Space Analyzer Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
                catch
                {
                    // If even logging fails, show basic error
                    MessageBox.Show(
                        $"Critical error: {ex.Message}\n\n" +
                        $"Could not write log file.",
                        "Disk Space Analyzer Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }
    }
}