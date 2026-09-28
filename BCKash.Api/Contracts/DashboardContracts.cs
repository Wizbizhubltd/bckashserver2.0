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
