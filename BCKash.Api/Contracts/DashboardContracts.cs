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
    int PendingStaffOnboardingCount);
