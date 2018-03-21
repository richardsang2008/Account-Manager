using System;
using System.IO;
using System.Windows.Forms;

namespace PokemonGoGUI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args));

            Application.ThreadException += Application_ThreadException;
        }

        private static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            if(e.Exception is ObjectDisposedException)
            {
                MessageBox.Show("An object disposed exception has occured. A log will be written to log.txt", "Fatal Error");

                try
                {
                    File.WriteAllText("log.txt", e.Exception.ToString());
                }
                catch
                {
                    MessageBox.Show(String.Format("Failed to write log file. Screenshot this exception:\n{0}", e.Exception.ToString()));
                }

                Application.Exit();
            }
        }
    }
}
