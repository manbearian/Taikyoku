using ShogiClient;
using ShogiEngine;

namespace MauiUI
{
    public partial class MainPage : ContentPage
    {
        //
        // Bindabe Proprerties
        //

        public static readonly BindableProperty ConnectionProperty = BindableProperty.Create(nameof(Connection), typeof(Connection), typeof(MyGamesView));

        public Connection Connection
        {
            get => (Connection)GetValue(ConnectionProperty);
            set => SetValue(ConnectionProperty, value);
        }

        public MainPage()
        {
            InitializeComponent();

            Connection = new Connection();
        }

        private void NewLocalGameBtn_Clicked(object sender, EventArgs e)
        {
            Navigation.PushModalAsync(new BoardPage(Guid.Empty, new TaikyokuShogi()), true);
        }
    }
}