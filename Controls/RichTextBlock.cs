using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace PrimerPipeline.Controls
{
    public class RichTextBlock : TextBlock
    {
        public static DependencyProperty InlineProperty;

        static RichTextBlock()
        {
            //This OverrideMetadata call tells the system that this element wants to provide a style that is different than its base class.

            //This style is defined in themes\generic.xaml

            DefaultStyleKeyProperty.OverrideMetadata(typeof(RichTextBlock), new FrameworkPropertyMetadata(typeof(RichTextBlock)));

            InlineProperty = DependencyProperty.Register("RichText", typeof(List<Inline>), typeof(RichTextBlock), 
                new PropertyMetadata(null, new PropertyChangedCallback(OnInlineChanged)));
        }

        public List<Inline> RichText
        {
            get { return (List<Inline>)GetValue(InlineProperty); }
            set { SetValue(InlineProperty, value); }
        }

        public static void OnInlineChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue == e.OldValue) return;

            RichTextBlock r = sender as RichTextBlock;

            List<Inline> i = e.NewValue as List<Inline>;

            if (r == null || i == null) 
            { 
                return; 
            }
            
            r.Inlines.Clear();

            foreach (Inline inline in i)
            {
                r.Inlines.Add(inline);
            }
        }
    }
}