namespace BCKash.Api.Contracts;

public record DashboardSummaryResponse(
    int OfficesCount,
    int ActiveOfficesCount,
    int StaffCount,
    int ClientsCount,
    int ActiveClientsCount,
    int OutstandingLoansCount,
    int LateLoansCount,
    int DisbursementsThisMonthCount,
    decimal DisbursementsThisMonthAmount,
    int RepaymentsThisMonthCount,
    decimal RepaymentsThisMonthAmount,
    int PendingLoanApplicationsCount,
    int PendingStaffOnboardingCount,
    LoanPortfolioSummary LoanPortfolio);

/// <summary>All-time loan amounts by stage, with the number of loans behind each (repayments count transactions).</summary>
public record LoanPortfolioSummary(
    decimal RequestedAmount,
    int RequestedCount,
    decimal ApprovedAmount,
    int ApprovedCount,
    decimal RejectedAmount,
    int RejectedCount,
    decimal PendingApprovalAmount,
    int PendingApprovalCount,
    decimal RepaidAmount,
    int RepaidCount,
    /// <summary>Unpaid principal and interest on overdue instalments of loans not yet past their final repayment date.</summary>
    decimal LateRepaymentAmount,
    int LateRepaymentCount,
    /// <summary>Outstanding principal and interest on the defaulted loans.</summary>
    decimal DefaultedAmount,
    int DefaultedCount);

// ---- Super admin pending actions ----

/// <summary>Everything waiting on a super admin, grouped by kind; <c>Total</c> is what the control portal's bell shows.</summary>
public record PendingActionsResponse(int Total, IReadOnlyList<PendingActionGroupResponse> Groups);

/// <summary>One kind of pending item. <c>Items</c> are the newest <c>Count</c>-capped few; <c>Link</c> opens the full list in the control portal.</summary>
public record PendingActionGroupResponse(string Key, string Title, string Link, int Count, IReadOnlyList<PendingActionItemResponse> Items);

/// <summary>One pending item and where it opens in the control portal.</summary>
public record PendingActionItemResponse(int Id, string Title, string? Detail, string Link, DateTime? CreatedAt);

public record PendingActionsSummaryResponse(int Total);
