using System;
using System.Windows;

namespace PrimerPipeline
{
    public partial class Window_MISA_Definition : Window
    {
        public Window_MISA_Definition(Window owner)
        {
            InitializeComponent();

            Owner = owner;
        }

        public Window_MISA_Definition(Window owner, MicrosatelliteCalculator.Settings.MisaDefinition definition)
            : this(owner)
        {
            Title = "Edit definition";

            unitSize_NumericTextBox.NumericValue = definition.UnitSize;
            minRepeats_NumericTextBox.NumericValue = definition.MinimumRepeats;
        }

        public MicrosatelliteCalculator.Settings.MisaDefinition GetDefinition()
        {
            return new MicrosatelliteCalculator.Settings.MisaDefinition((int)unitSize_NumericTextBox.NumericValue, (int)minRepeats_NumericTextBox.NumericValue);
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }
    }
}