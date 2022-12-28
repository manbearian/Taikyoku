namespace MauiUI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Don't wrap in a shell or navigation page
            MainPage = new MainPage();
            // MainPage = new AppShell();
        }
    }
}