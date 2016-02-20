using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PrimerPipeline
{
    public class DNA_Trimmer
    {
        #region Variables

        private string fileName = "";

        private Settings mySettings = null;
        
        private StreamWriter sW_TrimmedFile = null;

        private int sequencesRead = 0, sequencesAfterTrimming = 0;

        private MicrosatelliteCalculator misaCalculator = null;

        private const int ProgressUpdateSequenceInterval = 50;
        public event EventHandler<CurrentResultsEventArgs> TriggerProgressDisplayUpdate;

        #endregion

        public DNA_Trimmer(string fileName, DNA_Trimmer.Settings trimSettings, MicrosatelliteCalculator.Settings misaSettings, bool p3In_IncludeThermodynamicParameters)
        {
            this.fileName = fileName;

            mySettings = trimSettings;

            //initialise the microsatellite calculator:
            misaCalculator = new MicrosatelliteCalculator(fileName, misaSettings, p3In_IncludeThermodynamicParameters);
        }

        private void CompleteProcess()
        {
            //close the file writers. If they are not initialised then no data has been written:
            if (sW_TrimmedFile != null)
            {
                sW_TrimmedFile.Close();
                sW_TrimmedFile.Dispose();
                sW_TrimmedFile = null;
            }

            misaCalculator.CompleteProcess();
        }

        public string GetOutputFileName_TrimmedFile()
        {
            return GetTrimmedFileName(fileName);
        }

        public static string GetTrimmedFileName(string fileName)
        {
            return string.Format("{0}.trimmedContig", MiscTask.GetBasePath(fileName));
        }

        private void ProcessSequence(string sequenceID, string sequenceData, bool finalSequence = false)
        {
            sequencesRead++;

            if (!sequenceData.Equals(""))
            {
                //1. The first step is to trim the data according to user specified arguments:

                //make the sequence upper case here to avoid each trim argument having to do so:
                sequenceData = sequenceData.ToUpper();

                //in the script, they first substitute anything that's not ACGTN with N (why would there be anything else?), and
                //also remove numbers and whitespace (Note: In this case Regex was faster than C# methods):
                sequenceData = Regex.Replace(sequenceData, "[\\d\\s>]", "");
                sequenceData = Regex.Replace(sequenceData, "[^ACGTN]", "N");

                //now process according to the arguments:
                for (int i = 0; i < mySettings.Arguments.Count; i++)
                {
                    if (!sequenceData.Equals(""))
                    {
                        sequenceData = mySettings.Arguments[i].Trim(sequenceData);
                    }
                }

                //check the length again, in case it was trimmed:
                if (!sequenceData.Equals(""))
                {
                    sequencesAfterTrimming++;

                    //remove spaces from the sequence ID:
                    sequenceID = sequenceID.Replace(" ", "_");

                    //only output if the user has indicated that they want to do this:
                    if (mySettings.ExportTrimmedFile)
                    {
                        //we'll only create a file for the trimmed data if there is something to go in it, as there
                        //is now:
                        if (sW_TrimmedFile == null)
                        {
                            sW_TrimmedFile = new StreamWriter(GetOutputFileName_TrimmedFile());
                        }

                        //this line does the equivalent of the separate replace spaces command that was being run:
                        sW_TrimmedFile.WriteLine(sequenceID);

                        //write the sequence in lines of 70 characters:
                        int sequenceLength = sequenceData.Length;
                        int chunkSize = 70;

                        for (int i = 0; i < sequenceLength; i += chunkSize)
                        {
                            if (i + chunkSize > sequenceLength)
                            {
                                chunkSize = sequenceLength - i;
                            }

                            sW_TrimmedFile.WriteLine(sequenceData.Substring(i, chunkSize));
                        }
                    }

                    //2. The second step is to identify microsatellites:

                    //regardless of the export we can use this data to identify microsatellites:
                    misaCalculator.Calculate(sequenceID, sequenceData);
                }
            }

            if (sequencesRead % ProgressUpdateSequenceInterval == 0 || finalSequence)
            {
                UpdateRunningOutput();
            }
        }

        public void Process(BackgroundWorker bgW, Progress progress)
        {
            progress.AssignCurrentTask("Searching for microsatellites");

            using (StreamReader sR = new StreamReader(fileName))
            {
                try
                {
                    //save the trim settings so the user has a record of them:
                    SaveTrimAndMisaSettings();

                    //we'll keep track of the number of lines read so that we can report on progress every so often:
                    int linesRead = 0;

                    string currentSequence = "";
                    string sequenceName = "";

                    while (!sR.EndOfStream && !bgW.CancellationPending && !progress.MoveToNextTaskRequested)
                    {
                        string currentLine = sR.ReadLine();
                        linesRead++;

                        //assemble the entire sequence before processing:
                        if (currentLine.StartsWith(">"))
                        {
                            //process according to the arguments (the method checks that currentSequence is not empty):
                            ProcessSequence(sequenceName, currentSequence);

                            //prepare for the next sequence:
                            sequenceName = currentLine;
                            currentSequence = "";
                        }
                        else
                        {
                            currentSequence = currentSequence + currentLine;
                        }

                        //update the progress:
                        if (linesRead % 1000 == 0)
                        {
                            progress.ApplyProgressFraction((double)sR.BaseStream.Position / sR.BaseStream.Length);
                            bgW.ReportProgress(0, progress);
                        }
                    }

                    if (!bgW.CancellationPending)
                    {
                        //process according to the arguments (the method checks that currentSequence is not empty):
                        ProcessSequence(sequenceName, currentSequence, true);

                        //update the progress:
                        progress.CurrentTaskComplete();
                        bgW.ReportProgress(0, progress);
                    }
                }
                catch { }
                finally
                {
                    CompleteProcess();

                    sR.Close();
                }
            }
        }

        private void SaveTrimAndMisaSettings()
        {
            string basePath = MiscTask.GetBasePath(fileName);

            mySettings.SaveForFile(basePath);
            misaCalculator.SaveSettingsForFile(basePath);
        }

        private void UpdateRunningOutput()
        {
            if (TriggerProgressDisplayUpdate != null)
            {
                TriggerProgressDisplayUpdate(this, new CurrentResultsEventArgs(sequencesRead, misaCalculator.Stats));
            }
        }

        #region Accessor methods

        public MicrosatelliteCalculator MISA
        {
            get { return misaCalculator; }
        }

        public int TotalSequencesRead
        {
            get { return sequencesRead; }
        }

        #endregion

        #region Support classes

        public abstract class TrimArgument : INotifyPropertyChanged
        {
            protected abstract string GetDescription();

            public abstract void Save(StreamWriter sW);

            public abstract string Trim(string sequence);

            #region INotifyPropertyChanged Members

            public event PropertyChangedEventHandler PropertyChanged;

            protected void NotifyPropertyChanged(string info)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(info));
                }
            }

            #endregion

            #region Accessor methods

            public string Description
            {
                get { return GetDescription(); }
            }

            #endregion
        }

        public class AmbiguousTrimArgument : TrimArgument
        {
            #region Variables

            private int numberOfBases = 0, windowSize = 0;

            public const string ExportPrefix = "AMB:";

            #endregion

            public AmbiguousTrimArgument(int numberOfBases, int windowSize)
            {
                this.numberOfBases = numberOfBases;
                this.windowSize = windowSize;
            }

            public AmbiguousTrimArgument(string argument)
            {
                string args = argument.Replace(ExportPrefix.ToLower(), "").Trim();

                string[] splits = args.Split(',');

                numberOfBases = Convert.ToInt32(splits[0]);
                windowSize = Convert.ToInt32(splits[1]);
            }

            protected override string GetDescription()
            {
                return string.Format("Ambiguous trim. Number of bases = {0}, window size = {1}.", numberOfBases, windowSize);
            }

            public override string Trim(string sequence)
            {
                //sequence = Trim_5N(sequence);
                //sequence = Trim_3N(sequence);

                //on tests, the C# way was 4X faster than the Regex way:
                sequence = Trim_5N_CSharp(sequence);
                sequence = Trim_3N_CSharp(sequence);

                return sequence;
            }

            #region C#

            private string Trim_3N_CSharp(string sequence)
            {
                //NOTE: All chars are converted to uppercase, to ensure that they are easily comparable.

                while (sequence.Length > windowSize)
                {
                    string windowedSequence = sequence.Substring(sequence.Length - windowSize);

                    //what's the last character:
                    char lastChar = windowedSequence[windowedSequence.Length - 1];

                    //only continue if this is N:
                    if (lastChar.Equals('N'))
                    {
                        //how many times does this occur in a row:
                        int sequentialCount = 1;

                        for (int i = windowedSequence.Length - 2; i >= 0; i--)
                        {
                            if (windowedSequence[i].Equals(lastChar))
                            {
                                sequentialCount++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (sequentialCount >= numberOfBases)
                        {
                            sequence = sequence.Remove(sequence.Length - sequentialCount);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                return sequence;
            }

            private string Trim_5N_CSharp(string sequence)
            {
                while (sequence.Length > windowSize)
                {
                    string windowedSequence = sequence.Substring(0, windowSize);

                    //what's the first character:
                    char firstChar = windowedSequence[0];

                    //only continue if this is N:
                    if (firstChar.Equals('N'))
                    {
                        //how many times does this occur in a row:
                        int sequentialCount = 1;

                        for (int i = 1; i < windowedSequence.Length; i++)
                        {
                            if (windowedSequence[i].Equals(firstChar))
                            {
                                sequentialCount++;
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (sequentialCount >= numberOfBases)
                        {
                            sequence = sequence.Remove(0, sequentialCount);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                return sequence;
            }

            #endregion
            
            #region Regex

            private string Trim_3N(string sequence)
            {
                //([^N])\\1 finds the repeating character and assigns it to 1. The {{{0}}} bit then looks for the number of repeats, and
                //the $ tells it to do all this at the end of the windowed sequence. As we already found the character with the first 
                //bit, we need to subtract 1 from numberOfBases:

                //this matches numberOfBases or more times. The source Perl script implied this expression in its summary:
                Regex expression = new Regex(string.Format("([N])\\1{{{0},}}$", numberOfBases - 1), RegexOptions.IgnoreCase);

                while (sequence.Length > windowSize)
                {
                    string windowedSequence = sequence.Substring(sequence.Length - windowSize);

                    //if there are any stretches of a particular base in the windowed sequence that repeat
                    //numberOfBases times, get rid of them:
                    Match result = expression.Match(windowedSequence);

                    if (result.Success)//something to work out
                    {
                        //remove the matching sequence from the end of the string:
                        sequence = Regex.Replace(sequence, string.Format("{0}$", result.Value), "", RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        break;
                    }
                }

                return sequence;
            }

            private string Trim_5N(string sequence)
            {
                //([^N])\\1 finds the repeating character and assigns it to 1. The {{{0}}} bit then looks for the number of repeats, and
                //the ^ at the start tells it to do all of this at the very start of the string. As we already found the character with 
                //the first bit, we need to subtract 1 from numberOfBases:

                //this matches numberOfBases or more times. The source Perl script implied this expression in its summary:
                Regex expression = new Regex(string.Format("^([N])\\1{{{0},}}", numberOfBases - 1), RegexOptions.IgnoreCase);

                while (sequence.Length > windowSize)
                {
                    string windowedSequence = sequence.Substring(0, windowSize);
                    
                    //if there are any stretches of a particular base in the windowed sequence that repeat
                    //numberOfBases times, get rid of them:
                    Match result = expression.Match(windowedSequence);

                    if (result.Success)
                    {
                        //remove the matching sequence from the start of the string:
                        sequence = Regex.Replace(sequence, string.Format("^{0}", result.Value), "", RegexOptions.IgnoreCase);                        
                    }
                    else
                    {
                        break;
                    }
                }

                return sequence;
            }

            #endregion

            #region Load / save

            public override void Save(StreamWriter sW)
            {
                sW.WriteLine("{0} {1},{2}", ExportPrefix, numberOfBases, windowSize);
            }

            #endregion

            #region Accessor methods

            public int NumberOfBases
            {
                get { return numberOfBases; }
            }

            public int WindowSize
            {
                get { return windowSize; }
            }

            #endregion
        }

        public class CutOffTrimArgument : TrimArgument
        {
            #region Variables

            private int minValue = 0, maxSequenceSize = 0;

            public const string ExportPrefix = "CUT:";
            
            #endregion

            public CutOffTrimArgument(int minValue, int maxSequenceSize)
            {
                this.minValue = minValue;
                this.maxSequenceSize = maxSequenceSize;
            }

            public CutOffTrimArgument(string argument)
            {
                string args = argument.Replace(ExportPrefix.ToLower(), "").Trim();

                string[] splits = args.Split(',');

                minValue = Convert.ToInt32(splits[0]);
                maxSequenceSize = Convert.ToInt32(splits[1]);
            }

            protected override string GetDescription()
            {
                return string.Format("Cut-off. Minimum value = {0}, maximum sequence size = {1}.", minValue, maxSequenceSize);
            }

            public override string Trim(string sequence)
            {
                if (sequence.Length > maxSequenceSize)
                {
                    sequence = sequence.Substring(0, maxSequenceSize);
                }

                if (sequence.Length < minValue)
                {
                    sequence = "";
                }

                return sequence;
            }

            #region Load / save

            public override void Save(StreamWriter sW)
            {
                sW.WriteLine("{0} {1},{2}", ExportPrefix, minValue, maxSequenceSize);
            }

            #endregion

            #region Accessor methods

            public int MaxSequenceSize
            {
                get { return maxSequenceSize; }
            }

            public int MinValue
            {
                get { return minValue; }
            }

            #endregion
        }

        public class StretchTypeTrimArgument : TrimArgument
        {
            #region Variables

            private bool from5End = true;
            private char characterType = 'A';

            private int minAcceptedRepeat = 1, windowSize = 0;

            public const string ExportPrefix = "STRETCH:";

            #endregion

            public StretchTypeTrimArgument(bool from5End, char characterType, int minAcceptedRepeat, int windowSize)
            {
                this.from5End = from5End;
                this.characterType = characterType;
                this.minAcceptedRepeat = minAcceptedRepeat;
                this.windowSize = windowSize;
            }

            public StretchTypeTrimArgument(string argument)
            {
                string args = argument.Replace(ExportPrefix.ToLower(), "").Trim();

                string[] splits = args.Split(',');

                from5End = splits[0].Equals("5'") ? true : false;
                characterType = char.ToUpper(splits[1][0]);
                minAcceptedRepeat = Convert.ToInt32(splits[2]);
                windowSize = Convert.ToInt32(splits[3]);
            }

            protected override string GetDescription()
            {
                return string.Format("Remove stretches of type {0} from {1}' end. Minimum accepted repeat = {2}, window size = {3}.", characterType, from5End ? "5" : "3", minAcceptedRepeat, windowSize);
            }

            public override string Trim(string sequence)
            {
                //if (from5End)
                //{
                //    sequence = Trim_5N(sequence);
                //}
                //else
                //{
                //    sequence = Trim_3N(sequence);
                //}

                if (from5End)
                {
                    sequence = Trim_5N_CSharp(sequence);
                }
                else
                {
                    sequence = Trim_3N_CSharp(sequence);
                }

                return sequence;
            }

            #region C#

            private int CountSequentialOccurrences(string sequence, int startIndex, bool forwards)
            {
                int count = 1;

                if (startIndex < 0 || startIndex > sequence.Length - 1)
                {
                    return count;
                }

                if (forwards)
                {
                    for (int i = startIndex; i < sequence.Length; i++)
                    {
                        if (sequence[i].Equals(characterType))
                        {
                            count++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = startIndex; i >= 0; i--)
                    {
                        if (sequence[i].Equals(characterType))
                        {
                            count++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return count;
            }

            private string Trim_3N_CSharp(string sequence)
            {
                while (sequence.Length > 0)
                {
                    string windowedSequence = sequence;

                    if (sequence.Length > windowSize)
                    {
                        windowedSequence = sequence.Substring(sequence.Length - windowSize);
                    }

                    //remove the character from the start:
                    if (windowedSequence[windowedSequence.Length - 1].Equals(characterType))
                    {
                        sequence = sequence.TrimEnd(characterType);
                    }
                    else
                    {
                        //how many times does this occur in a row:
                        int sequentialCount = 0, startIndex = 0;

                        for (int i = windowedSequence.Length - 1; i >= 0; i--)
                        {
                            if (windowedSequence[i].Equals(characterType))
                            {
                                //how many sequential times does the character occur:
                                sequentialCount = CountSequentialOccurrences(windowedSequence, i - 1, false);

                                //remove the stretch if it occurs enough times sequentially:
                                if (sequentialCount >= minAcceptedRepeat)
                                {
                                    startIndex = (sequence.Length - windowedSequence.Length) + (i - sequentialCount) + 1;

                                    break;
                                }
                            }
                        }

                        if (sequentialCount >= minAcceptedRepeat)
                        {
                            sequence = sequence.Remove(startIndex);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return sequence;
            }

            private string Trim_5N_CSharp(string sequence)
            {
                while (sequence.Length > 0)
                {
                    string windowedSequence = sequence;

                    if (sequence.Length > windowSize)
                    {
                        windowedSequence = sequence.Substring(0, windowSize);
                    }

                    //remove the character from the start:
                    if (windowedSequence[0].Equals(characterType))
                    {
                        sequence = sequence.TrimStart(characterType);
                    }
                    else
                    {
                        //how many times does this occur in a row:
                        int sequentialCount = 0, characterCount = 0;

                        for (int i = 0; i < windowedSequence.Length; i++)
                        {
                            if (windowedSequence[i].Equals(characterType))
                            {
                                //how many sequential times does the character occur:
                                sequentialCount = CountSequentialOccurrences(windowedSequence, i + 1, true);

                                //remove the stretch if it occurs enough times sequentially:
                                if (sequentialCount >= minAcceptedRepeat)
                                {
                                    characterCount = i + sequentialCount;

                                    break;
                                }
                            }
                        }

                        if (sequentialCount >= minAcceptedRepeat)
                        {
                            sequence = sequence.Remove(0, characterCount);
                        }
                        else
                        {
                            break;
                        }
                    }
                }                
                
                return sequence;
            }

            #endregion

            #region Regex

            private string Trim_3N(string sequence)
            {
                if (sequence[sequence.Length - 1].Equals(characterType))
                {
                    string ex = string.Format(".*?(({0}){{{1}}}.*)", characterType, minAcceptedRepeat);

                    Regex expression = new Regex(ex, RegexOptions.IgnoreCase);

                    while (true)
                    {
                        string windowedSequence = sequence;

                        if (sequence.Length > windowSize)
                        {
                            windowedSequence = sequence.Substring(sequence.Length - windowSize);
                        }

                        //if there are any stretches of a particular base in the windowed sequence that repeat
                        //numberOfBases times, get rid of them:
                        Match result = expression.Match(windowedSequence);

                        if (result.Success)
                        {
                            //remove the matching sequence from the start of the string:
                            sequence = Regex.Replace(sequence, string.Format("{0}$", result.Value), "", RegexOptions.IgnoreCase);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                return sequence;
            }

            private string Trim_5N(string sequence)
            {
                string ex = string.Format("^(.*?({0}){{{1}}})", characterType, minAcceptedRepeat);

                Regex expression = new Regex(ex, RegexOptions.IgnoreCase);

                while (sequence.Length > 0)
                {
                    string windowedSequence = sequence;

                    if (sequence.Length > windowSize)
                    {
                        windowedSequence = sequence.Substring(0, windowSize);
                    }

                    //if there are any stretches of a particular base in the windowed sequence that repeat
                    //numberOfBases times, get rid of them:
                    Match result = expression.Match(windowedSequence);

                    if (result.Success)
                    {
                        //Regex expression = new Regex(string.Format("^([N])\\1{{{0},}}", numberOfBases - 1), RegexOptions.IgnoreCase);
                        //$seq =~ s/^($1$tr5_b*)//i;

                        //remove the matching sequence from the start of the string:
                        sequence = Regex.Replace(sequence, string.Format("^(\\1{0}*)", characterType), "", RegexOptions.IgnoreCase);


                        //sequence = Regex.Replace(sequence, string.Format("^{0}*", characterType), "", RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        break;
                    }
                }                    

                return sequence;
            }

            #endregion

            #region Load / save

            public override void Save(StreamWriter sW)
            {
                sW.WriteLine("{0} {1},{2},{3},{4}", ExportPrefix, from5End ? "5'" : "3'", characterType, minAcceptedRepeat, windowSize);
            }

            #endregion

            #region Accessor methods

            public char CharacterType
            {
                get { return characterType; }
            }

            public bool From5End
            {
                get { return from5End; }
            }

            public int MinAcceptedRepeat
            {
                get { return minAcceptedRepeat; }
            }

            public int WindowSize
            {
                get { return windowSize; }
            }

            #endregion
        }

        public class Settings
        {
            #region Variables

            private const string SettingsFileName = "TrimSettings";
            private const string Extension = ".txt";

            private List<TrimArgument> myArguments = new List<TrimArgument>();
            private bool exportTrimmedFile = false;

            private const string ExportTrimSettingPrefix = "Export trimmed file:";

            #endregion

            public Settings()
            {
                ResetToDefault();

                //try and load settings from the misa.ini file:
                string settingsFileName = GetDefaultFilePath();

                if (File.Exists(settingsFileName))
                {
                    try
                    {
                        LoadFromFile(settingsFileName);
                    }
                    catch
                    {
                        //ensure all the settings are back to default:
                        ResetToDefault();
                    }
                }
            }

            public Settings(string fileName)
            {
                LoadFromFile(fileName);
            }

            public Settings(List<TrimArgument> arguments, bool exportTrimmedFile)
            {
                ApplySettings(arguments, exportTrimmedFile);
            }

            public void ApplySettings(List<TrimArgument> arguments, bool exportTrimmedFile)
            {
                myArguments.Clear();
                myArguments.AddRange(arguments);

                this.exportTrimmedFile = exportTrimmedFile;
            }

            private string GetDefaultFilePath()
            {
                return string.Format("{0}\\{1}{2}", Program.GetDirectory(), SettingsFileName, Extension);
            }

            private void LoadFromFile(string fileName)
            {
                myArguments = new List<TrimArgument>();

                using (StreamReader sR = new StreamReader(fileName))
                {
                    try
                    {
                        while (!sR.EndOfStream)
                        {
                            string currentLine = sR.ReadLine().ToLower();

                            if (!currentLine.Equals(""))
                            {
                                if (currentLine.StartsWith(ExportTrimSettingPrefix.ToLower()))
                                {
                                    currentLine = currentLine.Replace(ExportTrimSettingPrefix.ToLower(), "");

                                    exportTrimmedFile = Convert.ToBoolean(currentLine.Trim());
                                }
                                else
                                {
                                    if (currentLine.StartsWith(AmbiguousTrimArgument.ExportPrefix.ToLower()))
                                    {
                                        myArguments.Add(new AmbiguousTrimArgument(currentLine));
                                    }
                                    else if (currentLine.StartsWith(CutOffTrimArgument.ExportPrefix.ToLower()))
                                    {
                                        myArguments.Add(new CutOffTrimArgument(currentLine));
                                    }
                                    else if (currentLine.StartsWith(StretchTypeTrimArgument.ExportPrefix.ToLower()))
                                    {
                                        myArguments.Add(new StretchTypeTrimArgument(currentLine));
                                    }
                                }
                            }
                        }
                    }
                    catch { throw; }
                    finally
                    {
                        sR.Close();
                    }
                }
            }

            public void ResetToDefault()
            {
                myArguments = new List<TrimArgument>();

                //default arguments to match the test files:
                myArguments.Add(new DNA_Trimmer.AmbiguousTrimArgument(2, 200));
                myArguments.Add(new DNA_Trimmer.StretchTypeTrimArgument(true, 'A', 5, 200));
                myArguments.Add(new DNA_Trimmer.StretchTypeTrimArgument(false, 'A', 5, 200));
                myArguments.Add(new DNA_Trimmer.CutOffTrimArgument(500, 50700));

                exportTrimmedFile = false;
            }

            public void Save(string fileName)
            {
                using (StreamWriter sW = new StreamWriter(fileName))
                {
                    sW.WriteLine(string.Format("{0} {1}", ExportTrimSettingPrefix, exportTrimmedFile));

                    for (int i = 0; i < myArguments.Count; i++)
                    {
                        myArguments[i].Save(sW);
                    }

                    sW.Close();
                }
            }

            public void SaveCurrentSettings()
            {
                try
                {
                    Save(GetDefaultFilePath());
                }
                catch { }
            }

            public void SaveForFile(string basePath)
            {
                Save(string.Format("{0}_{1}{2}", basePath, SettingsFileName, Extension));
            }

            #region Accessor methods

            public List<TrimArgument> Arguments
            {
                get { return myArguments; }
            }

            public bool ExportTrimmedFile
            {
                get { return exportTrimmedFile; }
            }

            #endregion
        }

        #endregion
    }

    public class CurrentResultsEventArgs : EventArgs
    {
        public int SequencesRead { get; private set; }
        public MicrosatelliteCalculator.MisaStats MisaStats { get; private set; }

        public CurrentResultsEventArgs(int sequencesRead, MicrosatelliteCalculator.MisaStats misaStats)
        {
            SequencesRead = sequencesRead;
            MisaStats = misaStats;
        }
    }
}