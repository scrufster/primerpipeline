using System;
using System.Collections.Generic;
using System.Windows;

namespace PrimerPipeline
{
    public partial class Window_MessageBoxWithList : Window
    {
        private Window_MessageBoxWithList(Window owner, MessageBoxButton buttons)
        {
            InitializeComponent();

            Owner = owner;
            Title = Program.Name;

            //set the buttons:
            switch (buttons)
            {
                case MessageBoxButton.YesNo:
                    //Yes/No option:
                    buttonPositive_Button.Content = "Yes";
                    buttonNegative_Button.Content = "No";
                    break;
                default:
                    //an OK button only:
                    buttonPositive_Button.Content = "OK";
                    buttonPositive_Button.Margin = new Thickness(3, 3, 12, 12);
                    buttonNegative_Button.Visibility = Visibility.Collapsed;

                    //in this case pressing enter can be the same as clicking OK:
                    buttonPositive_Button.IsDefault = true;

                    break;
            }
        }

        public Window_MessageBoxWithList(Window owner, List<string> listItems, string itemName,
            string preListMessage, MessageBoxButton buttons, string postListMessage = "")
            : this(owner, buttons)
        {
            preMessage_TextBlock.Text = string.Format("The following {0}{1} {2}:",
                listItems.Count > 1 ? listItems.Count + " " : "",
                MiscTask.Pluraliser(itemName, listItems.Count),
                preListMessage);
            postMessage_TextBlock.Text = postListMessage;

            //collapse the post list message if there is no message:
            if (postListMessage.Equals(""))
            {
                postMessage_TextBlock.Visibility = System.Windows.Visibility.Collapsed;
            }

            for (int i = 0; i < listItems.Count; i++)
            {
                list_ListView.Items.Add(new ListItem(listItems[i]));
            }
        }

        public Window_MessageBoxWithList(Window owner, List<string> listItems, string preListMessage, MessageBoxButton buttons, string postListMessage = "")
            : this(owner, buttons)
        {
            preMessage_TextBlock.Text = preListMessage;
            postMessage_TextBlock.Text = postListMessage;

            //collapse the post list message if there is no message:
            if (postListMessage.Equals(""))
            {
                postMessage_TextBlock.Visibility = System.Windows.Visibility.Collapsed;
            }

            for (int i = 0; i < listItems.Count; i++)
            {
                list_ListView.Items.Add(new ListItem(listItems[i]));
            }
        }

        private void CopyList_Button_Click(object sender, RoutedEventArgs e)
        {
            if (list_ListView.HasItems)
            {
                string text = preMessage_TextBlock.Text + "\n\n";

                for (int i = 0; i < list_ListView.Items.Count; i++)
                {
                    string endLineText = i < list_ListView.Items.Count - 1 ? "\n" : "";

                    text = text + ((ListItem)list_ListView.Items[i]).ItemName + endLineText;
                }

                if (!postMessage_TextBlock.Text.ToString().Equals(""))
                {
                    text = text + "\n\n" + postMessage_TextBlock.Text;
                }

                Clipboard.SetText(text);
            }
        }

        private void PositiveResponse_Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private class ListItem
        {
            public string ItemName { get; private set; }

            public ListItem(string itemName)
            {
                ItemName = itemName;
            }
        }
    }
}