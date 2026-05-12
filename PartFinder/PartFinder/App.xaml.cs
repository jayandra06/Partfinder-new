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
            services.AddSingleton<LocalProfileStore>();
            services.AddSingleton<ILocalSetupContext, LocalSetupContext>();
            services.AddSingleton<BackendApiClient>();
            services.AddSingleton<IOrgUserDirectoryService, MongoOrgUserDirectoryService>();
            services.AddSingleton<ICurrentUserAccessService, CurrentUserAccessService>();
            services.AddSingleton<ITemplateSchemaService, MongoTemplateSchemaService>();
            services.AddSingleton<IMasterDataRecordsService, MongoMasterDataRecordsService>();
            services.AddSingleton<IContextActionsService, MongoContextActionsService>();
            services.AddSingleton<IPartsDataService, MongoPartsDataService>();
            services.AddSingleton<IExcelTemplateService, ClosedXmlExcelTemplateService>();
            services.AddSingleton<IFavouriteStore, MongoFavouriteStore>();

            // Dedicated services for pages
            services.AddSingleton<MongoInventoryService>();
            services.AddSingleton<MongoAuditService>();
            services.AddSingleton<MongoAlertsService>();
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
