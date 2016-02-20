using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PrimerPipeline
{
    public class Primer3OutputResult
    {
        #region Variables

        private string sequenceID = "", sequenceTemplate = "";
        private List<PrimerData> primerData = new List<PrimerData>();

        #endregion

        public Primer3OutputResult(StreamReader sR)
        {
            string currentLine = "";

            int currentPrimer = 0;

            //separate results are delimited by an '=':
            while (!currentLine.StartsWith("=") && !sR.EndOfStream)
            {
                currentLine = sR.ReadLine();

                if (currentLine.StartsWith("SEQUENCE_ID="))
                {
                    sequenceID = currentLine.Replace("SEQUENCE_ID=", "");
                }
                else if (currentLine.StartsWith("SEQUENCE_TEMPLATE="))
                {
                    sequenceTemplate = currentLine.Replace("SEQUENCE_TEMPLATE=", "");
                }
                else if (currentLine.StartsWith(string.Format("PRIMER_LEFT_{0}", currentPrimer)))
                {
                    primerData.Add(new PrimerData(sR, ref currentLine, currentPrimer));

                    currentPrimer++;
                }
            }
        }

        public void AppendToOutput(StringBuilder sB, int expectedPrimerCount)
        {
            for (int i = 0; i < expectedPrimerCount; i++)
            {
                //do we have actual data for this primer:
                if (i < primerData.Count)
                {
                    sB.Append(primerData[i].GetResult() + ",");
                }
                else
                {
                    //empty columns:
                    sB.Append(',', 9);
                }
            }

            sB.Append(sequenceTemplate);
        }

        public static void AppendHeader(StringBuilder sB, int targetNumberOfPrimers)
        {
            //data for each primer output:
            for (int i = 0; i < targetNumberOfPrimers; i++)
            {
                sB.Append(string.Format("FORWARD PRIMER{1} (5'-3'){0}Tm(°C){0}Size{0}REVERSE PRIMER1 (5'-3'){0}Tm(°C){0}"
                    + "Size{0}PRODUCT1 size (bp){0}Start (bp){0}End (bp){0}", ",", i + 1));
            }
        }

        public bool IsValid()
        {
            return primerData.Count > 0;
        }

        #region Support classes

        public class PrimerData
        {
            #region Variables

            private string leftSequence = "", leftTemperature = "", left_Size = "", rightSequence = "", rightTM = "", right_Size = "", pairProductSize = "", start = "", end = "";

            #endregion

            public PrimerData(StreamReader sR, ref string currentLine, int currentPrimer)
            {
                string field_LeftSequence = string.Format("PRIMER_LEFT_{0}_SEQUENCE=", currentPrimer);
                string field_PrimerLeftTemperature = string.Format("PRIMER_LEFT_{0}_TM=", currentPrimer);
                string field_PrimerLeft = string.Format("PRIMER_LEFT_{0}=", currentPrimer);
                string field_RightSequence = string.Format("PRIMER_RIGHT_{0}_SEQUENCE=", currentPrimer);
                string field_PrimerRightTemperature = string.Format("PRIMER_RIGHT_{0}_TM=", currentPrimer);
                string field_PrimerRight = string.Format("PRIMER_RIGHT_{0}=", currentPrimer);
                string field_PairProductSize = string.Format("PRIMER_PAIR_{0}_PRODUCT_SIZE=", currentPrimer);

                while (!currentLine.StartsWith("=") && !sR.EndOfStream)
                {
                    currentLine = sR.ReadLine();

                    if (currentLine.StartsWith(field_LeftSequence))
                    {
                        leftSequence = currentLine.Replace(field_LeftSequence, "");
                    }
                    else if (currentLine.StartsWith(field_PrimerLeftTemperature))
                    {
                        leftTemperature = currentLine.Replace(field_PrimerLeftTemperature, "");
                    }
                    else if (currentLine.StartsWith(field_PrimerLeft))
                    {
                        string[] values = currentLine.Replace(field_PrimerLeft, "").Split(',');

                        left_Size = values[1];
                        start = values[0];
                    }
                    else if (currentLine.StartsWith(field_RightSequence))
                    {
                        rightSequence = currentLine.Replace(field_RightSequence, "");
                    }
                    else if (currentLine.StartsWith(field_PrimerRightTemperature))
                    {
                        rightTM = currentLine.Replace(field_PrimerRightTemperature, "");
                    }
                    else if (currentLine.StartsWith(field_PrimerRight))
                    {
                        string[] values = currentLine.Replace(field_PrimerRight, "").Split(',');

                        right_Size = values[1];
                        end = values[0];
                    }
                    else if (currentLine.StartsWith(field_PairProductSize))
                    {
                        pairProductSize = currentLine.Replace(field_PairProductSize, "");

                        //the product size is the last field for a primer, so we'll stop after
                        //we read this field so that we can go onto read the next one:
                        break;
                    }
                }
            }

            public string GetResult()
            {
                return string.Format("{0},{1},{2},{3},{4},{5},{6},{7},{8}", 
                    leftSequence, 
                    leftTemperature, 
                    left_Size, 
                    rightSequence, 
                    rightTM, 
                    right_Size, 
                    pairProductSize, 
                    start, 
                    end);
            }
        }

        #endregion

        #region Accessor methods

        public string SequenceID
        {
            get { return sequenceID; }
        }

        public string SequenceTemplate
        {
            get { return sequenceTemplate; }
        }

        #endregion
    }
}