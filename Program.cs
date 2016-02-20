using System;
using System.IO;

namespace PrimerPipeline
{
    static class Program
    {
        [STAThread]
        public static void Main()
        {
            PrimerPipeline.App app = new PrimerPipeline.App();
            app.InitializeComponent();
            app.Run();
        }

        public static string GetDirectory()
        {
            return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        }

        #region Accessor methods

        public static string Name
        {
            get { return "PrimerPipeline"; }
        }

        #endregion
    }
}
