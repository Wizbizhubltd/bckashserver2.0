using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using BCKash.Application.Assets;
using BCKash.Application.Auth;
using BCKash.Application.Clients;
using BCKash.Application.Communications;
using BCKash.Application.Expenses;
using BCKash.Application.Files;
using BCKash.Application.GeneralLedger;
using BCKash.Application.Groups;
using BCKash.Application.Identity;
using BCKash.Application.Loans;
using BCKash.Application.Organization;
using BCKash.Application.PayrollProcessing;
using BCKash.Application.Reporting;
using BCKash.Application.Savings;
using BCKash.Infrastructure.Assets;
using BCKash.Infrastructure.Audit;
using BCKash.Infrastructure.Auth;
using BCKash.Infrastructure.Aws;
using BCKash.Infrastructure.Clients;
using BCKash.Infrastructure.Communications;
using BCKash.Infrastructure.Data;
using BCKash.Infrastructure.Expenses;
using BCKash.Infrastructure.Files;
using BCKash.Infrastructure.GeneralLedger;
using BCKash.Infrastructure.Groups;
using BCKash.Infrastructure.Identity;
using BCKash.Infrastructure.Loans;
using BCKash.Infrastructure.Organization;
using BCKash.Infrastructure.PayrollProcessing;
using BCKash.Infrastructure.Reporting;
using BCKash.Infrastructure.Savings;
using EFCoreSecondLevelCacheInterceptor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using StackExchange.Redis;

namespace BCKash.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    // Kept out of the EF second-level cache. These are written on nearly every sign-in (or, for
    // audit_trail, on every SaveChanges), and their reads are keyed on per-request values
    // (timestamps, OTP/token ids), so a cached result is almost never reused. Caching them only
    // added Redis writes on every read plus an invalidation on every write, all on the sign-in
    // path. Security state like lockouts and OTPs must also be read fresh anyway.
    private static readonly string[] UncachedTables = ["throttle", "login_otps", "persistences", "audit_trail"];

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<LoginThrottleSettings>(configuration.GetSection(LoginThrottleSettings.SectionName));
        services.Configure<FileStorageSettings>(configuration.GetSection(FileStorageSettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<SmsSettings>(configuration.GetSection(SmsSettings.SectionName));
        services.Configure<IdentityBootstrapSettings>(configuration.GetSection(IdentityBootstrapSettings.SectionName));
        services.Configure<OtpSettings>(o => o.MasterOtp = configuration[OtpSettings.MasterOtpKey]);
        services.Configure<BvnGatewaySettings>(configuration.GetSection(BvnGatewaySettings.SectionName));
        services.Configure<StaffPortalSettings>(configuration.GetSection(StaffPortalSettings.SectionName));

        // UseMockBvn (see MockBvnVerificationProvider) skips the real BVN gateway for testing the
        // onboarding flow; unset or empty means every lookup goes to the gateway.
        var mockBvnMode = MockBvnVerificationProvider.ParseMode(configuration[MockBvnVerificationProvider.ConfigKey]);
        if (mockBvnMode.HasValue)
        {
            services.AddSingleton<IBvnVerificationProvider>(new MockBvnVerificationProvider(mockBvnMode.Value));
        }
        else
        {
            services.AddHttpClient<IBvnVerificationProvider, BcKashGatewayBvnVerificationProvider>(client => client.Timeout = TimeSpan.FromSeconds(30));
        }

        // AWS: with credentials, uploads go to the S3 bucket and face biometrics use Rekognition;
        // without them (local development, tests) files stay on disk and face capture is unavailable.
        services.Configure<AwsSettings>(configuration.GetSection(AwsSettings.SectionName));
        services.Configure<BiometricsSettings>(configuration.GetSection(BiometricsSettings.SectionName));
        var aws = configuration.GetSection(AwsSettings.SectionName).Get<AwsSettings>() ?? new AwsSettings();
        services.AddScoped<LocalDiskFileStorageService>();
        if (aws.HasCredentials && !string.IsNullOrWhiteSpace(aws.S3Bucket))
        {
            services.AddSingleton<IAmazonS3>(new AmazonS3Client(
                new BasicAWSCredentials(aws.AccessKeyId, aws.SecretAccessKey), RegionEndpoint.GetBySystemName(aws.Region)));
            services.AddScoped<IFileStorageService, S3FileStorageService>();
        }
        else
        {
            services.AddScoped<IFileStorageService>(sp => sp.GetRequiredService<LocalDiskFileStorageService>());
        }

        if (aws.HasCredentials)
        {
            services.AddSingleton<IFaceBiometrics, RekognitionFaceBiometrics>();
        }

        services.AddScoped<IClientBiometricsService, ClientBiometricsService>();

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<BCKashDbContext>((sp, options) =>
        {
            var connectionString = configuration.GetConnectionString("BCKashDb");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:BCKashDb is not configured — set ConnectionStrings__BCKashDb in BCKashServer2.0/.env (copy .env.example) for local development.");
            }

            // Integration tests run against SQLite (no MySQL instance available in this
            // environment — see Phase 0 plan) via this single config-driven switch, rather than
            // a second AddDbContext call in the test host: EF Core doesn't support two different
            // providers' services coexisting in the same DI container, even if only one
            // DbContextOptions registration survives.
            if (configuration.GetValue<bool>("Testing:UseSqlite"))
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                // ConvertZeroDateTime: the legacy schema (and any dump of it) can contain
                // zero-value dates ('0000-00-00'/'0000-00-00 00:00:00') — MySQL happily stores
                // these but MySqlConnector rejects them on read by default. This maps them to
                // DateTime.MinValue instead of throwing, regardless of what's in the configured
                // connection string, so every environment gets this without a manual edit.
                var builder = new MySqlConnectionStringBuilder(connectionString) { ConvertZeroDateTime = true };

                // A fixed server version, not ServerVersion.AutoDetect(...) — AutoDetect needs a
                // live connection at startup, which would make the API fail to boot whenever the
                // database is briefly unreachable. BCKash runs on MariaDB 11.4 (production and the
                // docker-compose `db` service); adjust if the target instance changes.
                options.UseMySql(builder.ConnectionString, new MariaDbServerVersion(new Version(11, 4, 0)));
            }

            options.AddInterceptors(
                sp.GetRequiredService<AuditSaveChangesInterceptor>(),
                sp.GetRequiredService<SecondLevelCacheInterceptor>());
        });

        // Same Testing:UseSqlite flag used for the DB provider and email/SMS fakes below — no
        // Redis instance is available in the test environment either, so tests get an in-memory
        // IDistributedCache and EF second-level cache instead of a real connection.
        if (configuration.GetValue<bool>("Testing:UseSqlite"))
        {
            services.AddDistributedMemoryCache();
            services.AddEFSecondLevelCache(options =>
                options.UseMemoryCacheProvider()
                       .CacheAllQueriesExceptContainingTableNames(CacheExpirationMode.Absolute, TimeSpan.FromMinutes(5), UncachedTables));
        }
        else
        {
            var redisConnectionString = configuration.GetConnectionString("Redis");
            if (string.IsNullOrWhiteSpace(redisConnectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Redis is not configured — set ConnectionStrings__Redis in BCKashServer2.0/.env (copy .env.example) for local development.");
            }

            services.AddSingleton<IConnectionMultiplexer>(
                _ => ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "BCKash:";
            });

            // Every EF Core query BCKashDbContext runs is cached in Redis (keyed by the query +
            // its parameters), and every SaveChanges automatically invalidates the cache entries
            // for whichever tables it just wrote to — see SecondLevelCacheInterceptor wired into
            // BCKashDbContext above. This covers all reads/writes made through the DbContext; it
            // would NOT cover ExecuteUpdate/ExecuteDelete bulk operations, which bypass
            // SaveChanges — none exist in this codebase today (grep for them before adding one).
            services.AddEFSecondLevelCache(options =>
                options.UseStackExchangeRedisCacheProvider(
                        ConfigurationOptions.Parse(redisConnectionString), TimeSpan.FromMinutes(5))
                       .CacheAllQueriesExceptContainingTableNames(CacheExpirationMode.Absolute, TimeSpan.FromMinutes(5), UncachedTables)
                       .UseCacheKeyPrefix("EF_")
                       .UseDbCallsIfCachingProviderIsDown(TimeSpan.FromMinutes(1)));
        }

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<ITotpService, OtpNetTotpService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ILoginThrottleService, LoginThrottleService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOfficeScope, OfficeScope>();
        services.AddScoped<IActiveSessionChecker, ActiveSessionChecker>();
        services.AddScoped<IOfficeService, OfficeService>();
        services.AddScoped<ICompanyProfileProvider, CompanyProfileProvider>();
        services.AddScoped<ICurrencyDisplayProvider, CurrencyDisplayProvider>();
        services.AddScoped<IOfficeFundService, OfficeFundService>();
        services.AddScoped<ILoanApplicationClientCodeService, LoanApplicationClientCodeService>();
        services.AddScoped<IOverdueRulesProvider, OverdueRulesProvider>();
        services.AddScoped<ILoanPenaltyService, LoanPenaltyService>();
        services.AddScoped<ILoanNotificationService, LoanNotificationService>();
        services.AddScoped<ILoanCompletionService, LoanCompletionService>();
        services.AddScoped<IRepaymentSubmissionService, RepaymentSubmissionService>();
        services.AddScoped<IStaffNotificationService, StaffNotificationService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IClientAccess, ClientAccess>();
        services.AddScoped<IClientSavingsService, ClientSavingsService>();
        services.AddScoped<IClientSavingsSettingsProvider, ClientSavingsSettingsProvider>();
        services.AddScoped<IDeletionService, DeletionService>();
        services.AddScoped<IClientOnboardingService, ClientOnboardingService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IGroupMembershipService, GroupMembershipService>();
        services.AddScoped<ILoanProductService, LoanProductService>();
        services.AddScoped<ILoanApplicationService, LoanApplicationService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<ILoanGlPostingService, LoanGlPostingService>();
        services.AddScoped<ILoanRepaymentService, LoanRepaymentService>();
        services.AddScoped<ILoanWaiverService, LoanWaiverService>();
        services.AddScoped<ILoanRescheduleService, LoanRescheduleService>();
        services.AddScoped<ILoanWriteOffService, LoanWriteOffService>();
        services.AddScoped<ILoanNpaService, LoanNpaService>();
        services.AddScoped<IGlClosureGuard, GlClosureGuard>();
        services.AddScoped<IGlAccountService, GlAccountService>();
        services.AddScoped<IManualJournalEntryService, ManualJournalEntryService>();
        services.AddScoped<IGlJournalEntryService, GlJournalEntryService>();
        services.AddScoped<IGlClosureService, GlClosureService>();
        services.AddScoped<IOfficeTransferService, OfficeTransferService>();
        services.AddScoped<IGlReportService, GlReportService>();
        services.AddScoped<ISavingsProductService, SavingsProductService>();
        services.AddScoped<ISavingsAccountService, SavingsAccountService>();
        services.AddScoped<ISavingsTransactionService, SavingsTransactionService>();
        services.AddScoped<ISavingsChargeService, SavingsChargeService>();
        services.AddScoped<ISavingsInterestPostingService, SavingsInterestPostingService>();
        services.AddScoped<ISavingsGlPostingService, SavingsGlPostingService>();
        services.AddScoped<ISavingsTransferService, SavingsTransferService>();
        services.AddScoped<IAssetTypeService, AssetTypeService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAssetDepreciationService, AssetDepreciationService>();
        services.AddScoped<IAssetGlPostingService, AssetGlPostingService>();
        services.AddScoped<IExpenseTypeService, ExpenseTypeService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IExpenseGlPostingService, ExpenseGlPostingService>();
        services.AddScoped<IExpenseBudgetService, ExpenseBudgetService>();
        services.AddScoped<IOtherIncomeTypeService, OtherIncomeTypeService>();
        services.AddScoped<IOtherIncomeService, OtherIncomeService>();
        services.AddScoped<IOtherIncomeGlPostingService, OtherIncomeGlPostingService>();
        services.AddScoped<IPayrollTemplateService, PayrollTemplateService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IPayrollGlPostingService, PayrollGlPostingService>();
        services.AddScoped<ICampaignRecipientService, CampaignRecipientService>();
        services.AddScoped<ICommunicationCampaignService, CommunicationCampaignService>();
        services.AddScoped<ISmsGatewayService, SmsGatewayService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IClientReportService, ClientReportService>();
        services.AddScoped<ILoanReportService, LoanReportService>();
        services.AddScoped<IGroupReportService, GroupReportService>();
        services.AddScoped<ISavingsReportService, SavingsReportService>();
        services.AddScoped<IOrganisationReportService, OrganisationReportService>();
        services.AddScoped<IReportCatalogService, ReportCatalogService>();
        services.AddScoped<IReportExporter, ReportExporter>();
        services.AddScoped<IReportSchedulerService, ReportSchedulerService>();
        services.AddScoped<IUserService, UserService>();

        // No test environment can actually deliver SMTP mail or hit a real SMS gateway, so the
        // same Testing:UseSqlite flag that already switches the DB provider also swaps these two
        // for recording fakes tests can inspect (see RecordingEmailSender's doc comment).
        //
        // Either way, every SMS goes through a wrapper that honours the "SMS sending" master switch
        // (Settings → Notifications — see ISmsSwitch); the fakes are also registered as themselves
        // so tests can read what they recorded.
        services.AddScoped<ISmsSwitch, SmsSwitch>();
        if (configuration.GetValue<bool>("Testing:UseSqlite"))
        {
            services.AddSingleton<IEmailSender, RecordingEmailSender>();
            services.AddSingleton<RecordingSmsSender>();
            services.AddSingleton<RecordingOtpSmsSender>();
            services.AddScoped<ISmsSender>(sp => new SwitchedSmsSender(
                sp.GetRequiredService<RecordingSmsSender>(), sp.GetRequiredService<ISmsSwitch>(), sp.GetRequiredService<ILogger<SwitchedSmsSender>>()));
            services.AddScoped<IOtpSmsSender>(sp => new SwitchedOtpSmsSender(
                sp.GetRequiredService<RecordingOtpSmsSender>(), sp.GetRequiredService<ISmsSwitch>(), sp.GetRequiredService<ILogger<SwitchedOtpSmsSender>>()));
            services.AddScoped<IOtpDispatcher, InlineOtpDispatcher>();
        }
        else
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
            services.AddHttpClient<HttpSmsGatewaySender>();
            services.AddHttpClient<TermiiOtpSmsSender>();
            services.AddScoped<ISmsSender>(sp => new SwitchedSmsSender(
                sp.GetRequiredService<HttpSmsGatewaySender>(), sp.GetRequiredService<ISmsSwitch>(), sp.GetRequiredService<ILogger<SwitchedSmsSender>>()));
            services.AddScoped<IOtpSmsSender>(sp => new SwitchedOtpSmsSender(
                sp.GetRequiredService<TermiiOtpSmsSender>(), sp.GetRequiredService<ISmsSwitch>(), sp.GetRequiredService<ILogger<SwitchedOtpSmsSender>>()));

            // Tests drive penalty runs explicitly through ILoanPenaltyService / the run-due endpoint.
            services.AddHostedService<DailyLoanPenaltyWorker>();
            services.AddHostedService<DailyLoanReminderWorker>();
            services.AddSingleton<BackgroundOtpDispatcher>();
            services.AddSingleton<IOtpDispatcher>(sp => sp.GetRequiredService<BackgroundOtpDispatcher>());
            services.AddHostedService(sp => sp.GetRequiredService<BackgroundOtpDispatcher>());
        }

        services.AddHostedService<ReferenceDataSeeder>();
        services.AddHostedService<IdentityBootstrapSeeder>();

        return services;
    }
}
