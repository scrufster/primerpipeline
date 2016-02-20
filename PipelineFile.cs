using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Timers;

namespace PrimerPipeline
{
    public sealed class PipelineFile : INotifyPropertyChanged
    {
        #region Variables

        //we'll allow the user to input files from different stages of the process, and the program will repsond accordingly:
        private FileType myFileType = FileType.CONTIG;

        private CurrentTask currentTask = CurrentTask.NONE;
        private string currentTaskDetails = "";

        private BackgroundWorker bgW = null;
        private Progress myProgress = null;

        private string inputFileName = "", status = "Ready to process";
        private bool processing = false, processed = false, processCancelled = false;

        //Primer3
        private int p3ExpectedResultCount = 0;

        private int targetNumberOfPrimers = 0;
        private Process primer3Process = null;
        private bool primer3ProgressCheckInProgress = false;
        Timer primer3ProgressCheckTimer = null;
        private const int Primer3ProgressCheckIntervalSeconds = 30;

        //final results file output:
        private int sequenceCount_Success = 0, sequenceCount_Fail = 0;

        private DateTime startTime;

        public event EventHandler ProcessCompleted;

        #endregion

        public PipelineFile(string fileName)
        {
            this.inputFileName = fileName;

            if (Path.GetExtension(fileName).ToLower().Equals(Primer3Settings.Extension_P3In))
            {
                myFileType = FileType.PRIMER3_IN;
            }
            else if (Path.GetExtension(fileName).ToLower().Equals(Primer3Settings.Extension_P3Out))
            {
                myFileType = FileType.PRIMER3_OUT;
            }
            else if (BioDataFile.IsBioDataFile(fileName))
            {
                myFileType = FileType.FINAL_RESULTS;

                //we can only view the results for this file:
                processed = true;

                status = "Processed";
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is PipelineFile)
            {
                PipelineFile p = (PipelineFile)obj;

                return p.inputFileName.Equals(inputFileName);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        private string GetMyOutputBasePath()
        {
            return MiscTask.GetBasePath(inputFileName);
        }

        private string GetMyPrimer3SettingsPath()
        {
            return string.Format("{0}_Primer3_Settings.txt", GetMyOutputBasePath());
        }

        public string GetTrimmedFileName()
        {
            return DNA_Trimmer.GetTrimmedFileName(inputFileName);
        }

        public bool HasNextTask()
        {
            return currentTask != CurrentTask.FINAL_RESULTS;
        }

        public bool IsLocked()
        {
            return FilePreLoadState.GetPreLoadState(inputFileName) == FilePreLoadState.PreLoadState.LOCKED;
        }

        public bool IsProcessing()
        {
            return processing;
        }

        public void MoveToNextTask()
        {
            //if it's Primer3 that's running, we'll need to kill it:
            if (primer3Process != null)
            {
                //this will still count as the process exiting, and so onward operations will continue:
                StopPrimer3();
            }
            else
            {
                myProgress.MoveToNextTaskRequested = true;
            }
        }

        public void StartProcess(DNA_Trimmer.Settings trimSettings, MicrosatelliteCalculator.Settings misaSettings, 
            Primer3Settings primer3Settings, string fileName_Misa)
        {
            processing = true;

            bgW = new BackgroundWorker();
            bgW.WorkerReportsProgress = true;
            bgW.WorkerSupportsCancellation = true;

            bgW.DoWork += BGW_ProcessFile_DoWork;
            bgW.ProgressChanged += BGW_ProcessFile_ProgressChanged;
            bgW.RunWorkerCompleted += BGW_ProcessFile_RunWorkerCompleted;

            startTime = DateTime.Now;

            bgW.RunWorkerAsync(new object[] { trimSettings, misaSettings, primer3Settings, fileName_Misa });
        }

        public void Stop()
        {
            processCancelled = true; 

            if (bgW != null)
            {
                bgW.CancelAsync();
            }

            if (primer3Process != null)
            {
                StopPrimer3();
            }           
        }

        private void UpdateStatus(string newStatus)
        {
            status = newStatus;
            NotifyPropertyChanged("Status");
        }
        
        #region Background worker

        private void BGW_ProcessFile_DoWork(object sender, DoWorkEventArgs e)
        {
            if (myFileType != FileType.FINAL_RESULTS)
            {
                string errorString = "";

                try
                {
                    //reset variable values. They might be re-processing the file with different settings:
                    processed = false;
                    sequenceCount_Success = 0;
                    sequenceCount_Fail = 0;

                    //the arguments:
                    object[] args = (object[])e.Argument;

                    DNA_Trimmer.Settings trimSettings = (DNA_Trimmer.Settings)args[0];
                    MicrosatelliteCalculator.Settings misaSettings = (MicrosatelliteCalculator.Settings)args[1];
                    Primer3Settings primer3Settings = (Primer3Settings)args[2];
                    string fileName_Misa = (string)args[3];

                    BackgroundWorker bgW = (BackgroundWorker)sender;
                    myProgress = new Progress(myFileType == FileType.CONTIG ? 3 : !fileName_Misa.Equals("") ? 2 : 1);

                    //we need to know the number of primers output later:
                    targetNumberOfPrimers = primer3Settings.GetOutputPrimerCount();
                    
                    bool canRunPrimer3 = myFileType == FileType.PRIMER3_IN;
                    string fileName_P3Input = inputFileName;

                    if (myFileType == FileType.CONTIG)
                    {
                        currentTask = CurrentTask.TRIM_AND_MISA;                

                        errorString = "Failed during trimming and MISA";

                        //1. Trim .contig file, and identify microsatellites. This also creates
                        //the Primer3 input file:
                        DNA_Trimmer trimAndIdentify = new DNA_Trimmer(inputFileName, trimSettings, misaSettings, primer3Settings.InputFileIncludesThermodynamicParameters);
                        trimAndIdentify.TriggerProgressDisplayUpdate += TrimAndIdentify_MisaResultsUpdated;

                        trimAndIdentify.Process(bgW, myProgress);

                        trimAndIdentify.TriggerProgressDisplayUpdate -= TrimAndIdentify_MisaResultsUpdated;

                        fileName_Misa = trimAndIdentify.MISA.GetFileName_MISA();
                        canRunPrimer3 = trimAndIdentify.MISA.Primer3InputFileCreated();
                        fileName_P3Input = trimAndIdentify.MISA.GetFileName_Primer3Input();
                    }

                    //if we got here as a result of the user requesting a move to the next task, we've done that
                    //now so we can cancel the request:
                    myProgress.MoveToNextTaskRequested = false;

                    string fileName_P3Out = inputFileName;
                    bool canCreateFinalResults = myFileType == FileType.PRIMER3_OUT;

                    if (canRunPrimer3 && !bgW.CancellationPending)
                    {
                        currentTask = CurrentTask.PRIMER3; 

                        errorString = "Failed during Primer3";

                        //2. Run primer3
                        if (RunPrimer3(fileName_P3Input, primer3Settings, bgW, myProgress) && !fileName_Misa.Equals(""))
                        {
                            canCreateFinalResults = true;
                            fileName_P3Out = GetPrimer3OutputFileName();
                        }
                        else
                        {
                            errorString = "Primer3 files missing";
                        }
                    }

                    if (canCreateFinalResults && !bgW.CancellationPending)
                    {
                        currentTask = CurrentTask.FINAL_RESULTS; 

                        errorString = "Failed to create final results file";

                        //3. Convert to table
                        CreateResultsFile(fileName_Misa, fileName_P3Out, bgW, myProgress);

                        processed = true;
                    }
                }
                catch
                {
                    e.Result = errorString;
                }
            }
        }

        private void BGW_ProcessFile_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Progress progress = (Progress)e.UserState;
            
            UpdateStatus(progress.GetProgress());
        }

        private void BGW_ProcessFile_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //were there any errors:
            string errorString = (string)e.Result;

            if (processCancelled)
            {
                UpdateStatus("Process cancelled");
            }
            else
            {
                if (errorString != null)
                {
                    UpdateStatus(string.Format("Error occurred: {0}", errorString));
                }
                else
                {
                    UpdateStatus(string.Format("Processed (time taken = {0})", DateTime.Now.Subtract(startTime).ToString(@"hh\:mm\:ss")));
                }
            }

            StopProgressCheckTimer();

            UpdateCurrentTaskDetails("");

            processing = false;
            currentTask = CurrentTask.NONE; 

            bgW.Dispose();
            bgW = null;

            //notify that the process is complete. This will trigger any updates, e.g. context menu options
            //if this item is already selected:
            if (ProcessCompleted != null)
            {
                ProcessCompleted(this, new EventArgs());
            }
        }

        private void TrimAndIdentify_MisaResultsUpdated(object sender, CurrentResultsEventArgs e)
        {
            UpdateCurrentTaskDetails(string.Format("SSR results (found/read): {0}/{1}", e.MisaStats.TotalSSRs, e.SequencesRead));
        }

        private void UpdateCurrentTaskDetails(string details)
        {
            currentTaskDetails = details;
            NotifyPropertyChanged("CurrentTaskDetails");
        }

        #endregion

        #region Final results

        private void CreateResultsFile(string misaFileName, string primer3OutputFileName, BackgroundWorker bgW, Progress progress)
        {
            progress.AssignCurrentTask("Creating final results file");
            bgW.ReportProgress(0, progress);

            using (StreamReader sR_P3 = new StreamReader(primer3OutputFileName))
            {
                try
                {
                    using (StreamReader sR_Misa = new StreamReader(misaFileName))
                    {
                        //we'll only create the output file if we're going to need it:
                        StreamWriter sW = null;

                        try
                        {
                            while (!sR_P3.EndOfStream)
                            {
                                //the Primer3 result:
                                Primer3OutputResult primer3Result = new Primer3OutputResult(sR_P3);

                                if (primer3Result.IsValid())
                                {
                                    //find the matching Misa data:
                                    string misaData = "";

                                    //now find the MISA information that matches this file:
                                    while (!sR_Misa.EndOfStream)
                                    {
                                        string currentLine_Misa = sR_Misa.ReadLine();

                                        //we wrote the Misa file, and it's split by tabs:
                                        if (!currentLine_Misa.Equals(""))
                                        {
                                            string[] misaSplits = currentLine_Misa.Split('\t');

                                            //the sequence names aren't identical, as we added on a value to the end
                                            //of the Primer3 input version, so they are slightly longer:
                                            if (misaSplits.Length > 0 && primer3Result.SequenceID.Contains(misaSplits[0]))
                                            {
                                                //this is the matching sequence in the Misa file, and as we are putting
                                                //all of it into the output file, we only need to change the delimeter:
                                                misaData = currentLine_Misa.Replace('\t', ',');

                                                break;
                                            }
                                        }
                                    }

                                    StringBuilder sB = new StringBuilder();
                                    
                                    //the notes column, which will be left blank and the user can add data to that if they want:
                                    sB.Append(",");

                                    if (!misaData.Equals(""))
                                    {
                                        sB.Append(misaData + ",");
                                    }
                                    else
                                    {
                                        //we need to fill the space for the data anyway:
                                        sB.Append(',', 7);
                                    }

                                    //add the primer data:
                                    primer3Result.AppendToOutput(sB, targetNumberOfPrimers);

                                    //save to the output file:
                                    if (sW == null)
                                    {
                                        sW = new StreamWriter(GetFinalResultsFileName());

                                        StringBuilder headerBuilder = new StringBuilder();

                                        headerBuilder.Append(string.Format("Details{0}ID{0}SSR nr.{0}SSR type{0}SSR{0}Size{0}Start{0}End{0}", ","));

                                        //data for each primer output:
                                        Primer3OutputResult.AppendHeader(headerBuilder, targetNumberOfPrimers);

                                        headerBuilder.Append("Template");

                                        //write the header:
                                        sW.WriteLine(headerBuilder.ToString());
                                    }

                                    sW.WriteLine(sB.ToString());

                                    sequenceCount_Success++;
                                }
                                else
                                {
                                    sequenceCount_Fail++;
                                }

                                //report progress:
                                if ((sequenceCount_Success + sequenceCount_Fail) % 25 == 0)
                                {
                                    progress.ApplyProgressFraction((double)sR_P3.BaseStream.Position / sR_P3.BaseStream.Length);
                                    bgW.ReportProgress(0, progress);
                                }
                            }
                        }
                        catch { throw; }
                        finally
                        {
                            sR_Misa.Close();

                            if (sW != null)
                            {
                                sW.Close();
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    sR_P3.Close();
                }
            }
        }

        public string GetFinalResultsFileName()
        {
            if (myFileType == FileType.FINAL_RESULTS)
            {
                return inputFileName;
            }
            else
            {
                return string.Format("{0}_FinalResults.csv", MiscTask.GetBasePath(inputFileName));
            }            
        }

        #endregion

        #region Primer3

        private string GetPrimer3OutputFileName()
        {
            return string.Format("{0}{1}", MiscTask.GetBasePath(inputFileName), Primer3Settings.Extension_P3Out);
        }

        private string GetPrimer3OutputFileName_ProgressCopy()
        {
            return string.Format("{0}_p3Out_ProgressCheckCopy", MiscTask.GetBasePath(inputFileName));
        }

        private bool RunPrimer3(string inputFile, Primer3Settings settings, BackgroundWorker bgW, Progress progress)
        {
            if (Primer3Settings.VerifyEXE() && settings.ThermodynamicSettingsValid())
            {
                //we're not in control of this stage, hence indeterminate progress:
                progress.AssignCurrentTask("Running Primer3", false);
                bgW.ReportProgress(0, progress);

                //save the Primer3 settings in the same directory as the output:
                settings.Save(GetMyPrimer3SettingsPath());

                //we need to know the number of primers output later:
                targetNumberOfPrimers = settings.GetOutputPrimerCount();

                p3ExpectedResultCount = GetForecastNumberOfResults(inputFile);

                //USAGE: primer3_core [-format_output] [-default_version=1|-default_version=2] [-io_version=4] [-p3_settings_file=<file_path>] 
                //[-echo_settings_file] [-strict_tags] [-output=<file_path>] [-error=<file_path>] [input_file]

                ProcessStartInfo processStartInfo = new ProcessStartInfo(Primer3Settings.GetPrimer3Path(),
                    string.Format("-p3_settings_file=\"{0}\" -output=\"{1}\" -error=\"{2}\" \"{3}\"",
                    GetMyPrimer3SettingsPath(),
                    GetPrimer3OutputFileName(),
                    string.Format("{0}_Primer3_ErrorLog.txt", GetMyOutputBasePath()),
                    inputFile));
                processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                primer3Process = new Process();
                primer3Process.StartInfo = processStartInfo;
                primer3Process.EnableRaisingEvents = true;

                UpdateCurrentTaskDetails(string.Format("(updates every {0} seconds)", Primer3ProgressCheckIntervalSeconds));

                primer3Process.Start();

                //we want to try and obtain progress from Primer 3:
                primer3ProgressCheckTimer = new Timer(Primer3ProgressCheckIntervalSeconds * 1000);
                primer3ProgressCheckTimer.Elapsed += Primer3ProgressTimer_Elapsed;
                primer3ProgressCheckTimer.Start();
                
                //wait until the process completes:
                primer3Process.WaitForExit();
                
                primer3Process.Dispose();
                primer3Process = null;

                StopProgressCheckTimer();

                return true;
            }
            else
            {
                return false;
            }
        }

        private int GetForecastNumberOfResults(string inputPath)
        {
            int numResults = 0;

            using (StreamReader sR = new StreamReader(inputPath))
            {
                try
                {
                    while (!sR.EndOfStream)
                    {
                        Primer3OutputResult result = new Primer3OutputResult(sR);

                        numResults++;
                    }
                }
                catch { }
                finally
                {
                    sR.Close();
                }
            }

            return numResults;
        }

        private void Primer3ProgressTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!primer3ProgressCheckInProgress)
            {
                primer3ProgressCheckInProgress = true;

                //the Primer3 output file name:
                string p3ProgressFileName = GetPrimer3OutputFileName_ProgressCopy();

                try
                {
                    //the p3 out file is locked, so we'll need to make a copy to check progress:
                    File.Copy(GetPrimer3OutputFileName(), p3ProgressFileName, true);

                    //the progress file should be hidden:
                    File.SetAttributes(p3ProgressFileName, FileAttributes.Hidden);

                    //reset the counters:
                    int validResults = 0, totalResults = 0;

                    //using (StreamReader sR = new StreamReader(new FileStream(p3OutFile, FileMode.Open, FileAccess.Read, FileShare.Read)))
                    using (StreamReader sR = new StreamReader(p3ProgressFileName))
                    {
                        try
                        {
                            while (!sR.EndOfStream)
                            {
                                Primer3OutputResult result = new Primer3OutputResult(sR);

                                if (result != null && result.IsValid())
                                {
                                    validResults++;
                                }

                                totalResults++;                                
                            }

                            myProgress.ApplyProgressFraction((double)totalResults / p3ExpectedResultCount);
                            bgW.ReportProgress(0, myProgress);

                            //UpdateStatus(string.Format("Running Primer3 ({0}%)", Math.Round(((double)totalResults / p3ExpectedResultCount) * 100, 1)));
                            UpdateCurrentTaskDetails(string.Format("Results found (valid/total): {0}/{1}...", 
                                validResults, 
                                totalResults));
                        }
                        catch { }
                        finally
                        {
                            sR.Close();
                        }
                    }

                    //delete the progress check file:
                    File.Delete(p3ProgressFileName);
                }
                catch { }

                primer3ProgressCheckInProgress = false;
            }
        }

        private void StopPrimer3()
        {
            try
            {
                primer3Process.Kill();

                StopProgressCheckTimer();
            }
            catch { }
        }

        private void StopProgressCheckTimer()
        {
            if (primer3ProgressCheckTimer != null)
            {
                primer3ProgressCheckTimer.Stop();
                primer3ProgressCheckTimer.Dispose();
                primer3ProgressCheckTimer = null;
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        #endregion

        #region Accessor methods

        public string CurrentTaskDetails
        {
            get { return currentTaskDetails; }
        }

        public string FileName
        {
            get { return inputFileName; }
        }

        public FileType MyFileType
        {
            get { return myFileType; }
        }

        public bool Processed
        {
            get { return processed; }
        }
        
        public string SafeFileName
        {
            get { return Path.GetFileName(inputFileName); }
        }
        
        public string Status
        {
            get { return status; }
        }

        #endregion

        #region Support classes

        private enum CurrentTask
        {
            NONE = 0,
            TRIM_AND_MISA = 1,
            PRIMER3 = 2,
            FINAL_RESULTS = 3
        }

        public enum FileType
        {
            CONTIG = 0,
            PRIMER3_IN = 1,
            PRIMER3_OUT = 2,
            FINAL_RESULTS = 3
        }

        #endregion
    }
}