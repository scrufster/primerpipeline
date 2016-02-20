using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace PrimerPipeline.Controls
{
    public class NumericTextBox : TextBox
    {
        #region Variables

        private bool allowDP = false;
        private bool allowNegative = false;
        private bool cycleToNegative = false;
        private bool goBlankOnInvalid = false;
        private bool enforceMaxLimit = false;
        private bool isCyclic = false;  //if true, a value larger than the maximum will loop round to fit into the allowed range
        private double minValue;    //the actual minimum allowed value
        private double maxValue;    //the actual maximum allowed value
        private double typicalMinValue;
        private double typicalMaxValue;
        private double typicalValue;    //a typical value for this field
        private bool useTypicalValues = true;

        private bool ensureMultipleOfValue = false;
        private double multipleOfValue = 0;

        private bool dataPasted = false;

        #endregion

        public NumericTextBox()
        {
            VerticalContentAlignment = System.Windows.VerticalAlignment.Center;

            this.LostFocus += new RoutedEventHandler(NumericTextBox_LostFocus);
            this.PreviewTextInput += new TextCompositionEventHandler(NumericTextBox_PreviewTextInput);
            this.TextChanged += NumericTextBox_TextChanged;

            DataObject.AddPastingHandler(this, new DataObjectPastingEventHandler(OnPaste));
        }

        private void CheckForValidNumber()
        {
            if (this.Text.Equals(""))
            {
                if (!goBlankOnInvalid)
                {
                    if (useTypicalValues)
                    {
                        //set to the typical value:
                        this.Text = typicalValue.ToString();
                    }
                    else
                    {
                        //set to the minimum usual sonar range:
                        this.Text = minValue.ToString();
                    }
                }
            }
            else
            {
                //the actual text in the textbox:
                string textboxText = this.Text;

                //first check that number does not contain anything numeric that is not allowed:
                if (!allowDP)
                {
                    if (textboxText.Contains("."))
                    {
                        //get the position of the first ".":
                        int position = textboxText.IndexOf(".");

                        //remove anything after the point:
                        textboxText = textboxText.Substring(0, position);
                    }
                }

                if (!allowNegative)
                {
                    textboxText = textboxText.Replace("-", "");
                }

                double textboxNumber;
                bool isANumber = false;

                //try to convert the input to a number:
                isANumber = double.TryParse(textboxText, out textboxNumber);

                //if it is a number, then make sure it is within the allowed range:
                if (isANumber)
                {
                    //first adjust the number if it is cyclic:
                    if (isCyclic)
                    {
                        if (cycleToNegative && allowNegative)
                        {
                            if (textboxNumber > maxValue)
                            {
                                textboxNumber = (textboxNumber % maxValue) - maxValue;
                            }
                            else if (textboxNumber < minValue)
                            {
                                textboxNumber = (textboxNumber % maxValue) + maxValue;
                            }
                        }
                        else
                        {
                            textboxNumber = textboxNumber % maxValue;
                        }
                    }

                    if (ensureMultipleOfValue)
                    {
                        textboxNumber = textboxNumber - (textboxNumber % multipleOfValue);
                    }

                    //now check that it is within the range:
                    if (textboxNumber > maxValue)
                    {
                        //only do this if maximum limits are being enforced:
                        if (enforceMaxLimit)
                        {
                            if (useTypicalValues)
                            {
                                //set to the typical minimum value:
                                textboxNumber = typicalMaxValue;
                            }
                            else
                            {
                                textboxNumber = maxValue;
                            }
                        }
                    }
                    else if (textboxNumber < minValue)
                    {
                        //only do this if maximum limits are being enforced:
                        if (enforceMaxLimit)
                        {
                            if (useTypicalValues)
                            {
                                //set to the typical minimum value:
                                textboxNumber = typicalMinValue;
                            }
                            else
                            {
                                textboxNumber = minValue;
                            }
                        }
                    }

                    //otherwise it must be fine already
                    this.Text = textboxNumber.ToString();
                }
                else //input was not a number:
                {
                    if (goBlankOnInvalid)
                    {
                        this.Text = "";
                    }
                    else if (useTypicalValues)
                    {
                        //set to the typical minimum value:
                        this.Text = typicalValue.ToString();
                    }
                    else
                    {
                        //set to the minimum value:
                        this.Text = minValue.ToString();
                    }
                }
            }
        }

        private bool IsValidNumericInput(int input)
        {
            //if the input is "-":
            if (input == 45)
            {
                //allow only if this is the first digit, and has not already been used:
                if (allowNegative && !this.Text.Contains("-") && this.SelectionStart == 0)
                {
                    return true;
                }
            }

            //if the input is ".":
            if (input == 46)
            {
                //allow only if this has not already been used, and is not the first digit:
                if (allowDP && !this.Text.Contains(".") && this.SelectionStart > 0)
                {
                    //don't allow if it is coming right after a "-":
                    if (this.Text.Contains("-") && this.SelectionStart == 1)
                    {
                        return false;
                    }

                    return true;
                }
            }

            //if the input is a valid number:
            if (input >= 48 && input <= 57)
            {
                //if this is the second character, don't allow it to be a 0 if the first was also a zero:
                if (this.SelectionStart == 1 && this.Text.Substring(0, 1) == "0" && input == 48)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            dataPasted = true;
        }

        #region Events
        
        private void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CheckForValidNumber();
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!e.Text.Equals(""))
            {
                char pressedChar = e.Text.ToCharArray(0, 1)[0];

                //suppress the input if it is not valid numeric:
                if (!IsValidNumericInput((int)pressedChar))
                {
                    //suppress the keypress (by saying the event has already been handled):
                    e.Handled = true;
                }
            }
            else
            {
                e.Handled = true;
            }            
        }

        private void NumericTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (dataPasted)
            {
                if (Text.Contains(" "))
                {
                    Text = Text.Replace(" ", "");
                }

                dataPasted = false;
            }
        }

        #endregion

        #region Accessor methods

        /// <summary>
        /// Whether or not numbers can have fractions.
        /// </summary>
        public bool AllowDecimalPlaces
        {
            get { return allowDP; }
            set { allowDP = value; }
        }

        /// <summary>
        /// Whether or not numbers can be negative as well.
        /// </summary>
        public bool AllowNegative
        {
            get { return allowNegative; }
            set { allowNegative = value; }
        }

        public bool CycleToNegative
        {
            get { return cycleToNegative; }
            set { cycleToNegative = value; }
        }

        /// <summary>
        /// If true, the textbox will default to empty on invalid input.
        /// </summary>
        public bool GoBlankOnInvalid
        {
            get { return goBlankOnInvalid; }
            set { goBlankOnInvalid = value; }
        }

        /// <summary>
        /// If true, the value in the textbox upon leaving will not exceed that set
        /// as the maximum value.
        /// </summary>
        public bool EnforceMaximumLimit
        {
            get { return enforceMaxLimit; }
            set { enforceMaxLimit = value; }
        }

        /// <summary>
        /// If true, the textbox will only accept values that are multiples of the MutlipleOfValue property.
        /// </summary>
        public bool EnsureMultipleOfValue
        {
            get { return ensureMultipleOfValue; }
            set { ensureMultipleOfValue = value; }
        }

        /// <summary>
        /// Returns true if what is currently in the textbox is within the valid
        /// specified ranges. I.e. to be used in conjunction with methods to be triggered
        /// after this textbox's KeyUp event.
        /// </summary>
        public bool IsCurrentlyValid
        {
            get
            {
                double textboxNumber = 0;
                bool isANumber = false;

                if (allowDP)
                {
                    //try to convert the input to a number:
                    isANumber = double.TryParse(this.Text, out textboxNumber);
                }
                else //if no d.p. allowed, the number must be meant to be an integer:
                {
                    int textboxIntNumber = 0;

                    //try to convert the input to a number:
                    bool isAnInteger = int.TryParse(this.Text, out textboxIntNumber);

                    if (isAnInteger)
                    {
                        isANumber = true;
                        textboxNumber = (double)textboxIntNumber;
                    }
                }

                if (!isANumber)
                {
                    return false;
                }
                else
                {
                    if (isCyclic || !enforceMaxLimit)
                    {
                        return true;
                    }
                    else
                    {
                        if ((!enforceMaxLimit) || (textboxNumber >= minValue && textboxNumber <= maxValue))
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// If true, a number greater than the maximum can be entered, but upon leaving
        /// it will be adjusted to its equivalent value within the valid range.
        /// </summary>
        public bool IsCyclic
        {
            get { return isCyclic; }
            set { isCyclic = value; }
        }

        /// <summary>
        /// The minimum value allowed.
        /// </summary>
        public double MinValueAllowed
        {
            get { return minValue; }
            set { minValue = value; }
        }

        /// <summary>
        /// The maximum value allowed.
        /// </summary>
        public double MaxValueAllowed
        {
            get { return maxValue; }
            set { maxValue = value; }
        }

        /// <summary>
        /// If EnsureMultipleOfValue if set to true, the textbox will only accept values that are a multiple of
        /// this number.
        /// </summary>
        public double MultipleOfValue
        {
            get { return multipleOfValue; }
            set { multipleOfValue = value; }
        }

        public double NumericValue
        {
            get
            {
                double myNumber;
                bool isANumber = double.TryParse(this.Text, out myNumber);
                return isANumber ? myNumber : (useTypicalValues ? typicalValue : minValue);//double.NaN);
            }
            set 
            {
                this.Text = value.ToString();

                CheckForValidNumber();
            }
        }

        /// <summary>
        /// A typical value to be expected for this textbox, i.e. if invalid input is given
        /// the program may resort to this value.
        /// </summary>
        public double TypicalValue
        {
            get { return typicalValue; }
            set { typicalValue = value; }
        }

        /// <summary>
        /// A typical minimum value, which may not be the actual possible minimum value.
        /// </summary>
        public double TypicalMinValue
        {
            get { return typicalMinValue; }
            set { typicalMinValue = value; }
        }

        /// <summary>
        /// A typical maximum value, which may not be the actual possible maximum value.
        /// </summary>
        public double TypicalMaxValue
        {
            get { return typicalMaxValue; }
            set { typicalMaxValue = value; }
        }

        /// <summary>
        /// True if the textbox is to use typical values when invalid input is given.
        /// </summary>
        public bool UseTypicalValues
        {
            get { return useTypicalValues; }
            set { useTypicalValues = value; }
        }

        #endregion
    }
}
