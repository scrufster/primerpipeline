using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace PrimerPipeline
{
    public static class ExtensionMethods
    {
        public static string Reverse(this string s)
        {
            int length = s.Length;

            if (length > 1)
            {
                char[] chars = new char[length];
                int lastIndex = length - 1;

                for (int i = 0; i < length; i++)
                {
                    chars[i] = s[lastIndex - i];
                }

                return new string(chars);
            }
            else
            {
                return s;
            }
        }
    }

    static class ExtensionMethods_Colour
    {
        private static readonly Dictionary<string, Color> knownColors = GetKnownColors();

        public static string GetColorName(this Color color)
        {
            string result = knownColors
                .Where(kvp => kvp.Value.Equals(color))
                .Select(kvp => kvp.Key)
                .FirstOrDefault();

            return result == null ? "" : result;
        }

        static Dictionary<string, Color> GetKnownColors()
        {
            var colorProperties = typeof(Colors).GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            return colorProperties
                .ToDictionary(
                    p => p.Name,
                    p => (Color)p.GetValue(null, null));
        }
    }
    
    public static class FilePreLoadState
    {
        static FilePreLoadState() { }

        public static void GetDataFilePreLoadMessage(Window owner, FilePreLoadState.PreLoadState state, string fileDescription)
        {
            string message = "";

            switch (state)
            {
                case FilePreLoadState.PreLoadState.LOCKED: message = string.Format("The {0} data file is currently locked by another process.", fileDescription); break;
                default: message = string.Format("The {0} data file could not be found.", fileDescription); break;
            }

            MessageBox.Show(owner, message, Program.Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        public static PreLoadState GetPreLoadState(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return PreLoadState.MISSING;
            }
            else if (IsFileLocked(fileName))
            {
                return PreLoadState.LOCKED;
            }
            else
            {
                return PreLoadState.READY;
            }
        }

        /// <summary>
        /// Check if a file is locked by another process before attempting to access it.
        /// </summary>
        /// <param name="fileName">The fileName to check.</param>
        /// <returns>True if the file is currently in use, false if it is not.</returns>
        private static bool IsFileLocked(string fileName)
        {
            FileStream stream = null;

            try
            {
                stream = new FileInfo(fileName).Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                //the file is unavailable because it is still being written to or being processed by another thread:
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        public enum PreLoadState
        {
            LOCKED, MISSING, READY
        }
    }
   
    static class MiscTask
    {
        public static string GetBasePath(string fileName)
        {
            return string.Format("{0}\\{1}", Path.GetDirectoryName(fileName), Path.GetFileNameWithoutExtension(fileName));
        }

        public static List<string> GetFilesFromFilesAndFoldersList(string[] list, string fileSearchPattern = "*.*")
        {
            List<string> outputList = new List<string>(list.Length);

            for (int i = 0; i < list.Length; i++)
            {
                if (File.GetAttributes(list[i]) != FileAttributes.Directory)
                {
                    outputList.Add(list[i]);
                }
                else
                {
                    //find sub-directories:
                    List<string> relevantFolders = new List<string>();

                    //add the root folder itself:
                    relevantFolders.Add(list[i]);

                    SearchForDirectories(list[i], "*", relevantFolders);

                    for (int j = 0; j < relevantFolders.Count; j++)
                    {
                        try
                        {
                            SearchForFiles(relevantFolders[j], fileSearchPattern, outputList);
                        }
                        catch { }
                    }
                }
            }

            return outputList;
        }

        public static void OpenDirectory(string directory, bool selectFile = false, string fileName = "")
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    if (!selectFile || fileName.Equals(""))
                    {
                        System.Diagnostics.Process.Start(directory);
                    }
                    else
                    {
                        System.Diagnostics.Process.Start("explorer.exe", "/select, \"" + fileName + "\"");
                    }
                }
            }
            catch { }
        }

        public static void OpenDirectory(Window owner, string fileName, bool selectFile)
        {
            string directory = Directory.GetParent(fileName).FullName;

            if (MessageBox.Show(owner, "Do you want to open the output directory?",
                Program.Name, MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes)
            {
                OpenDirectory(directory, selectFile, fileName);
            }
        }

        /// <summary>
        /// Adds an 's' to the end of a word if the number is not 1.
        /// </summary>
        /// <param name="word">The singular version of the word.</param>
        /// <param name="number">The number of items the word will describe.</param>
        /// <returns>The plural or singular of the word, whichever is required.</returns>
        public static string Pluraliser(string word, int number)
        {
            return number == 1 ? word : word + "s";
        }

        public static void SearchForDirectories(string rootFolder, string searchPattern, List<string> foldersList)
        {
            foreach (string folder in Directory.EnumerateDirectories(rootFolder, searchPattern, SearchOption.TopDirectoryOnly))
            {
                foldersList.Add(folder);
            }

            foreach (string subDir in Directory.GetDirectories(rootFolder))
            {
                try
                {
                    SearchForDirectories(subDir, searchPattern, foldersList);
                }
                catch { }
            }
        }

        public static void SearchForFiles(string folder, string searchPattern, List<string> filesList)
        {
            foreach (string file in Directory.EnumerateFiles(folder, searchPattern, SearchOption.TopDirectoryOnly))
            {
                if (!filesList.Contains(file))
                {
                    filesList.Add(file);
                }
            }
            foreach (string subDir in Directory.GetDirectories(folder))
            {
                try
                {
                    SearchForFiles(subDir, searchPattern, filesList);
                }
                catch { }
            }
        }
        
        /// <summary>
        /// Show a list of strings to the user, and optionally return a dialog response.
        /// </summary>
        /// <param name="owner">The owning window. The list window will be centred in this.</param>
        /// <param name="listItems">The list to be displayed.</param>
        /// <param name="preListMessage">A message to come before the list.".</param>
        /// <param name="postListMessage">A message to be shown beneath the list.</param>
        /// <param name="buttons">The buttons to be shown on the list window.</param>
        /// <returns>A true/false result based on user response.</returns>
        public static bool StringListToMessageBox(Window owner, List<string> listItems, string preListMessage,
            string postListMessage, MessageBoxButton buttons = MessageBoxButton.OK)
        {
            if (listItems.Count > 0)
            {
                Window_MessageBoxWithList messageBoxWithList = new Window_MessageBoxWithList(owner, listItems, preListMessage, buttons, postListMessage);

                return messageBoxWithList.ShowDialog().Value;
            }

            return true;
        }
    }
}