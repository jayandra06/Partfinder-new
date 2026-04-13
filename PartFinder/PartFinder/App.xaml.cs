using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using PartFinder.Services;
using PartFinder.ViewModels;
using PartFinder.Views.Pages;

namespace PartFinder
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;
        private Window? _window;

        public App()
        {
            InitializeComponent();
            Services = ConfigureServices();
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                DebugLogClient.Post(
                    "error",
                    "UnhandledException",
                    e.ExceptionObject?.ToString());
            };
            DebugLogClient.Post("info", "PartFinder WinUI started", null);
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IAppStateStore, AppStateStore>();
            services.AddSingleton<ITemplateSchemaService, InMemoryTemplateSchemaService>();
            services.AddSingleton<IPartsDataService, MockPartsDataService>();

            services.AddSingleton<ShellViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<PartsViewModel>();
            services.AddTransient<TemplatesViewModel>();

            services.AddTransient<DashboardPage>();
            services.AddTransient<PartsPage>();
            services.AddTransient<TemplatesPage>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            _window.Activate();
        }
    }
}
