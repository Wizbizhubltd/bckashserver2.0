using BCKash.Domain.Assets;
using BCKash.Domain.Clients;
using BCKash.Domain.Communications;
using BCKash.Domain.Expenses;
using BCKash.Domain.GeneralLedger;
using BCKash.Domain.Groups;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Domain.Organization;
using BCKash.Domain.Payroll;
using BCKash.Domain.Reporting;
using BCKash.Domain.Savings;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Data;

/// <summary>
/// Single DbContext for the whole 74-table legacy schema (FRD §1.4 — Phase 0 is a
/// faithful baseline, no destructive changes). Entity configurations are discovered
/// automatically via <see cref="ApplyConfigurationsFromAssembly"/>, so later phases
/// add new <c>IEntityTypeConfiguration&lt;T&gt;</c> classes under Data/Configurations
/// without touching this file.
/// </summary>
public class BCKashDbContext : DbContext
{
    public BCKashDbContext(DbContextOptions<BCKashDbContext> options) : base(options)
    {
    }

    // Organization & Reference Data
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<State> States => Set<State>();
    public DbSet<Lga> Lgas => Set<Lga>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<PaymentType> PaymentTypes => Set<PaymentType>();
    public DbSet<PaymentDetail> PaymentDetails => Set<PaymentDetail>();
    public DbSet<PaymentTypeDetail> PaymentTypeDetails => Set<PaymentTypeDetail>();
    public DbSet<Charge> Charges => Set<Charge>();
    public DbSet<CustomField> CustomFields => Set<CustomField>();
    public DbSet<CustomFieldMeta> CustomFieldMeta => Set<CustomFieldMeta>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();

    // Identity & Access
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RoleUser> RoleUsers => Set<RoleUser>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<Activation> Activations => Set<Activation>();
    public DbSet<Persistence> Persistences => Set<Persistence>();
    public DbSet<LoginOtp> LoginOtps => Set<LoginOtp>();
    public DbSet<Throttle> Throttles => Set<Throttle>();
    public DbSet<AuditTrailEntry> AuditTrail => Set<AuditTrailEntry>();

    // Client Management
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ClientIdentification> ClientIdentifications => Set<ClientIdentification>();
    public DbSet<ClientIdentificationType> ClientIdentificationTypes => Set<ClientIdentificationType>();
    public DbSet<ClientNextOfKin> ClientNextOfKin => Set<ClientNextOfKin>();
    public DbSet<ClientNextOfGuardian> ClientNextOfGuardians => Set<ClientNextOfGuardian>();
    public DbSet<ClientProfession> ClientProfessions => Set<ClientProfession>();
    public DbSet<ClientRelationship> ClientRelationships => Set<ClientRelationship>();
    public DbSet<ClientUser> ClientUsers => Set<ClientUser>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Note> Notes => Set<Note>();

    // Group Lending
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupClient> GroupClients => Set<GroupClient>();
    public DbSet<GroupUser> GroupUsers => Set<GroupUser>();

    // Loan Management
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();
    public DbSet<LoanProduct> LoanProducts => Set<LoanProduct>();
    public DbSet<LoanProductCharge> LoanProductCharges => Set<LoanProductCharge>();
    public DbSet<LoanCharge> LoanCharges => Set<LoanCharge>();
    public DbSet<LoanPurpose> LoanPurposes => Set<LoanPurpose>();
    public DbSet<LoanRepaymentSchedule> LoanRepaymentSchedules => Set<LoanRepaymentSchedule>();
    public DbSet<LoanRescheduleRequest> LoanRescheduleRequests => Set<LoanRescheduleRequest>();
    public DbSet<LoanTransaction> LoanTransactions => Set<LoanTransaction>();
    public DbSet<LoanTransactionRepaymentScheduleMapping> LoanTransactionRepaymentScheduleMappings => Set<LoanTransactionRepaymentScheduleMapping>();
    public DbSet<LoanProvisioningCriteria> LoanProvisioningCriteria => Set<LoanProvisioningCriteria>();
    public DbSet<Guarantor> Guarantors => Set<Guarantor>();
    public DbSet<Collateral> Collateral => Set<Collateral>();
    public DbSet<CollateralType> CollateralTypes => Set<CollateralType>();
    public DbSet<GroupLoanAllocation> GroupLoanAllocations => Set<GroupLoanAllocation>();

    // Savings Management
    public DbSet<SavingsAccount> Savings => Set<SavingsAccount>();
    public DbSet<SavingsProduct> SavingsProducts => Set<SavingsProduct>();
    public DbSet<SavingsCharge> SavingsCharges => Set<SavingsCharge>();
    public DbSet<SavingsProductCharge> SavingsProductCharges => Set<SavingsProductCharge>();
    public DbSet<SavingsTransaction> SavingsTransactions => Set<SavingsTransaction>();

    // General Ledger
    public DbSet<GlAccount> GlAccounts => Set<GlAccount>();
    public DbSet<GlJournalEntry> GlJournalEntries => Set<GlJournalEntry>();
    public DbSet<GlClosure> GlClosures => Set<GlClosure>();
    public DbSet<OfficeTransaction> OfficeTransactions => Set<OfficeTransaction>();

    // Payroll
    public DbSet<Payroll> Payroll => Set<Payroll>();
    public DbSet<PayrollMeta> PayrollMeta => Set<PayrollMeta>();
    public DbSet<PayrollTemplate> PayrollTemplates => Set<PayrollTemplate>();
    public DbSet<PayrollTemplateMeta> PayrollTemplateMeta => Set<PayrollTemplateMeta>();

    // Fixed Assets
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetDepreciation> AssetDepreciations => Set<AssetDepreciation>();
    public DbSet<AssetType> AssetTypes => Set<AssetType>();

    // Expenses & Other Income
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseBudget> ExpenseBudgets => Set<ExpenseBudget>();
    public DbSet<ExpenseType> ExpenseTypes => Set<ExpenseType>();
    public DbSet<OtherIncome> OtherIncomes => Set<OtherIncome>();
    public DbSet<OtherIncomeType> OtherIncomeTypes => Set<OtherIncomeType>();

    // Client Communications
    public DbSet<CommunicationCampaign> CommunicationCampaigns => Set<CommunicationCampaign>();
    public DbSet<SmsGateway> SmsGateways => Set<SmsGateway>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    // Reporting
    public DbSet<ReportScheduler> ReportSchedulers => Set<ReportScheduler>();
    public DbSet<ReportSchedulerRunHistory> ReportSchedulerRunHistories => Set<ReportSchedulerRunHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BCKashDbContext).Assembly);
    }
}
