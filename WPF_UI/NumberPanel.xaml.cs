using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using ShogiEngine;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for NumberPanel.xaml
    /// </summary>
    public partial class NumberPanel : UserControl
    {
        public Orientation Orientation { get; set; }

        public Brush TextColor { get; set; } = Brushes.Black;

        public Brush FillColor { get; set; } = Brushes.White;

        public NumberPanel()
        {
            InitializeComponent();
        }

        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(FillColor, null, new Rect(0, 0, ActualWidth, ActualHeight));

            if (Orientation == Orientation.Horizontal)
            {
                static string ColumnName(int i) => $"{TaikyokuShogi.BoardWidth - i}";

                var spacing = ActualWidth / TaikyokuShogi.BoardWidth;

                for (int i = 0; i < TaikyokuShogi.BoardWidth; ++i)
                {
                    var text = new FormattedText(
                        ColumnName(i),
                        CultureInfo.GetCultureInfo("en-us"),
                        FlowDirection.LeftToRight,
                        new Typeface("MS Gothic"),
                        ActualHeight,
                        TextColor,
                        1.25)
                    {
                        TextAlignment = TextAlignment.Center
                    };

                    dc.DrawText(text, new Point((i * spacing) + (spacing / 2), 0));
                }
            }
            else if (Orientation == Orientation.Vertical)
            {
                static string RowName(int i) => new string((char)('A' + (i % 26)), i / 26 + 1);

                var spacing = ActualHeight / TaikyokuShogi.BoardHeight;

                for (int i = 0; i < TaikyokuShogi.BoardHeight; ++i)
                {
                    var text = new FormattedText(
                        RowName(i),
                        CultureInfo.GetCultureInfo("en-us"),
                        FlowDirection.LeftToRight,
                        new Typeface("MS Gothic"),
                        ActualWidth * .8,
                        TextColor,
                        1.25)
                    {
                        TextAlignment = TextAlignment.Center
                    };

                    dc.DrawText(text, new Point(spacing / 3, (i * spacing) + (spacing / 4)));
                }
            }
            else
            {
                throw new NotSupportedException();
            }

            base.OnRender(dc);
        }
    }
}
