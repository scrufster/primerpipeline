using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace PrimerPipeline
{
    public partial class Window_Results : Window
    {
        #region Variables

        private BioDataFile myBioDataFile = null;

        private static bool includeData = true, highlightData = true, highlightInBold = true;

        #endregion

        public Window_Results(Window owner, PipelineFile processedPipelineFile)
        {
            InitializeComponent();

            Owner = owner;

            Title = "Results";

            BioDataFile.BioLine.IndexResult.CreateBrushes();

            LoadFile(processedPipelineFile.GetFinalResultsFileName());
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && Keyboard.IsKeyDown(Key.C))
            {
                CopyDataToClipboard();
            }
        }

        public bool ContainsFile(PipelineFile file)
        {
            if (myBioDataFile != null && myBioDataFile.FileName.Equals(file.FileName))
            {
                return true;
            }

            return false;
        }

        private void CopyDataToClipboard()
        {
            if (results_ListView.SelectedItems.Count == 1)
            {
                BioDataFile.BioLine bioLine = (BioDataFile.BioLine)results_ListView.SelectedItems[0];

                bioLine.CopyToClipboard();
            }
        }

        private void LoadFile(string fileName)
        {
            if (myBioDataFile != null && myBioDataFile.FileName.Equals(fileName))
            {
                MessageBox.Show("The selected file is already loaded.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Information);

                return;
            }

            FilePreLoadState.PreLoadState preLoadState = FilePreLoadState.GetPreLoadState(fileName);

            if (preLoadState == FilePreLoadState.PreLoadState.READY)
            {
                BackgroundWorker bgW = new BackgroundWorker();
                bgW.WorkerReportsProgress = true;

                bgW.DoWork += BGW_DoWork;
                bgW.ProgressChanged += BGW_ProgressChanged;
                bgW.RunWorkerCompleted += BGW_RunWorkerCompleted;

                bgW.RunWorkerAsync(fileName);
            }
            else
            {
                FilePreLoadState.GetDataFilePreLoadMessage(this, preLoadState, "selected");
            }
        }

        private void UpdateData()
        {
            if (myBioDataFile != null)
            {
                bool correctResultsOnly = correctResultsOnly_CheckBox.IsChecked.Value;
                bool includeData = includeData_CheckBox.IsChecked.Value;
                bool highlightData = highlightData_CheckBox.IsChecked.Value;
                bool highlightInBold = highlightInBold_CheckBox.IsChecked.Value;

                FlowDocument flowDocument = new FlowDocument();

                int correctCount = 0;

                results_ListView.Items.Clear();

                for (int i = 0; i < myBioDataFile.BioLines.Count; i++)
                {
                    if (!correctResultsOnly || myBioDataFile.BioLines[i].HasCorrectResult())
                    {
                        results_ListView.Items.Add(myBioDataFile.BioLines[i]);
                    }

                    if (myBioDataFile.BioLines[i].HasCorrectResult())
                    {
                        correctCount++;
                    }
                }

                status_StatusBarItem.Content = string.Format("{0}/{1} correct ({2}%)",
                    correctCount, myBioDataFile.BioLines.Count, Math.Round(((double)correctCount / myBioDataFile.BioLines.Count) * 100, 2));
            }
        }

        #region Background worker

        private void BGW_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                BackgroundWorker bgW = (BackgroundWorker)sender;
                string fileName = (string)e.Argument;

                BioDataFile file = new BioDataFile(fileName, bgW);

                e.Result = file;
            }
            catch
            {
                e.Result = null;
            }
        }

        private void BGW_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            status_StatusBarItem.Content = string.Format("loading ({0}%)...", e.ProgressPercentage);
        }

        private void BGW_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result != null)
            {
                myBioDataFile = (BioDataFile)e.Result;

                selectedFile_TextBox.Text = Path.GetFileName(myBioDataFile.FileName);

                UpdateData();
            }
            else
            {
                MessageBox.Show("There was a problem reading the selected file.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Events

        private void CorrectResultsOnly_CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                UpdateData();
            }
        }
        
        private void Export_Button_Click(object sender, RoutedEventArgs e)
        {
            if (myBioDataFile != null)
            {
                //can we access the source file:
                FilePreLoadState.PreLoadState preLoadState = FilePreLoadState.GetPreLoadState(myBioDataFile.FileName);

                if (preLoadState == FilePreLoadState.PreLoadState.READY)
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.Filter = "CSV file|*.csv|HTML file|*.html";

                    if (saveFileDialog.ShowDialog().Value)
                    {
                        try
                        {
                            if (myBioDataFile.Export(saveFileDialog.FileName, correctResultsOnly_CheckBox.IsChecked.Value, exportSelectedResults_RadioButton.IsChecked.Value))
                            {
                                MiscTask.OpenDirectory(this, saveFileDialog.FileName, true);
                            }
                        }
                        catch
                        {
                            MessageBox.Show("There was a problem exporting the data.", Program.Name, MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    FilePreLoadState.GetDataFilePreLoadMessage(this, preLoadState, "original");
                }
            }
        }
        
        private void Export_CheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool export = ((CheckBox)sender).IsChecked.Value;

            for (int i = 0; i < results_ListView.SelectedItems.Count; i++)
            {
                ((BioDataFile.BioLine)results_ListView.SelectedItems[i]).Export = export;
            }
        }

        private void HighlightData_CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
            {
                highlightData = highlightData_CheckBox.IsChecked.Value;

                UpdateData();
            }
        }
        
        private void HighlightInBold_CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded && highlightData_CheckBox.IsChecked.Value)
            {
                highlightInBold = highlightInBold_CheckBox.IsChecked.Value;

                UpdateData();
            }
        }

        private void IncludeData_CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)           
            {
                includeData = includeData_CheckBox.IsChecked.Value;

                UpdateData();
            }
        }
        
        private void Results_ListView_Drop(object sender, DragEventArgs e)
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

                    LoadFile(itemsDropped[0]);
                }
            }));
        }

        private void SelectFile_Button_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFile = new OpenFileDialog();
            openFile.Filter = "CSV files|*.csv";
            openFile.Multiselect = false;

            if (openFile.ShowDialog().Value)
            {
                LoadFile(openFile.FileName);
            }
        }

        #endregion

        #region Accessor methods

        public static bool HighlightData
        {
            get { return highlightData; }
        }

        public static bool HighlightInBold
        {
            get { return highlightInBold; }
        }

        public static bool IncludeData
        {
            get { return includeData; }
        }

        #endregion
    }

    public class BioDataFile
    {
        #region Variables

        private string fileName = "";
        private List<BioLine> myBioLines = new List<BioLine>();

        #endregion

        public BioDataFile(string fileName, BackgroundWorker bgW)
        {
            this.fileName = fileName;

            using (StreamReader sR = new StreamReader(fileName))
            {
                //read the header:
                string[] header = sR.ReadLine().ToLower().Split(',');

                BioColumnData columnData = new BioColumnData(header);

                while (!sR.EndOfStream)
                {
                    string currentLine = sR.ReadLine();

                    if (!currentLine.Equals(""))
                    {
                        myBioLines.Add(new BioLine(currentLine.Split(','), columnData));
                    }

                    //what percentage progress are we on:
                    double percentage = ((double)sR.BaseStream.Position / sR.BaseStream.Length) * 100;

                    bgW.ReportProgress((int)percentage);
                }

                sR.Close();
            }
        }

        public bool Export(string outputFileName, bool correctDataOnly, bool selectedOnly)
        {
            if (Path.GetExtension(outputFileName).Equals(".html"))
            {
                return ExportHTML(outputFileName, correctDataOnly, selectedOnly);
            }
            else
            {
                return ExportCSV(outputFileName, correctDataOnly, selectedOnly);
            }
        }

        private bool ExportCSV(string outputFileName, bool correctDataOnly, bool selectedOnly)
        {
            bool exported = false;

            //read and write the file at the same time:
            using (StreamReader sR = new StreamReader(fileName))
            {
                int currentLine = 0;

                using (StreamWriter sW = new StreamWriter(outputFileName))
                {
                    //read and write the header:
                    sW.Write(sR.ReadLine());
                    sW.WriteLine(",CORRECT PRIMERS");

                    while (!sR.EndOfStream)
                    {
                        string text = sR.ReadLine();

                        if ((!correctDataOnly || myBioLines[currentLine].HasCorrectResult())
                            && (!selectedOnly || myBioLines[currentLine].Export))
                        {
                            sW.Write(text);
                            sW.WriteLine("," + myBioLines[currentLine].CorrectPrimerNames.Replace(", ", " + "));

                            exported = true;
                        }

                        currentLine++;
                    }

                    sW.Close();
                }

                sR.Close();
            }

            return exported;
        }

        private bool ExportHTML(string outputFileName, bool correctDataOnly, bool selectedOnly)
        {
            bool exported = false;

            using (StreamWriter sW = new StreamWriter(outputFileName))
            {
                sW.WriteLine("<!DOCTYPE html>");
                sW.WriteLine("<html>");
                sW.WriteLine("<body>");

                StringBuilder sB = new StringBuilder();

                for (int i = 0; i < myBioLines.Count; i++)
                {
                    if ((!correctDataOnly || myBioLines[i].HasCorrectResult())
                        && (!selectedOnly || myBioLines[i].Export))
                    {
                        sB.Append(myBioLines[i].GetHTML());

                        exported = true;
                    }
                }

                sW.WriteLine(sB.ToString());

                sW.WriteLine("</body>");
                sW.WriteLine("</html>");

                sW.Close();
            }

            return exported;
        }

        public static bool IsBioDataFile(string fileName)
        {
            bool isFinalResultsFile = false;

            try
            {
                //find out what stage of the process this file is at:
                using (StreamReader sR = new StreamReader(fileName))
                {
                    string currentLine = "";

                    while (!sR.EndOfStream && currentLine.Equals(""))
                    {
                        currentLine = sR.ReadLine();
                    }

                    //this should be the header:
                    isFinalResultsFile = new BioColumnData(currentLine.ToLower().Split(',')).IsValid();

                    sR.Close();
                }
            }
            catch { }

            return isFinalResultsFile;
        }

        #region Accessor methods

        public List<BioLine> BioLines
        {
            get { return myBioLines; }
        }

        public string FileName
        {
            get { return fileName; }
        }

        #endregion

        #region Support classes

        public class BioColumnData
        {
            #region Variables

            private int cIndex_ID = -1, cIndex_SSR = -1, cIndex_Template = -1;
            private List<int> cIndex_PrimerForward = new List<int>(), cIndex_PrimerReverse = new List<int>();

            #endregion

            public BioColumnData(string[] fileHeader)
            {
                cIndex_ID = GetColumn(fileHeader, 0, "id", true);
                cIndex_SSR = GetColumn(fileHeader, 0, "ssr", true);
                cIndex_Template = GetColumn(fileHeader, 7, "template", false);

                int primerColumn_Forward = 0;

                while (primerColumn_Forward != -1)
                {
                    primerColumn_Forward = GetColumn(fileHeader, primerColumn_Forward, "forward", false);

                    if (primerColumn_Forward != -1)
                    {
                        //is there a corresponding reverse primer:
                        int primerColumn_Reverse = GetColumn(fileHeader, primerColumn_Forward, "reverse", false);

                        //we'll only allow primers if they have a reverse primer as well:
                        if (primerColumn_Reverse != -1)
                        {
                            cIndex_PrimerForward.Add(primerColumn_Forward);
                            cIndex_PrimerReverse.Add(primerColumn_Reverse);

                            //shift the search start position for the next primer:
                            primerColumn_Forward = primerColumn_Reverse + 1;
                        }
                    }
                }
            }

            private int GetColumn(string[] header, int startIndex, string searchText, bool fixedLength)
            {
                for (int i = startIndex; i < header.Length; i++)
                {
                    if (fixedLength)
                    {
                        if (header[i].Contains(searchText) && header[i].Length == searchText.Length)
                        {
                            return i;
                        }
                    }
                    else
                    {
                        if (header[i].StartsWith(searchText))
                        {
                            return i;
                        }
                    }
                }

                return -1;
            }

            public bool IsValid()
            {
                return cIndex_ID != -1
                    && cIndex_SSR != -1
                    && cIndex_Template != -1
                    && cIndex_PrimerForward.Count > 0;
            }

            #region Accessor methods

            public int CIndex_ID
            {
                get { return cIndex_ID; }
            }

            public List<int> CIndex_PrimerForward
            {
                get { return cIndex_PrimerForward; }
            }

            public List<int> CIndex_PrimerReverse
            {
                get { return cIndex_PrimerReverse; }
            }

            public int CIndex_SSR
            {
                get { return cIndex_SSR; }
            }

            public int CIndex_Template
            {
                get { return cIndex_Template; }
            }

            #endregion
        }

        public class BioLine : INotifyPropertyChanged
        {
            #region Variables

            private string id = "", details = "", ssR_Brief = "", ssR = "", template = "";
            private bool export = false;
            private List<Primer> myPrimers = new List<Primer>();
            private Primer currentPrimer;

            #endregion

            public BioLine(string[] text, BioColumnData columnData)
            {
                details = text[0];
                id = text[columnData.CIndex_ID];

                ssR_Brief = text[columnData.CIndex_SSR].Replace("*", "");
                ssR = GetFullSSR(ssR_Brief);

                for (int i = 0; i < columnData.CIndex_PrimerForward.Count; i++)
                {
                    myPrimers.Add(new Primer(myPrimers.Count + 1, 
                        text[columnData.CIndex_PrimerForward[i]], text[columnData.CIndex_PrimerReverse[i]]));
                }

                template = text[columnData.CIndex_Template].ToUpper();

                //calculate results for all the primers:
                for (int i = 0; i < myPrimers.Count; i++)
                {
                    myPrimers[i].CalculateResults(ssR, template);

                    if (currentPrimer == null && myPrimers[i].IsCorrect)
                    {
                        currentPrimer = myPrimers[i];
                    }
                }

                if (currentPrimer == null)
                {
                    currentPrimer = myPrimers[0];
                }
            }

            public void CopyToClipboard()
            {
                StringBuilder sB_Plain = new StringBuilder();

                sB_Plain.AppendLine(string.Format("ID: {0}", id));
                sB_Plain.AppendLine(string.Format("SSR (brief): {0}", ssR_Brief));
                sB_Plain.AppendLine(string.Format("SSR (full): {0}", ssR));

                sB_Plain.AppendLine(string.Format("Correct primers: {0}", GetCorrectPrimersString()));

                sB_Plain.AppendLine(string.Format("Has overlap: {0}", currentPrimer.HasOverlapString));
                sB_Plain.AppendLine(string.Format("Template ({0} highlighted): {1}", currentPrimer.Name, currentPrimer.GetPlainText(template)));

                ClipboardHelper.CopyToClipboard(GetHTML(), sB_Plain.ToString());
            }

            private string GetCorrectPrimersString()
            {
                int correctPrimers = CorrectPrimers;
                
                return string.Format("{0}{1}", correctPrimers, correctPrimers == 0 ? "" : string.Format(" ({0} {1})", MiscTask.Pluraliser("primer", correctPrimers), CorrectPrimerNames));
            }

            private string GetFullSSR(string ssrBrief)
            {
                string result = "";

                int endIndex = -1;

                while ((endIndex = ssrBrief.IndexOf(")")) != -1)
                {
                    string dataSection = ssrBrief.Substring(0, endIndex).Replace("(", "");

                    endIndex++;

                    //the number to repeat by:
                    int numberEndIndex = ssrBrief.IndexOf("(", endIndex);

                    //if we're on the last section there won't be another bracket:
                    if (numberEndIndex == -1)
                    {
                        numberEndIndex = ssrBrief.Length;
                    }

                    int number = Convert.ToInt32(ssrBrief.Substring(endIndex, numberEndIndex - endIndex));

                    for (int i = 0; i < number; i++)
                    {
                        result = result + dataSection;
                    }

                    //cut this used section out:
                    ssrBrief = ssrBrief.Substring(numberEndIndex);
                }

                return result;
            }

            public string GetHTML()
            {
                StringBuilder sB = new StringBuilder();

                sB.AppendLine("<head>");
                sB.AppendLine("<basefont face=\"arial\" size=\"2\">");
                sB.AppendLine("</head>");

                sB.AppendLine(string.Format("<p><b>ID:</b> {0}</p>", id));
                sB.AppendLine(string.Format("<p><b>SSR (brief):</b> {0}</p>", ssR_Brief));
                sB.AppendLine(string.Format("<p><b>SSR (full):</b> {0}</p>", ssR));

                int correctPrimers = CorrectPrimers;

                sB.AppendLine(string.Format("<p><b>Correct primers:</b> {0}</p>", GetCorrectPrimersString()));
                
                sB.AppendLine(string.Format("<p><b>Has overlap:</b> {0}</p>", currentPrimer.HasOverlapString));
                sB.AppendLine(string.Format("<p><b>Template ({0} highlighted):</b> {1}</p>", currentPrimer.Name, currentPrimer.GetHTML(template)));

                return sB.ToString();
            }

            private bool HasResults()
            {
                for (int i = 0; i < myPrimers.Count; i++)
                {
                    if (myPrimers[i].HasResults())
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool HasCorrectResult()
            {
                for (int i = 0; i < myPrimers.Count; i++)
                {
                    if (myPrimers[i].IsCorrect)
                    {
                        return true;
                    }
                }

                return false;
            }

            #region Notify property changed

            private void NotifyPropertyChanged(string propertyName)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            #endregion

            #region Accessor methods

            public int CorrectPrimers
            {
                get
                {
                    int result = 0;

                    for (int i = 0; i < myPrimers.Count; i++)
                    {
                        if (myPrimers[i].IsCorrect)
                        {
                            result++;
                        }
                    }

                    return result;
                }
            }

            public string CorrectPrimerNames
            {
                get
                {
                    string output = "";

                    for (int i = 0; i < myPrimers.Count; i++)
                    {
                        if (myPrimers[i].IsCorrect)
                        {
                            if (!output.Equals(""))
                            {
                                output = output + ", ";
                            }

                            output = output + (i + 1);
                        }
                    }
                    
                    return output;
                }
            }

            public Primer CurrentPrimer
            {
                get { return currentPrimer; }
                set
                {
                    currentPrimer = value;

                    NotifyPropertyChanged("CurrentPrimer");
                    NotifyPropertyChanged("RichOutput");
                }
            }

            public string Details
            {
                get { return details; }
            }

            public bool Export
            {
                get { return export; }
                set
                {
                    export = value;

                    NotifyPropertyChanged("Export");
                }
            }

            public string ID
            {
                get { return id; }
            }

            public List<Primer> MyPrimers
            {
                get { return myPrimers; }
            }

            public List<Inline> RichOutput
            {
                get
                {
                    if (Window_Results.IncludeData)
                    {
                        return currentPrimer.GetInlineResults(template, Window_Results.HighlightData, Window_Results.HighlightInBold);
                    }
                    else
                    {
                        return new List<Inline>();
                    }
                }
            }

            public string SSR_Brief
            {
                get { return ssR_Brief; }
            }

            #endregion

            #region Support classes

            public class IndexResult : IComparable
            {
                #region Variables

                private int index = 0, length = 0;
                private DataType myDataType;

                private static SolidColorBrush brushForward;
                private static SolidColorBrush brushReverse;
                private static SolidColorBrush brushSSR;

                #endregion

                public IndexResult(DataType dataType, string templateText, string searchText)
                {
                    myDataType = dataType;

                    index = templateText.IndexOf(searchText);
                    length = searchText.Length;
                }

                public int CompareTo(object obj)
                {
                    if (obj is IndexResult)
                    {
                        IndexResult iR = (IndexResult)obj;

                        return index.CompareTo(iR.index);
                    }

                    return 0;
                }

                public static void CreateBrushes()
                {
                    brushForward = new SolidColorBrush(Colors.Green);

                    if (brushForward.CanFreeze)
                    {
                        brushForward.Freeze();
                    }

                    brushReverse = new SolidColorBrush(Colors.Blue);

                    if (brushReverse.CanFreeze)
                    {
                        brushReverse.Freeze();
                    }

                    brushSSR = new SolidColorBrush(Colors.Red);

                    if (brushSSR.CanFreeze)
                    {
                        brushSSR.Freeze();
                    }
                }

                public bool IsValid()
                {
                    return index != -1;
                }

                #region Accessor methods

                public SolidColorBrush Brush
                {
                    get
                    {
                        switch (myDataType)
                        {
                            case DataType.FORWARD: return brushForward;
                            case DataType.REVERSE_COMPLIMENT: return brushReverse;
                            default: return brushSSR;
                        }
                    }
                }

                public int Index
                {
                    get { return index; }
                }

                public int Length
                {
                    get { return length; }
                }

                public DataType MyDataType
                {
                    get { return myDataType; }
                }

                //public string Name
                //{
                //    get
                //    {
                //        switch (myDataType)
                //        {
                //            case Primer.PrimerType.FORWARD: return "Forward";
                //            case Primer.PrimerType.REVERSE: return "Reverse";
                //            default: return "Reverse compliment";
                //        }
                //    }
                //}

                #endregion

                public enum DataType
                {
                    FORWARD, REVERSE_COMPLIMENT, SSR
                }
            }

            public class Primer
            {
                #region Variables

                private int id = 0;
                private string forward = "", reverse = "", reverseCompliment = "";
                private List<IndexResult> myResults = new List<IndexResult>(3);

                #endregion

                public Primer(int id, string forward, string reverse)
                {
                    this.id = id;
                    this.forward = forward.ToUpper();
                    this.reverse = reverse.ToUpper();
                    reverseCompliment = CalculateReverseCompliment();
                }

                public void CalculateResults(string ssR, string template)
                {
                    //calculate the results:
                    myResults.Add(new IndexResult(IndexResult.DataType.FORWARD, template, forward));
                    myResults.Add(new IndexResult(IndexResult.DataType.REVERSE_COMPLIMENT, template, reverseCompliment));
                    myResults.Add(new IndexResult(IndexResult.DataType.SSR, template, ssR));

                    myResults.Sort();
                }

                private string CalculateReverseCompliment()
                {
                    char[] flippedReverse = new char[reverse.Length];

                    for (int i = reverse.Length - 1, output = 0; i >= 0; i--, output++)
                    {
                        flippedReverse[output] = reverse[i];
                    }

                    //now get the compliment:
                    string result = "";

                    for (int i = 0; i < flippedReverse.Length; i++)
                    {
                        char compliment = 'X';

                        switch (flippedReverse[i])
                        {
                            case 'A': compliment = 'T'; break;
                            case 'C': compliment = 'G'; break;
                            case 'G': compliment = 'C'; break;
                            case 'T': compliment = 'A'; break;
                            default: break;
                        }

                        result = result + compliment;
                    }

                    return result;
                }

                public string GetHTML(string template)
                {
                    List<Inline> results = GetInlineResults(template, true, false);

                    StringBuilder sB = new StringBuilder();

                    for (int i = 0; i < results.Count; i++)
                    {
                        Run r = (Run)results[i];
                        string colourName = ((SolidColorBrush)r.Foreground).Color.GetColorName();

                        if (!colourName.Equals("Black"))
                        {
                            sB.Append(string.Format("<span style=\"color:{0}\"><b>{1}</b></span>", colourName, r.Text));
                        }
                        else
                        {
                            sB.Append(string.Format("{0}", r.Text));
                        }
                    }

                    return sB.ToString();
                }

                public List<Inline> GetInlineResults(string template, bool highlightData, bool highlightsInBold)
                {
                    List<Inline> results = new List<Inline>();
                    
                    if (highlightData)
                    {
                        int lastIndex = 0;

                        for (int i = 0; i < myResults.Count; i++)
                        {
                            if (myResults[i].IsValid())
                            {
                                int length = myResults[i].Index - lastIndex;

                                if (length >= 0)
                                {
                                    //write the text up to the start of this result:
                                    results.Add(new Run(template.Substring(lastIndex, length)));

                                    //at which index will data from this result end:
                                    lastIndex = myResults[i].Index + myResults[i].Length;

                                    //currently we're going to output the full length as a single colour:
                                    int myOutputLength = myResults[i].Length;

                                    //do any of the forthcoming results overlap with this one:
                                    for (int j = i + 1; j < myResults.Count; j++)
                                    {
                                        if (myResults[j].IsValid() && myResults[j].Index < lastIndex)
                                        {
                                            myOutputLength = myResults[j].Index - myResults[i].Index;

                                            //so the last index has changed now too:
                                            lastIndex = myResults[j].Index;

                                            break;
                                        }
                                    }

                                    //now the highlighted text:
                                    Run highlightedData = new Run(template.Substring(myResults[i].Index, myOutputLength));
                                    highlightedData.Foreground = myResults[i].Brush;

                                    if (highlightsInBold)
                                    {
                                        highlightedData.FontWeight = FontWeights.Bold;
                                    }

                                    results.Add(highlightedData);
                                }
                            }
                        }

                        //how much data remains:
                        int remainingLength = template.Length - lastIndex;

                        if (remainingLength > 0)
                        {
                            //the rest of the data:
                            results.Add(new Run(template.Substring(lastIndex, remainingLength)));
                        }
                    }
                    else
                    {
                        results.Add(new Run(template));
                    }

                    return results;
                }

                public string GetPlainText(string template)
                {
                    List<Inline> results = GetInlineResults(template, true, false);

                    StringBuilder sB = new StringBuilder();

                    for (int i = 0; i < results.Count; i++)
                    {
                        Run r = (Run)results[i];

                        sB.Append(r.Text);

                        if (i < results.Count - 1)
                        {
                            sB.Append(" ");
                        }
                    }

                    return sB.ToString();
                }

                private bool HasOverlap()
                {
                    for (int i = 0; i < myResults.Count - 1; i++)
                    {
                        if (myResults[i].IsValid())
                        {
                            //what's the end index of this result:
                            int endIndex = myResults[i].Index + myResults[i].Length;

                            //find the next primer:
                            for (int j = i + 1; j < myResults.Count; j++)
                            {
                                if (myResults[j].IsValid() && endIndex > myResults[j].Index)
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    return false;
                }

                public bool HasResults()
                {
                    for (int i = 0; i < myResults.Count; i++)
                    {
                        if (myResults[i].IsValid())
                        {
                            return true;
                        }
                    }

                    return false;
                }

                #region Accessor methods

                public string Forward
                {
                    get { return forward; }
                }

                public string HasOverlapString
                {
                    get { return HasOverlap() ? "Yes" : "No"; }
                }

                public bool IsCorrect
                {
                    get 
                    {
                        if (!HasOverlap() && myResults.Count == 3)
                        {
                            return myResults[0].MyDataType == IndexResult.DataType.FORWARD
                                && myResults[1].MyDataType == IndexResult.DataType.SSR
                                && myResults[2].MyDataType == IndexResult.DataType.REVERSE_COMPLIMENT;
                        }

                        return false;
                    }
                }

                public string Name
                {
                    get { return string.Format("Primer {0}", id); }
                }

                public string Reverse
                {
                    get { return reverse; }
                }

                public string ReverseCompliment
                {
                    get { return reverseCompliment; }
                }
                
                #endregion
            }

            #endregion
        }
        
        #endregion
    }
}