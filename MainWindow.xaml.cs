using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using Microsoft.Win32;

namespace PrimerPipeline
{
    public partial class MainWindow : Window
    {
        #region Variables

        private List<Window_Results> resultsWindows = new List<Window_Results>();

        //settings for the user interface:
        private DNA_Trimmer.Settings trimSettings = new DNA_Trimmer.Settings();
        private MicrosatelliteCalculator.Settings misaSettings = new MicrosatelliteCalculator.Settings();
        private Primer3Settings primer3Settings = new Primer3Settings();

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            Title = Program.Name;

            InitialiseSettings_Trimming();
            InitialiseSettings_MISA();
            InitialiseSettings_Primer3(); 

            UpdateButtonStates_ProcessQueue();
            UpdateButtonStates_DNATrim();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //ensure that any Primer3 processes are stopped:
            for (int i = 0; i < files_ListView.Items.Count; i++)
            {
                ((PipelineFile)files_ListView.Items[i]).Stop();
            }
        }

        private void ManagePipelineFileEvents(PipelineFile file, bool subscribe)
        {
            if (subscribe)
            {
                file.ProcessCompleted += PipelineFile_ProcessCompleted;
            }
            else
            {
                file.ProcessCompleted -= PipelineFile_ProcessCompleted;
            }
        }

        private void PipelineFile_ProcessCompleted(object sender, EventArgs e)
        {
            SetContextMenu();
        }

        private void ResultsWindow_Closing(object sender, CancelEventArgs e)
        {
            Window_Results resultsWindow = (Window_Results)sender;
            resultsWindow.Closing -= ResultsWindow_Closing;

            //remove this window from the list:
            resultsWindows.Remove(resultsWindow);
        }

        #region Context menu

        private void MoveToNextTask_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (files_ListView.SelectedItems.Count == 1)
            {
                PipelineFile fileToOpen = (PipelineFile)files_ListView.SelectedItems[0];

                fileToOpen.MoveToNextTask();

                //we no longer need this option on the context menu:
                SetContextMenu();
            }
        }

        private void SetContextMenu()
        {
            ContextMenu contextMenu = null;

            if (files_ListView.SelectedItems.Count == 1)
            {
                PipelineFile file = (PipelineFile)files_ListView.SelectedItems[0];

                if (file.IsProcessing())
                {
                    contextMenu = new ContextMenu();

                    if (file.HasNextTask())
                    {
                        MenuItem moveToNextTaskProcess_MenuItem = new MenuItem();
                        moveToNextTaskProcess_MenuItem.Header = "Move to next task";
                        moveToNextTaskProcess_MenuItem.Click += MoveToNextTask_MenuItem_Click;

                        contextMenu.Items.Add(moveToNextTaskProcess_MenuItem);

                        contextMenu.Items.Add(new Separator());
                    }

                    MenuItem stopProcess_MenuItem = new MenuItem();
                    stopProcess_MenuItem.Header = "Cancel process";
                    stopProcess_MenuItem.Click += StopProcess_MenuItem_Click;

                    contextMenu.Items.Add(stopProcess_MenuItem);
                }
                else if (file.Processed)
                {
                    contextMenu = new ContextMenu();

                    MenuItem viewResults_MenuItem = new MenuItem();
                    viewResults_MenuItem.Header = "View results";
                    viewResults_MenuItem.Click += ViewResults_MenuItem_Click;

                    contextMenu.Items.Add(viewResults_MenuItem);
                }
            }

            files_ListView.ContextMenu = contextMenu;
        }

        private void StopProcess_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (files_ListView.SelectedItems.Count == 1)
            {
                PipelineFile fileToOpen = (PipelineFile)files_ListView.SelectedItems[0];

                fileToOpen.Stop();

                //we no longer need this option on the context menu:
                SetContextMenu();
            }
        }

        private void ViewResults_MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (files_ListView.SelectedItems.Count == 1)
            {
                PipelineFile fileToOpen = (PipelineFile)files_ListView.SelectedItems[0];

                if (!fileToOpen.IsLocked())
                {
                    //Is this file already open in a results window?
                    for (int i = 0; i < resultsWindows.Count; i++)
                    {
                        if (resultsWindows[i].ContainsFile(fileToOpen))
                        {
                            resultsWindows[i].Focus();

                            return;
                        }
                    }

                    //if we got to here we need to open a new window:
                    Window_Results resultsWindow = new Window_Results(this, (PipelineFile)files_ListView.SelectedItems[0]);
                    resultsWindow.Closing += ResultsWindow_Closing;

                    resultsWindows.Add(resultsWindow);
                    resultsWindow.Show();
                }
                else
                {
                    MessageBox.Show("The selected file is currently locked by another process.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);
                }
            }
        }

        #endregion

        #region File compare

        private void Compare_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog getSourceDialog = new OpenFileDialog();
            getSourceDialog.Multiselect = true;

            if (getSourceDialog.ShowDialog().Value && getSourceDialog.FileNames.Length == 2)
            {
                BackgroundWorker bgW = new BackgroundWorker();
                bgW.WorkerReportsProgress = true;

                bgW.DoWork += BGW_CompareFiles_DoWork;
                bgW.RunWorkerCompleted += BGW_RunWorkerCompleted;


                bgW.RunWorkerAsync(new object[] { getSourceDialog.FileNames[0], getSourceDialog.FileNames[1] });
            } 
        }

        private void BGW_CompareFiles_DoWork(object sender, DoWorkEventArgs e)
        {
            object[] args = (object[])e.Argument;

            string file1 = (string)args[0];
            string file2 = (string)args[1];

            int count_F1 = 0, count_F2 = 0;
            bool identical = true;

            using (StreamReader sW1 = new StreamReader(file1))
            {
                using (StreamReader sW2 = new StreamReader(file2))
                {
                    try
                    {
                        while (!sW1.EndOfStream && !sW2.EndOfStream)
                        {
                            string line_SW1 = "", sequenceName_SW1 = "";

                            //get the next non-empty line:
                            while (line_SW1.Trim().Equals(""))
                            {
                                line_SW1 = sW1.ReadLine();

                                if (line_SW1.StartsWith(">"))
                                {
                                    //this is the sequence name:
                                    sequenceName_SW1 = line_SW1;

                                    line_SW1 = "";
                                }

                                count_F1++;
                            }

                            string line_SW2 = "", sequenceName_SW2 = "";

                            //get the next non-empty line:
                            while (line_SW2.Trim().Equals(""))
                            {
                                line_SW2 = sW2.ReadLine();

                                if (line_SW2.StartsWith(">"))
                                {
                                    //this is the sequence name:
                                    sequenceName_SW2 = line_SW2;

                                    line_SW2 = "";
                                }

                                count_F2++;
                            }

                            if (!line_SW1.Equals(line_SW2))
                            {
                                identical = false;
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        sW1.Close();
                        sW2.Close();
                    }
                }
            }

            e.Result = string.Format("Lines ({0}): {1}\nLines ({2}): {3}\n\nIdentical: {4}", 
                Path.GetFileName(file1), count_F1, Path.GetFileName(file2), count_F2, identical);
        }

        private void BGW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show(string.Format("Compare complete. Results:\n\n{0}", (string)e.Result));
        }

        #endregion

        #region Process queue

        private List<PipelineFile> GetFilesToProcess()
        {
            List<PipelineFile> result = new List<PipelineFile>(files_ListView.Items.Count);

            List<string> lockedFiles = new List<string>(files_ListView.Items.Count);

            int alreadyProcessedFiles = 0;

            //if any of the files are already processed, see if the user wants to process them again:
            for (int i = 0; i < files_ListView.SelectedItems.Count; i++)
            {
                PipelineFile file = (PipelineFile)files_ListView.SelectedItems[i];

                if (file.Processed)
                {
                    alreadyProcessedFiles++;
                }

                if (!file.IsLocked())
                {
                    result.Add(file);
                }
                else
                {
                    lockedFiles.Add(file.SafeFileName);
                }
            }

            //if we've got any already processed files, ask if they really want to do them again:
            if (alreadyProcessedFiles > 0)
            {
                if (MessageBox.Show(string.Format("{0} selected {1} {2} already been processed.\n\nDo you want to process {3} again?",
                    alreadyProcessedFiles == 1 || alreadyProcessedFiles == result.Count ? "The" : string.Format("{0} of the", alreadyProcessedFiles),
                    alreadyProcessedFiles == 1 ? "file" : "files",
                    alreadyProcessedFiles == 1 ? "has" : "have",
                    alreadyProcessedFiles == 1 ? "it" : "them"), 
                    Program.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    result.RemoveAll(f => f.Processed);
                }
            }

            if (lockedFiles.Count > 0)
            {
                MiscTask.StringListToMessageBox(this, lockedFiles,    
                    string.Format("The following {0} are currently locked by another process and cannot be processed at this time:", 
                    MiscTask.Pluraliser("file", lockedFiles.Count)), ""); 
            }

            return result;
        }

        private void LoadFiles(IList<string> files)
        {
            List<string> lockedFiles = new List<string>(files.Count);

            for (int i = 0; i < files.Count; i++)
            {
                if (FilePreLoadState.GetPreLoadState(files[i]) == FilePreLoadState.PreLoadState.READY)
                {
                    //make the pipeline file first, so that we can check that it's not already in the list:
                    PipelineFile pipelineFile = new PipelineFile(files[i]);

                    if (!files_ListView.Items.Contains(pipelineFile))
                    {
                        files_ListView.Items.Add(pipelineFile);

                        ManagePipelineFileEvents(pipelineFile, true);
                    }
                }
                else
                {
                    lockedFiles.Add(Path.GetFileName(files[i]));
                }
            }

            UpdateButtonStates_ProcessQueue();

            if (lockedFiles.Count > 0)
            {
                MiscTask.StringListToMessageBox(this, lockedFiles,
                    string.Format("The following {0} are currently locked by another process:", MiscTask.Pluraliser("file", lockedFiles.Count)), ""); 
            }
        }

        private void RemovePipelineFiles(List<PipelineFile> itemsToRemove)
        {
            for (int i = 0; i < itemsToRemove.Count; i++)
            {
                files_ListView.Items.Remove(itemsToRemove[i]);

                ManagePipelineFileEvents(itemsToRemove[i], false);
            }

            itemsToRemove.Clear();

            UpdateButtonStates_ProcessQueue();
        }

        private void UpdateButtonStates_ProcessQueue()
        {
            processQueue_RemoveSelected_Button.IsEnabled = files_ListView.SelectedItems.Count > 0;
            processQueue_RemoveAll_Button.IsEnabled = files_ListView.Items.Count > 0;

            processPipeline_Button.IsEnabled = files_ListView.SelectedItems.Count > 0;
        }

        #region Events

        private void Files_ListView_Drop(object sender, DragEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DataObject droppedObject = (DataObject)e.Data;

                //if what was dragged contains files, load them:
                if (droppedObject.ContainsFileDropList())
                {
                    //get the names of the dropped items:
                    System.Collections.Specialized.StringCollection droppedStringCollection = droppedObject.GetFileDropList();

                    //make an array for the dropped items:
                    string[] itemsDropped = new string[droppedStringCollection.Count];

                    //copy the fileNames into the array:
                    droppedStringCollection.CopyTo(itemsDropped, 0);

                    LoadFiles(MiscTask.GetFilesFromFilesAndFoldersList(itemsDropped));                   
                }
            }));
        }

        private void Files_ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates_ProcessQueue();

            SetContextMenu();
        }
        
        private void ProcessQueue_OpenFiles_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog().Value)
            {
                LoadFiles(openFileDialog.FileNames); 
            }
        }
        
        private void ProcessPipeline_Button_Click(object sender, RoutedEventArgs e)
        {
            List<PipelineFile> filesToProcess = GetFilesToProcess();

            if (filesToProcess.Count > 0)
            {
                bool canContinue = true;

                //check that relevant items exist:
                if (!Primer3Settings.VerifyEXE())
                {
                    MessageBox.Show(string.Format("The Primer3.exe could not be found.\n\nPlease ensure that this is located in the same directory as the {0}.exe.", 
                        Program.Name), Program.Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    canContinue = false;
                }

                primer3Settings.InputFileIncludesThermodynamicParameters = primer3Input_IncludeThermodynamicParameters_CheckBox.IsChecked.Value;

                if (!primer3Settings.ThermodynamicSettingsValid())
                {
                    MessageBox.Show(string.Format("The primer3_config directory could not be found.\n\nPlease ensure that this is located in the same directory as the {0}.exe.",
                        Program.Name), Program.Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);

                    canContinue = false;
                }

                if (canContinue)
                {
                    //assign trimming arguments:
                    List<DNA_Trimmer.TrimArgument> trimArguments = new List<DNA_Trimmer.TrimArgument>(trimSettings_ListView.Items.Count);

                    for (int i = 0; i < trimSettings_ListView.Items.Count; i++)
                    {
                        trimArguments.Add((DNA_Trimmer.TrimArgument)trimSettings_ListView.Items[i]);
                    }

                    DNA_Trimmer.Settings exportTrimSettings = new DNA_Trimmer.Settings(trimArguments, exportTrimmedFile_CheckBox.IsChecked.Value);

                    //save the trim settings for the next time this program is opened:
                    exportTrimSettings.SaveCurrentSettings();

                    //MISA settings:
                    List<MicrosatelliteCalculator.Settings.MisaDefinition> misaDefinitions
                        = new List<MicrosatelliteCalculator.Settings.MisaDefinition>(misaDefinitions_ListView.Items.Count);

                    for (int i = 0; i < misaDefinitions_ListView.Items.Count; i++)
                    {
                        misaDefinitions.Add((MicrosatelliteCalculator.Settings.MisaDefinition)misaDefinitions_ListView.Items[i]);
                    }

                    MicrosatelliteCalculator.Settings misaSettings = new MicrosatelliteCalculator.Settings((int)misaInterruptions_NumericTextBox.NumericValue, misaDefinitions);

                    //save the MISA settings for the next time this program is opened:
                    misaSettings.SaveCurrentSettings();

                    //Save a copy for the next time the program is opened:
                    primer3Settings.SaveCurrentSettings();

                    //get a copy of the Primer3 settings, in case they are edited whilst the program is running:
                    Primer3Settings primer3Settings_Copy = new Primer3Settings(primer3Settings);

                    for (int i = 0; i < filesToProcess.Count; i++)
                    {
                        PipelineFile.FileType fileType = filesToProcess[i].MyFileType;

                        string misaFileName = "";

                        //if this file is a P3 in file, we also need the name of the Misa file to be able
                        //to make the final results file:
                        if (fileType == PipelineFile.FileType.PRIMER3_IN
                            || fileType == PipelineFile.FileType.PRIMER3_OUT)
                        {
                            OpenFileDialog openFileDialog = new OpenFileDialog();
                            openFileDialog.Multiselect = false;
                            openFileDialog.Title = string.Format("Select Misa file for {0}", filesToProcess[i].SafeFileName);

                            if (openFileDialog.ShowDialog().Value)
                            {
                                misaFileName = openFileDialog.FileName;
                            }
                            else
                            {
                                if (fileType == PipelineFile.FileType.PRIMER3_IN)
                                {
                                    if (MessageBox.Show("You have not selected a Misa file. This means that a final results CSV file cannot be "
                                    + "created for this file. A Primer3 output file can still be created.\n\nDo you want to continue?",
                                    Program.Name, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                                    {
                                        continue;
                                    }
                                }
                                else
                                {
                                    MessageBox.Show("You have not selected a Misa file. This means that a final results CSV file cannot be created for this file.",
                                        Program.Name, MessageBoxButton.OK, MessageBoxImage.Exclamation);

                                    continue;                                    
                                }                                
                            }
                        }

                        filesToProcess[i].StartProcess(exportTrimSettings, misaSettings, primer3Settings_Copy, misaFileName);
                    }

                    SetContextMenu();
                }
            }
        }

        private void ProcessQueue_RemoveAll_Button_Click(object sender, RoutedEventArgs e)
        {
            //we can only deal with items that are not currently processing:
            List<PipelineFile> removableItems = new List<PipelineFile>();

            for (int i = 0; i < files_ListView.Items.Count; i++)
            {
                PipelineFile file = (PipelineFile)files_ListView.Items[i];

                if (!file.IsProcessing())
                {
                    removableItems.Add(file);
                }
            }

            RemovePipelineFiles(removableItems);
        }

        private void ProcessQueue_RemoveSelected_Button_Click(object sender, RoutedEventArgs e)
        {
            if (files_ListView.SelectedItems.Count > 0)
            {
                //we can only deal with items that are not currently processing:
                List<PipelineFile> removableItems = new List<PipelineFile>();

                for (int i = 0; i < files_ListView.SelectedItems.Count; i++)
                {
                    PipelineFile file = (PipelineFile)files_ListView.SelectedItems[i];

                    if (!file.IsProcessing())
                    {
                        removableItems.Add(file);
                    }
                }

                RemovePipelineFiles(removableItems);
            }
        }

        #endregion

        #endregion

        #region DNA trimming

        private List<object> GetSelectedArgumentItems()
        {
            List<object> result = new List<object>(trimSettings_ListView.SelectedItems.Count);

            for (int i = 0; i < trimSettings_ListView.SelectedItems.Count; i++)
            {
                result.Add(trimSettings_ListView.SelectedItems[i]);
            }

            return result;
        }

        private void InitialiseSettings_Trimming()
        {
            exportTrimmedFile_CheckBox.IsChecked = trimSettings.ExportTrimmedFile;

            trimSettings_ListView.Items.Clear();

            for (int i = 0; i < trimSettings.Arguments.Count; i++)
            {
                trimSettings_ListView.Items.Add(trimSettings.Arguments[i]);
            }

            UpdateButtonStates_DNATrim();
        }

        private void SelectedArgument_MoveTo(List<object> items, int startIndex)
        {
            if (items.Count > 0 && items.Count != trimSettings_ListView.Items.Count && startIndex >= 0)
            {
                //first remove the items:
                for (int i = 0; i < items.Count; i++)
                {
                    trimSettings_ListView.Items.Remove(items[i]);
                }

                //now put them back in:
                for (int i = 0; i < items.Count; i++)
                {
                    if (startIndex > trimSettings_ListView.Items.Count)
                    {
                        startIndex = trimSettings_ListView.Items.Count;
                    }

                    trimSettings_ListView.Items.Insert(startIndex++, items[i]);

                    //also, keep them selected
                    trimSettings_ListView.SelectedItems.Add(items[i]);
                }
            }
        }

        private void UpdateButtonStates_DNATrim()
        {
            trim_EditArgument_Button.IsEnabled = trimSettings_ListView.SelectedItems.Count == 1;
            trim_RemoveSelected_Button.IsEnabled = trimSettings_ListView.SelectedItems.Count > 0;
            trim_RemoveAll_Button.IsEnabled = trimSettings_ListView.Items.Count > 0;
        }

        #region Events

        private void MoveDown_Button_Click(object sender, RoutedEventArgs e)
        {
            SelectedArgument_MoveTo(GetSelectedArgumentItems(), trimSettings_ListView.SelectedIndex + 1);    
        }

        private void MoveToBottom_Button_Click(object sender, RoutedEventArgs e)
        {
            SelectedArgument_MoveTo(GetSelectedArgumentItems(), trimSettings_ListView.Items.Count - 1);
        }

        private void MoveToTop_Button_Click(object sender, RoutedEventArgs e)
        {
            SelectedArgument_MoveTo(GetSelectedArgumentItems(), 0);
        }

        private void MoveUp_Button_Click(object sender, RoutedEventArgs e)
        {
            SelectedArgument_MoveTo(GetSelectedArgumentItems(), trimSettings_ListView.SelectedIndex - 1);      
        }

        private void Trim_AddArgument_Button_Click(object sender, RoutedEventArgs e)
        {
            Window_TrimArgument trimArgumentWindow = new Window_TrimArgument(this);

            if (trimArgumentWindow.ShowDialog().Value)
            {
                trimSettings_ListView.Items.Add(trimArgumentWindow.GetArgument());

                UpdateButtonStates_DNATrim();
            }
        }

        private void Trim_EditArgument_Button_Click(object sender, RoutedEventArgs e)
        {
            if (trimSettings_ListView.SelectedItems.Count == 1)
            {
                DNA_Trimmer.TrimArgument argument = (DNA_Trimmer.TrimArgument)trimSettings_ListView.SelectedItems[0];

                Window_TrimArgument trimArgumentWindow = new Window_TrimArgument(this, argument);

                if (trimArgumentWindow.ShowDialog().Value)
                {
                    int index = trimSettings_ListView.Items.IndexOf(argument);

                    trimSettings_ListView.Items.Remove(argument);
                    trimSettings_ListView.Items.Insert(index, trimArgumentWindow.GetArgument());
                }
            }
        }

        private void Trim_LoadSettings_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files|*.txt";

            if (openFileDialog.ShowDialog().Value)
            {
                try
                {
                    trimSettings = new DNA_Trimmer.Settings(openFileDialog.FileName);
                    
                    InitialiseSettings_Trimming();
                }
                catch
                {
                    MessageBox.Show("An error occurred while trying to load your trimming settings.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Trim_RemoveAll_Button_Click(object sender, RoutedEventArgs e)
        {
            trimSettings_ListView.Items.Clear();

            UpdateButtonStates_DNATrim();
        }

        private void Trim_RemoveSelected_Button_Click(object sender, RoutedEventArgs e)
        {
            if (trimSettings_ListView.SelectedItems.Count > 0)
            {
                int selectedItems = trimSettings_ListView.SelectedItems.Count;

                for (int i = 0; i < selectedItems; i++)
                {
                    trimSettings_ListView.Items.Remove(trimSettings_ListView.SelectedItems[0]);
                }
            }

            UpdateButtonStates_DNATrim();
        }
        
        private void Trim_ResetSettingsToDefault_Button_Click(object sender, RoutedEventArgs e)
        {
            trimSettings.ResetToDefault();

            InitialiseSettings_Trimming();
        }

        private void Trim_SaveSettings_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text files|*.txt";

            if (saveFileDialog.ShowDialog().Value)
            {
                try
                {
                    trimSettings.Save(saveFileDialog.FileName);
                }
                catch
                {
                    MessageBox.Show("An error occurred while trying to save your trimming settings.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TrimSettings_ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates_DNATrim();
        }

        #endregion

        #endregion

        #region MISA

        private void InitialiseSettings_MISA()
        {
            misaInterruptions_NumericTextBox.NumericValue = misaSettings.Interruptions;
            misaDefinitions_ListView.Items.Clear();

            for (int i = 0; i < misaSettings.Definitions.Count; i++)
            {
                misaDefinitions_ListView.Items.Add(misaSettings.Definitions[i]);
            }

            UpdateButtonStates_MISA();
        }

        private void UpdateButtonStates_MISA()
        {
            misa_EditArgument_Button.IsEnabled = misaDefinitions_ListView.SelectedItems.Count == 1;
            misa_RemoveSelected_Button.IsEnabled = misaDefinitions_ListView.SelectedItems.Count > 0;
            misa_RemoveAll_Button.IsEnabled = misaDefinitions_ListView.Items.Count > 0;
        }

        #region Events

        private void MISA_AddDefinition_Button_Click(object sender, RoutedEventArgs e)
        {
            Window_MISA_Definition misaDefinitionWindow = new Window_MISA_Definition(this);

            if (misaDefinitionWindow.ShowDialog().Value)
            {
                misaDefinitions_ListView.Items.Add(misaDefinitionWindow.GetDefinition());

                UpdateButtonStates_MISA();
            }
        }

        private void MISA_EditDefinition_Button_Click(object sender, RoutedEventArgs e)
        {
            if (misaDefinitions_ListView.SelectedItems.Count == 1)
            {
                MicrosatelliteCalculator.Settings.MisaDefinition definition = (MicrosatelliteCalculator.Settings.MisaDefinition)misaDefinitions_ListView.SelectedItems[0];

                Window_MISA_Definition misaDefinitionWindow = new Window_MISA_Definition(this, definition);

                if (misaDefinitionWindow.ShowDialog().Value)
                {
                    int index = misaDefinitions_ListView.Items.IndexOf(definition);

                    misaDefinitions_ListView.Items.Remove(definition);
                    misaDefinitions_ListView.Items.Insert(index, misaDefinitionWindow.GetDefinition());
                }
            }
        }

        private void MISA_LoadSettings_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "INI files|*.ini";

            if (openFileDialog.ShowDialog().Value)
            {
                try
                {
                    misaSettings = new MicrosatelliteCalculator.Settings(openFileDialog.FileName);

                    InitialiseSettings_MISA();
                }
                catch
                {
                    MessageBox.Show("An error occurred while trying to load your MISA settings.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MISA_RemoveAll_Button_Click(object sender, RoutedEventArgs e)
        {
            misaDefinitions_ListView.Items.Clear();

            UpdateButtonStates_MISA();
        }

        private void MISA_RemoveSelected_Button_Click(object sender, RoutedEventArgs e)
        {
            if (misaDefinitions_ListView.SelectedItems.Count > 0)
            {
                int selectedItems = misaDefinitions_ListView.SelectedItems.Count;

                for (int i = 0; i < selectedItems; i++)
                {
                    misaDefinitions_ListView.Items.Remove(misaDefinitions_ListView.SelectedItems[0]);
                }
            }

            UpdateButtonStates_MISA();
        }

        private void MISA_ResetSettingsToDefault_Button_Click(object sender, RoutedEventArgs e)
        {
            misaSettings.ResetToDefault();

            InitialiseSettings_MISA();
        }

        private void MISA_SaveSettings_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "INI files|*.ini";

            if (saveFileDialog.ShowDialog().Value)
            {
                try
                {
                    misaSettings.Save(saveFileDialog.FileName);
                }
                catch 
                { 
                    MessageBox.Show("An error occurred while trying to save your MISA settings.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Error); 
                }
            }
        }

        private void MISA_Settings_ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButtonStates_MISA();
        }

        #endregion

        #endregion
        
        #region Primer 3

        private void InitialiseSettings_Primer3()
        {
            primer3Input_IncludeThermodynamicParameters_CheckBox.IsChecked = primer3Settings.InputFileIncludesThermodynamicParameters;

            CollectionViewSource primer3SettingsViewSource = (CollectionViewSource)this.FindResource("Primer3Settings");
            primer3SettingsViewSource.Source = primer3Settings.Settings;
        }

        #region Events

        private void IncludeAdvancedSettings_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(primer3Settings_ListView.ItemsSource);
            view.Refresh();
        }

        private void Primer3_LoadSettings_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text files|*.txt";

            if (openFileDialog.ShowDialog().Value)
            {
                try
                {
                    primer3Settings = new Primer3Settings(openFileDialog.FileName);

                    InitialiseSettings_Primer3();
                }
                catch 
                {                     
                    MessageBox.Show("An error occurred while trying to load your Primer3 settings.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Error); 
                }
            }
        }

        private void Primer3Settings_Filter(object sender, FilterEventArgs e)
        {
            e.Accepted = false;

            if (e.Item is Primer3Settings.Primer3Setting) 
            {
                Primer3Settings.Primer3Setting setting = (Primer3Settings.Primer3Setting)e.Item;

                e.Accepted = setting.DisplayToUser && (!setting.IsAdvancedSetting || includeAdvancedSettings_CheckBox.IsChecked.Value);
            }
        }

        private void Primer3_ResetSettingsToDefault_Button_Click(object sender, RoutedEventArgs e)
        {
            primer3Settings = new Primer3Settings();

            InitialiseSettings_Primer3();
        }

        private void Primer3_SaveSettings_Button_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text files|*.txt";

            if (saveFileDialog.ShowDialog().Value)
            {
                try
                {
                    primer3Settings.Save(saveFileDialog.FileName);
                }
                catch 
                { 
                    MessageBox.Show("An error occurred while trying to save your Primer3 settings.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Error); 
                }
            }
        }

        #endregion

        #endregion
    }
}