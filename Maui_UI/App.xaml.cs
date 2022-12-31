namespace MauiUI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Don't wrap in a shell or navigation page
            MainPage = MauiUI.MainPage.Default;
            // MainPage = new AppShell();
        }
    }
}