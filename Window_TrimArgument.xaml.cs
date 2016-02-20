using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace PrimerPipeline
{
    public partial class Window_TrimArgument : Window
    {
        public Window_TrimArgument(Window owner)
        {
            InitializeComponent();

            Owner = owner;
        }

        public Window_TrimArgument(Window owner, DNA_Trimmer.TrimArgument argument)
            : this(owner)
        {
            Title = "Edit argument";

            if (argument is DNA_Trimmer.AmbiguousTrimArgument)
            {
                DNA_Trimmer.AmbiguousTrimArgument ambiguousArgument = (DNA_Trimmer.AmbiguousTrimArgument)argument;

                ambiguousBases_RadioButton.IsChecked = true;
                ambiguous_N_NumericTextBox.NumericValue = ambiguousArgument.NumberOfBases;
                ambiguous_WindowSize_NumericTextBox.NumericValue = ambiguousArgument.WindowSize;
            }
            else if (argument is DNA_Trimmer.StretchTypeTrimArgument)
            {
                DNA_Trimmer.StretchTypeTrimArgument stretchArgument = (DNA_Trimmer.StretchTypeTrimArgument)argument;

                stretchesOfType_RadioButton.IsChecked = true;
                stretchesOfType_End_ComboBox.SelectedIndex = stretchArgument.From5End ? 1 : 0;

                switch (stretchArgument.CharacterType)
                {
                    case 'A': stretchesOfType_Type_ComboBox.SelectedIndex = 0; break;
                    case 'C': stretchesOfType_Type_ComboBox.SelectedIndex = 1; break;
                    case 'G': stretchesOfType_Type_ComboBox.SelectedIndex = 2; break;
                    default: stretchesOfType_Type_ComboBox.SelectedIndex = 3; break;
                }
                
                stretchesOfType_Minimum_NumericTextBox.NumericValue = stretchArgument.MinAcceptedRepeat;
                stretchesOfType_WindowSize_NumericTextBox.NumericValue = stretchArgument.WindowSize;
            }
            else
            {
                DNA_Trimmer.CutOffTrimArgument cutOffArgument = (DNA_Trimmer.CutOffTrimArgument)argument;

                cutOff_RadioButton.IsChecked = true;
                cutOff_MinValue_NumericTextBox.NumericValue = cutOffArgument.MinValue;
                cutOff_SequenceSize_NumericTextBox.NumericValue = cutOffArgument.MaxSequenceSize;
            }
        }

        public DNA_Trimmer.TrimArgument GetArgument()
        {
            if (ambiguousBases_RadioButton.IsChecked.Value)
            {
                return new DNA_Trimmer.AmbiguousTrimArgument((int)ambiguous_N_NumericTextBox.NumericValue, (int)ambiguous_WindowSize_NumericTextBox.NumericValue);
            }
            else if (stretchesOfType_RadioButton.IsChecked.Value)
            {
                string charType = stretchesOfType_Type_ComboBox.Text;

                return new DNA_Trimmer.StretchTypeTrimArgument(stretchesOfType_End_ComboBox.SelectedIndex == 1,
                    charType[0], 
                    (int)stretchesOfType_Minimum_NumericTextBox.NumericValue,
                    (int)stretchesOfType_WindowSize_NumericTextBox.NumericValue);
            }
            else //cut-off:
            {
                return new DNA_Trimmer.CutOffTrimArgument((int)cutOff_MinValue_NumericTextBox.NumericValue, (int)cutOff_SequenceSize_NumericTextBox.NumericValue);
            }
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}
