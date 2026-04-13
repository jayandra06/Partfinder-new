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
        public static Window? MainAppWindow { get; private set; }
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
            services.AddSingleton<LocalUserSecurityStore>();
            services.AddSingleton<AdminSessionStore>();
            services.AddSingleton<ILocalSetupContext, LocalSetupContext>();
            services.AddSingleton<ITemplateSchemaService, MongoTemplateSchemaService>();
            services.AddSingleton<IMasterDataRecordsService, MongoMasterDataRecordsService>();
            services.AddSingleton<IContextActionsService, MongoContextActionsService>();
            services.AddSingleton<IPartsDataService, MongoPartsDataService>();
            services.AddSingleton<IExcelTemplateService, ClosedXmlExcelTemplateService>();

            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<IShellNavCoordinator>(sp => sp.GetRequiredService<ShellViewModel>());
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<PartsViewModel>();
            services.AddTransient<TemplatesViewModel>();
            services.AddTransient<MasterDataViewModel>();
            services.AddTransient<SettingsViewModel>();

            services.AddTransient<MasterDataPage>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<PartsPage>();
            services.AddTransient<TemplatesPage>();
            services.AddTransient<SettingsPage>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            MainAppWindow = _window;
            _window.Activate();
        }
    }
}
