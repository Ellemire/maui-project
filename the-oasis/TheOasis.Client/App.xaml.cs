using Microsoft.Extensions.DependencyInjection;

namespace TheOasis.Client
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            InitTheme();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }

        private void InitTheme()
        {
            AppTheme currentTheme = App.Current!.RequestedTheme;
            App.Current.UserAppTheme = AppTheme.Light;
        }
    }
}