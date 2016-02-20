using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PrimerPipeline
{
    public class MicrosatelliteCalculator
    {
        #region Variables

        private string fileName_Input = "", fileName_Misa = "", primerThermodynamicParametersPath = "";
        private bool primer3InputFileCreated = false;

        private StreamWriter sW_MisaFile = null, sW_P3In = null;

        //MISA settings:
        private Settings settings = null;
        
        //calculation variables:
        private int minRepeats = 1000;
        private int maxRepeats = 1;
        
        private MisaStats stats = new MisaStats();

        //Primer3 input file:
        private bool p3In_IncludeThermodynamicParameters = true;

        #endregion

        public MicrosatelliteCalculator(string fileName, Settings settings, bool p3In_IncludeThermodynamicParameters)
        {
            fileName_Input = fileName;
            this.fileName_Misa = string.Format("{0}.misa", MiscTask.GetBasePath(fileName));

            this.settings = settings;

            this.p3In_IncludeThermodynamicParameters = p3In_IncludeThermodynamicParameters;
            primerThermodynamicParametersPath = Primer3Settings.GetThermodynamicParametersPath();
        }

        private void AddToPrimer3InputFile(string sequenceID, string sequenceData, int ssrNumber, MisaResult_Output result)
        {
            if (sW_P3In == null)
            {
                sW_P3In = new StreamWriter(GetFileName_Primer3Input());

                primer3InputFileCreated = true;
            }

            sW_P3In.WriteLine(string.Format("SEQUENCE_ID={0}_{1}", sequenceID, ssrNumber));
            sW_P3In.WriteLine(string.Format("SEQUENCE_TEMPLATE={0}", sequenceData));
            sW_P3In.WriteLine(string.Format("PRIMER_PRODUCT_SIZE_RANGE={0}-{1}", 100, 280));

            //the values for the sequence target:
            int sT_Start = result.Start - 3;
            sW_P3In.WriteLine(string.Format("SEQUENCE_TARGET={0},{1}", sT_Start, sT_Start + result.SSR_Size + 6));

            sW_P3In.WriteLine(string.Format("PRIMER_MAX_END_STABILITY={0}", 250));

            if (p3In_IncludeThermodynamicParameters)
            {
                sW_P3In.WriteLine(string.Format("PRIMER_THERMODYNAMIC_PARAMETERS_PATH={0}", primerThermodynamicParametersPath));
            }
            
            sW_P3In.WriteLine("=");
        }

        public bool Calculate(string sequenceID, string sequenceData)
        {
            //we don't have the > in the Misa or Primer3 files:
            if (sequenceID.StartsWith(">"))
            {
                sequenceID = sequenceID.Substring(1);
            }

            //the stats keep track of various aspects of the MISA calculation:
            stats.AddSequence(sequenceData);

            List<MisaResult> misaResults = new List<MisaResult>();

            for (int i = 0; i < settings.Definitions.Count; i++)
            {
                int minReps = settings.Definitions[i].MinimumRepeats - 1;

                //update minRepeats:
                minRepeats = Math.Min(minRepeats, settings.Definitions[i].MinimumRepeats);

                //the search expression:
                Regex expression = new Regex(string.Format("(([ACGT]{{{0}}})\\2{{{1},}})",
                    settings.Definitions[i].UnitSize, minReps), RegexOptions.IgnoreCase);

                Match result = expression.Match(sequenceData);
                
                //search for text that meets this definition:
                while (result.Success)
                {
                    string motif = result.Groups[2].Value;

                    //reject false type motifs [e.g. (TT)6 or (ACAC)5]:
                    bool redundant = false;

                    for (float j = settings.Definitions[i].UnitSize - 1; j > 0; j--)
                    {
                        float value = (float)settings.Definitions[i].UnitSize / j - 1;

                        //make a new search expression:
                        Regex innerExpression = new Regex(string.Format("([ACGT]{{{0}}})\\1{{{1}}}", j, value), RegexOptions.IgnoreCase);

                        //having trouble with this bit:
                        //if the above expression produces a match, set redundant to true:
                        if (innerExpression.Match(motif).Success)
                        {
                            redundant = true;

                            break;
                        }
                    }

                    if (redundant)
                    {
                        //re-assign the result value:
                        result = result.NextMatch();

                        continue;
                    }

                    //if we got here then the repeat is not redundant:
                    string ssR = result.Groups[0].Value;
                    int repeats = ssR.Length / settings.Definitions[i].UnitSize;
                    int end = result.Index + 1;
                    int start = end - ssR.Length + 1;

                    start = end;
                    end = start + (ssR.Length - 1);

                    //add to the Misa results:
                    misaResults.Add(new MisaResult(1, start, end, motif, repeats));

                    //keep track of maxx repeats:
                    maxRepeats = Math.Max(maxRepeats, repeats);

                    //re-assign the result value:
                    result = result.NextMatch();
                }
            }

            //we'll only continue if we have SSR results, otherwise this sequence will not be in the Misa
            //or Primer3 files:
            if (misaResults.Count > 0)
            {
                //put the SSRs in order:
                misaResults.Sort();

                int sequenceCount = 0;
                List<MisaResult_Output> finalResults = new List<MisaResult_Output>();

                int space = settings.Interruptions + 1;

                int index = 0;

                while (index < misaResults.Count)
                {
                    if ((index == misaResults.Count - 1) || (misaResults[index + 1].Start - misaResults[index].End > space))
                    {
                        sequenceCount++;

                        string ssrType = misaResults[index].GetSSR_Type();
                        string ssrSeq = misaResults[index].GetSSRSequence();
                        int start = misaResults[index].Start;
                        int end = misaResults[index].End;

                        finalResults.Add(new MisaResult_Output(sequenceID, sequenceCount, ssrType, ssrSeq, start, end));

                        index++;
                    }
                    else
                    {
                        string ssrType = "", ssrSeq = "";

                        sequenceCount++;
                        stats.AddCompoundSSR();

                        int start = misaResults[index].Start;
                        int end = misaResults[index + 1].End;

                        if (misaResults[index + 1].Start - misaResults[index].End < 1)
                        {
                            ssrType = "c*";
                            ssrSeq = misaResults[index].GetSSRSequence() + misaResults[index + 1].GetSSRSequence();
                        }
                        else
                        {
                            string interSSR = sequenceData.Substring(misaResults[index].End, (misaResults[index + 1].Start - misaResults[index].End) - 1).ToLower();
                            ssrType = "c";
                            ssrSeq = misaResults[index].GetSSRSequence() + interSSR + misaResults[index + 1].GetSSRSequence();
                        }

                        while ((++index + 1 < misaResults.Count - 1) && (misaResults[index + 1].Start - misaResults[index].End) <= space)
                        {
                            stats.AddCompoundSSR();
                            end = misaResults[index + 1].End;

                            if (misaResults[index + 1].Start - misaResults[index].End < 1)
                            {
                                ssrType = "c*";

                                //concatenate this:
                                ssrSeq = string.Format("{0}{1}*", ssrSeq, misaResults[index + 1].GetSSRSequence());
                            }
                            else
                            {
                                string interSSR = sequenceData.Substring(misaResults[index].End, (misaResults[index + 1].Start - misaResults[index].End) - 1).ToLower();
                                ssrSeq = string.Format("{0}{1}{2}*", ssrSeq, interSSR, misaResults[index + 1].GetSSRSequence());
                            }
                        }

                        index++;

                        finalResults.Add(new MisaResult_Output(sequenceID, sequenceCount, ssrType, ssrSeq, start, end));
                    }
                }

                if (sW_MisaFile == null)
                {
                    sW_MisaFile = new StreamWriter(fileName_Misa);

                    //this is the file header:
                    sW_MisaFile.WriteLine("ID\tSSR nr.\tSSR type\tSSR\tsize\tstart\tend");
                }

                //output to the MISA and Primer3 input file:
                for (int i = 0; i < finalResults.Count; i++)
                {
                    finalResults[i].WriteToFile_Misa(sW_MisaFile);

                    //add a number to make sure it's unique:
                    AddToPrimer3InputFile(sequenceID, sequenceData, i + 1, finalResults[i]);
                }

                //keep track of the total number of SSRs:
                stats.LogResults(finalResults);

                return true;
            }
            else
            {
                return false;
            }
        }

        public void CompleteProcess()
        {
            if (settings.ExportStatsFile)
            {
                stats.Save(GetOutputFileName_StatsFile(), fileName_Input, settings);
            }

            //we only really need to know if a MISA file was created, as this is needed to continue with
            //the next stage in the overall process:
            if (sW_MisaFile != null)
            {
                sW_MisaFile.Close();
                sW_MisaFile.Dispose();
                sW_MisaFile = null;
            }

            if (sW_P3In != null)
            {
                sW_P3In.Close();
                sW_P3In.Dispose();
                sW_P3In = null;
            }
        }

        public string GetFileName_MISA()
        {
            return fileName_Misa;
        }

        public string GetFileName_Primer3Input()
        {
            return string.Format("{0}{1}", MiscTask.GetBasePath(fileName_Misa), Primer3Settings.Extension_P3In);
        }

        private string GetOutputFileName_StatsFile()
        {
            return string.Format("{0}.misa_statistics", MiscTask.GetBasePath(fileName_Misa));
        }

        public bool MisaFileCreated()
        {
            return stats.SequencesFound();
        }

        public bool Primer3InputFileCreated()
        {
            return primer3InputFileCreated;
        }

        public void SaveSettingsForFile(string basePath)
        {
            settings.SaveForFile(basePath);
        }

        #region Accessor methods

        public MisaStats Stats
        {
            get { return stats; }
        }

        #endregion

        #region Support classes

        public class MisaResult : IComparable
        {
            #region Variables

            private int number, start, end, numRepeats;
            private string motif;

            #endregion

            public MisaResult(int number, int start, int end, string motif, int numRepeats)
            {
                this.number = number;
                this.start = start;
                this.end = end;
                this.motif = motif;
                this.numRepeats = numRepeats;
            }

            public int CompareTo(object obj)
            {
                if (obj is MisaResult)
                {
                    MisaResult m = (MisaResult)obj;

                    return start.CompareTo(m.start);
                }
                
                return 0;
            }

            public string GetSSRSequence()
            {
                return string.Format("({0}){1}", motif, numRepeats);
            }

            public string GetSSR_Type()
            {
                return string.Format("p{0}", motif.Length);
            }

            #region Accessor methods

            public int End
            {
                get { return end; }
            }

            public string Motif
            {
                get { return motif; }
            }

            public int Number
            {
                get { return number; }
            }

            public int NumRepeats
            {
                get { return numRepeats; }
            }

            public int Start
            {
                get { return start; }
            }

            #endregion
        }

        public class MisaResult_Output
        {
            #region Variables

            private string sequenceID = "", ssrType = "", ssrSeq = "";
            private int sequenceCount = 0, start = 0, end = 0, ssrSize = 0;

            #endregion

            public MisaResult_Output(string sequenceID, int sequenceCount, string ssrType, string ssrSeq, int start, int end)
            {
                this.sequenceID = sequenceID;
                this.sequenceCount = sequenceCount;
                this.ssrType = ssrType;
                this.ssrSeq = ssrSeq;
                this.start = start;
                this.end = end;

                ssrSize = end - start + 1;
            }

            public void WriteToFile_Misa(StreamWriter sW)
            {
                sW.WriteLine(string.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}",
                    sequenceID, sequenceCount, ssrType, ssrSeq, ssrSize, start, end));
            }

            #region Accessor methods

            public int SSR_Size
            {
                get { return ssrSize; }
            }

            public int Start
            {
                get { return start; }
            }

            #endregion
        }

        public class Settings
        {
            #region Variables

            private const string SettingsFileName = "misa";
            private const string Extension = ".ini";

            //the maximum difference between 2 SSRs:
            private int interruptions = 0;

            private List<MisaDefinition> myDefinitions = new List<MisaDefinition>();

            private bool exportStatsFile = true;

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

            public Settings(int interruptions, List<MisaDefinition> definitions)
            {
                this.interruptions = interruptions;

                ApplyDefinitions(definitions);
            }

            public void ApplyDefinitions(List<MisaDefinition> definitions)
            {
                myDefinitions.Clear();

                myDefinitions.AddRange(definitions);
                myDefinitions.Sort();
            }

            private string GetDefaultFilePath()
            {
                return string.Format("{0}\\{1}{2}", Program.GetDirectory(), SettingsFileName, Extension);
            }

            private void LoadFromFile(string fileName)
            {
                using (StreamReader sR = new StreamReader(fileName))
                {
                    try
                    {
                        bool assignedDefinitions = false, assignedInterruptions = false;

                        while (!sR.EndOfStream)
                        {
                            string currentLine = sR.ReadLine().ToLower();

                            if (currentLine.Contains(':'))
                            {
                                string[] splits = currentLine.Split(':');

                                if (currentLine.StartsWith("definition"))
                                {
                                    string[] defSplits = splits[1].Trim().Split(' ');

                                    List<MisaDefinition> definitions = new List<MisaDefinition>(defSplits.Length);

                                    for (int i = 0; i < defSplits.Length; i++)
                                    {
                                        //split this into the two settings:
                                        string[] defSettings = defSplits[i].Split('-');

                                        definitions.Add(new MisaDefinition(Convert.ToInt32(defSettings[0]), Convert.ToInt32(defSettings[1])));
                                    }

                                    //add and sort these:
                                    ApplyDefinitions(definitions);

                                    assignedDefinitions = true;
                                }
                                else if (currentLine.StartsWith("interruptions"))
                                {
                                    interruptions = Convert.ToInt32(splits[1].Trim());

                                    assignedInterruptions = true;
                                }

                                if (assignedDefinitions && assignedInterruptions)
                                {
                                    break;
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
                myDefinitions = new List<MisaDefinition>();
                myDefinitions.Add(new MisaDefinition(1, 30));
                myDefinitions.Add(new MisaDefinition(2, 20));
                myDefinitions.Add(new MisaDefinition(3, 10));
                myDefinitions.Add(new MisaDefinition(4, 8));
                myDefinitions.Add(new MisaDefinition(5, 6));
                myDefinitions.Add(new MisaDefinition(6, 6));

                interruptions = 0;
            }

            public void Save(string fileName)
            {
                using (StreamWriter sW = new StreamWriter(fileName))
                {
                    //get the definitions as a string:
                    StringBuilder definitionsSB = new StringBuilder();

                    for (int i = 0; i < myDefinitions.Count; i++)
                    {
                        string definitionString = string.Format("{0}-{1}", myDefinitions[i].UnitSize, myDefinitions[i].MinimumRepeats);

                        if (i == 0)
                        {
                            definitionsSB.Append(definitionString);
                        }
                        else
                        {
                            definitionsSB.Append(" " + definitionString);
                        }
                    }

                    sW.WriteLine(string.Format("definition(unit_size,min_repeats):                   {0}", definitionsSB));
                    sW.WriteLine(string.Format("interruptions(max_difference_between_2_SSRs):        {0}", interruptions));

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

            public List<MisaDefinition> Definitions
            {
                get { return myDefinitions; }
            }

            public bool ExportStatsFile
            {
                get { return exportStatsFile; }
            }

            public int Interruptions
            {
                get { return interruptions; }
            }

            #endregion

            #region Support classes

            public class MisaDefinition : IComparable
            {
                #region Variables

                private int unitSize = 0, minRepeats = 0;

                #endregion

                public MisaDefinition(int unitSize, int minRepeats)
                {
                    this.unitSize = unitSize;
                    this.minRepeats = minRepeats;
                }

                public int CompareTo(object obj)
                {
                    if (obj is MisaDefinition)
                    {
                        MisaDefinition m = (MisaDefinition)obj;

                        return unitSize.CompareTo(m.unitSize);
                    }

                    return 0;
                }

                #region Accessor methods

                public int MinimumRepeats
                {
                    get { return minRepeats; }
                }

                public int UnitSize
                {
                    get { return unitSize; }
                }

                #endregion
            }

            #endregion
        }

        public class MisaStats
        {
            #region Variables

            private int numSequencesContainingSSRs = 0, numSSRsInCompoundFormation = 0, totalSSRs = 0, totalSequencesWithMoreThanOneSSR = 0;

            private int numSequencesChecked = 0;
            private long totalSize_CheckedSequences = 0;

            #endregion

            public MisaStats() { }

            public void AddCompoundSSR()
            {
                numSSRsInCompoundFormation++;
            }

            public void AddSequence(string sequenceData)
            {
                numSequencesChecked++;
                totalSize_CheckedSequences += sequenceData.Length;
            }

            public void LogResults(List<MisaResult_Output> results)
            {
                numSequencesContainingSSRs++;

                totalSSRs += results.Count;

                if (results.Count > 1)
                {
                    totalSequencesWithMoreThanOneSSR++;
                }
            }

            public void Save(string fileName, string contigFileName, Settings settings)
            {
                using (StreamWriter sW = new StreamWriter(fileName))
                {
                    sW.WriteLine(string.Format("Specifications\n==============\n\nSequence source file: \"{0}\"\n\n" 
                        + "Definement of microsatellites (unit size / minimum number of repeats):",
                        Path.GetFileName(contigFileName)));

                    for (int i = 0; i < settings.Definitions.Count; i++)
                    {
                        sW.Write(string.Format("({0}/{1})", settings.Definitions[i].UnitSize, settings.Definitions[i].MinimumRepeats));
                    }

                    sW.WriteLine();
                    sW.WriteLine();

                    if (settings.Interruptions > 0)
                    {
                        sW.WriteLine(string.Format("Maximal number of bases interrupting 2 SSRs in a compound microsatellite: {0}\n", settings.Interruptions));
                    }

                    //some empty lines:
                    sW.WriteLine();
                    sW.WriteLine();
                    sW.WriteLine("RESULTS OF MICROSATELLITE SEARCH\n================================");
                    sW.WriteLine();
                    sW.WriteLine(string.Format("Total number of sequences examined:              {0}", numSequencesChecked));
                    sW.WriteLine(string.Format("Total size of examined sequences (bp):           {0}", totalSize_CheckedSequences));
                    sW.WriteLine(string.Format("Total number of identified SSRs:                 {0}", totalSSRs));
                    sW.WriteLine(string.Format("Number of SSR containing sequences:              {0}", numSequencesContainingSSRs));
                    sW.WriteLine(string.Format("Number of sequences containing more than 1 SSR:  {0}", totalSequencesWithMoreThanOneSSR));
                    sW.WriteLine(string.Format("Number of SSRs present in compound formation:    {0}\n\n", numSSRsInCompoundFormation));

                    sW.WriteLine("Distribution to different repeat type classes\n---------------------------------------------\n");
                    sW.WriteLine("Unit size\tNumber of SSRs");

                    sW.Close();
                }
            }

            public bool SequencesFound()
            {
                return numSequencesContainingSSRs > 0;
            }

            #region Accessor methods

            public int TotalSSRs
            {
                get { return totalSSRs; }
            }

            #endregion
        }

        #endregion
    }
}