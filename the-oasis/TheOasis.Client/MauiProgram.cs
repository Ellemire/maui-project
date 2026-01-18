using Microsoft.Extensions.Logging;

namespace TheOasis.Client
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MrBedfort-Regular.ttf", "MrBedfort");
                    fonts.AddFont("Merriweather-VariableFont.ttf", "MerriweatherRegular");
                    fonts.AddFont("Merriweather-Italic-VariableFont.ttf", "MerriweatherItalic");
                    fonts.AddFont("MerriweatherSans-VariableFont.ttf", "MerriweatherSansRegular");
                    fonts.AddFont("MerriweatherSans-Italic-VariableFont.ttf", "MerriweatherSansItalic");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
