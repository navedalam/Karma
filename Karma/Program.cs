using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace eventuali
{
    static class Program
    {
        public static Form UI;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>

        [STAThread]
        static void Main()
        {
            UI = null;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            UI = new filemanager();
            Application.Run(UI);
        }
    }
}
