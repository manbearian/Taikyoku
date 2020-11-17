﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

using Oracle;

namespace WPF_UI
{
    /// <summary>
    /// Interaction logic for PieceInfo.xaml
    /// </summary>
    public partial class PieceInfoWindow : Window
    {
        public PieceInfoWindow()
        {
            InitializeComponent();
        }

        public void SetPiece(TaiyokuShogi game, PieceIdentity id) =>
            pieceInfoDisplay.SetPiece(game, id);
    }
}
