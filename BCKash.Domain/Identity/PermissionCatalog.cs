namespace BCKash.Domain.Identity;

/// <summary>One permission the API actually checks — see <see cref="PermissionCatalog"/>.</summary>
public record PermissionDefinition(string Slug, string Area, string Name, string Description);

/// <summary>
/// Every permission an endpoint checks (<c>[Authorize(Policy = "Permission:…")]</c>), with a
/// plain-language description of what it unlocks. The single source of truth for seeding the
/// `permissions` rows and for which permissions can be granted to a role. The legacy `permissions`
/// table holds ~290 more slugs from the old system that nothing checks; they're deliberately not
/// grantable, since switching them on would do nothing.
/// Add an entry here whenever a new permission policy is introduced.
/// </summary>
public static class PermissionCatalog
{
    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        // Organisation & access
        new("organization.manage", "Organisation", "Organisation setup", "Create and edit offices, fees & penalties, funds, payment types, currencies, countries and custom fields."),
        new("settings.manage", "Organisation", "Rules & settings", "Change system settings — company profile, overdue & penalty rules, notification switches."),
        new("users.manage", "Organisation", "Staff management", "View and onboard staff, approve or decline onboarding (maker-checker applies), change their role, class or office, block them and reset passwords."),

        // Customers
        new("clients.manage", "Customers", "Customers", "Create and edit customers and their IDs, next of kin, guardians, notes and documents; activate, decline or close them."),
        new("groups.manage", "Customers", "Groups", "Create groups and add or remove their members."),

        // Loans
        new("loan-products.manage", "Loans", "Loan products", "Create and edit loan products, loan purposes and collateral types."),
        new("loan-applications.manage", "Loans", "Raise loan applications", "Create and edit loan applications with their guarantors and collateral, and resubmit loans sent back for changes."),
        new("loan-applications.approve", "Loans", "Approve loans", "Approve or decline loan applications, and send loans back for changes."),
        new("loan-servicing.manage", "Loans", "Service loans", "Disburse loans, record repayments, reschedule, waive, write off and recover, attach loan charges and run due penalties."),

        // Savings
        new("savings-products.manage", "Savings", "Savings products", "Create and edit savings products."),
        new("savings-accounts.manage", "Savings", "Savings accounts", "Open and manage savings accounts — deposits, withdrawals, transfers, charges and interest posting."),

        // Accounting
        new("gl.manage", "Accounting", "General ledger", "Manage the chart of accounts, post journal entries, close accounting periods and record inter-office transfers."),
        new("gl.closure-reopen", "Accounting", "Reopen closed periods", "Reopen an accounting period that has been closed. Sensitive — changes figures already reported."),
        new("assets.manage", "Accounting", "Fixed assets", "Manage fixed assets, asset types and depreciation."),
        new("expenses.manage", "Accounting", "Record expenses", "Record expenses and manage expense types."),
        new("expenses.approve", "Accounting", "Approve expenses", "Approve or decline recorded expenses."),
        new("expense-budgets.manage", "Accounting", "Expense budgets", "Create and edit expense budgets."),
        new("expense-budgets.approve", "Accounting", "Approve expense budgets", "Approve or decline expense budgets."),
        new("other-income.manage", "Accounting", "Record other income", "Record income other than loan and savings income, and manage its types."),
        new("other-income.approve", "Accounting", "Approve other income", "Approve or decline recorded other income."),
        new("payroll.manage", "Accounting", "Payroll setup", "Create and edit payroll templates."),
        new("payroll.run", "Accounting", "Run payroll", "Run payroll from the templates."),

        // Communications & reports
        new("campaigns.manage", "Communications & reports", "Campaigns", "Create and edit SMS/email campaigns and configure SMS gateways."),
        new("campaigns.run", "Communications & reports", "Send campaigns", "Send campaigns that are due."),
        new("reports.view", "Communications & reports", "View reports", "Run and export reports."),
        new("report-schedules.manage", "Communications & reports", "Scheduled reports", "Set up reports that run and are sent on a schedule."),
    ];

    public static readonly IReadOnlySet<string> Slugs = All.Select(p => p.Slug).ToHashSet();
}
