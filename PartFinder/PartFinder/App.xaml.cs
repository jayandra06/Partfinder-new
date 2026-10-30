using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
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
            // Required for WinUI: default Theme has no tooltip/legend until configured (otherwise Chart.Load throws).
            LiveCharts.Configure(config =>
                config.AddSkiaSharp().AddDefaultMappers().AddDefaultTheme());

            InitializeComponent();
            Services = ConfigureServices();

            this.UnhandledException += (sender, e) =>
            {
                e.Handled = true;
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[PartFinder] XAML unhandled: {e.Exception}");
                    DebugLogClient.Post("error", "XamlUnhandledException", e.Exception?.ToString() ?? e.Message);
                    var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PartFinder", "Logs");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.UtcNow:O}] [XAML_UNHANDLED] {e.Exception?.ToString() ?? e.Message}\n");
                }
                catch { }
            };

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    DebugLogClient.Post("error", "UnhandledException", e.ExceptionObject?.ToString());
                    var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PartFinder", "Logs");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.UtcNow:O}] [APPDOMAIN_UNHANDLED] {e.ExceptionObject?.ToString()}\n");
                }
                catch { }
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                e.SetObserved();
                try
                {
                    DebugLogClient.Post("error", "UnobservedTaskException", e.Exception?.ToString());
                    var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PartFinder", "Logs");
                    Directory.CreateDirectory(logDir);
                    File.AppendAllText(Path.Combine(logDir, "crash.log"), $"[{DateTime.UtcNow:O}] [UNOBSERVED_TASK] {e.Exception?.ToString()}\n");
                }
                catch { }
            };

            DebugLogClient.Post("info", "PartFinder WinUI started", null);
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IAppStateStore, AppStateStore>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<LocalUserSecurityStore>();
            services.AddSingleton<AdminSessionStore>();
            services.AddSingleton<ILocalSetupContext, LocalSetupContext>();
            services.AddSingleton<LocalProfileStore>();
            services.AddSingleton<BackendApiClient>();
            services.AddSingleton<IOrgUserDirectoryService, MongoOrgUserDirectoryService>();
            services.AddSingleton<ICurrentUserAccessService, CurrentUserAccessService>();
            services.AddSingleton<ITemplateSchemaService, MongoTemplateSchemaService>();
            services.AddSingleton<BulkTemplateImportService>();
            services.AddSingleton<IMasterDataRecordsService, MongoMasterDataRecordsService>();
            services.AddSingleton<IContextActionsService, MongoContextActionsService>();
            services.AddSingleton<IPartsDataService, MongoPartsDataService>();
            services.AddSingleton<IExcelTemplateService, ClosedXmlExcelTemplateService>();
            services.AddSingleton<IFavouriteStore, MongoFavouriteStore>();
            services.AddSingleton<ExplorerNavigationCoordinator>();

            // Dedicated services for pages
            services.AddSingleton<MongoInventoryService>();
            services.AddSingleton<MongoAuditService>();
            services.AddSingleton<MongoAlertsService>();
            services.AddSingleton<MongoSessionService>();
            services.AddSingleton<ActivityLogger>();

            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<IShellNavCoordinator>(sp => sp.GetRequiredService<ShellViewModel>());
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<PartsViewModel>();
            services.AddTransient<TemplatesViewModel>();
            services.AddTransient<ViewDataViewModel>();
            services.AddTransient<WorksheetRelationsViewModel>();
            services.AddTransient<MasterDataViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<UserManagementViewModel>();
            services.AddTransient<QrCodeManagerViewModel>();
            services.AddTransient<AlertsViewModel>();
            services.AddTransient<AuditViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<FavouritesViewModel>();

            services.AddTransient<MasterDataPage>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<PartsPage>();
            services.AddTransient<InventoryPage>();
            services.AddTransient<AlertsPage>();
            services.AddTransient<AuditPage>();
            services.AddTransient<TemplatesPage>();
            services.AddTransient<ViewDataPage>();
            services.AddTransient<WorksheetRelationsPage>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<UserManagementPage>();
            services.AddTransient<QrCodeManagerPage>();

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
