using BCKash.SharedKernel;

namespace BCKash.Domain.Loans;

/// <summary>Maps the legacy `loans` table (BRD §6.5).</summary>
public class Loan : IHasTimestamps, ISoftDelete, IAuditable
{
    public int Id { get; set; }

    public LoanClientType ClientType { get; set; } = LoanClientType.Client;

    public int? LoanProductId { get; set; }

    /// <summary>References `clients.id` — outside this table group, kept as a plain scalar.</summary>
    public int? ClientId { get; set; }

    public string? OldClientId { get; set; }

    /// <summary>References `offices.id` — outside this table group, kept as a plain scalar.</summary>
    public int? OfficeId { get; set; }

    /// <summary>References `groups.id` — outside this table group, kept as a plain scalar.</summary>
    public int? GroupId { get; set; }

    /// <summary>References `funds.id` — outside this table group, kept as a plain scalar.</summary>
    public int? FundId { get; set; }

    public int? LoanPurposeId { get; set; }

    /// <summary>References `currencies.id` — outside this table group, kept as a plain scalar.</summary>
    public int? CurrencyId { get; set; }

    public int Decimals { get; set; } = 2;

    public string? AccountNumber { get; set; }
    public string? ExternalId { get; set; }

    /// <summary>References `users.id` — outside this table group, kept as a plain scalar.</summary>
    public int? LoanOfficerId { get; set; }

    public decimal? Principal { get; set; }
    public decimal? AppliedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal? PrincipalDerived { get; set; }
    public decimal? InterestDerived { get; set; }
    public decimal? FeesDerived { get; set; }
    public decimal? PenaltyDerived { get; set; }
    public decimal? DisbursementFees { get; set; }
    public decimal? ProcessingFee { get; set; }

    public int? LoanTerm { get; set; }
    public FrequencyType? LoanTermType { get; set; }
    public int? RepaymentFrequency { get; set; }
    public FrequencyType? RepaymentFrequencyType { get; set; }

    public bool OverrideInterest { get; set; }
    public decimal? InterestRate { get; set; }
    public decimal? OverrideInterestRate { get; set; }
    public InterestRateFrequencyType? InterestRateType { get; set; }

    public DateOnly? ExpectedDisbursementDate { get; set; }
    public DateOnly? DisbursementDate { get; set; }
    public DateOnly? ExpectedMaturityDate { get; set; }
    public DateOnly? ExpectedFirstRepaymentDate { get; set; }
    public int? RepaymentsNumber { get; set; }
    public DateOnly? FirstRepaymentDate { get; set; }

    public LoanInterestMethod? InterestMethod { get; set; }
    public LoanAmortizationMethod? AmortizationMethod { get; set; }

    public int? GraceOnInterestCharged { get; set; }
    public int? GraceOnPrincipal { get; set; }
    public int? GraceOnInterestPayment { get; set; }

    public LoanStatus Status { get; set; } = LoanStatus.Pending;

    /// <summary>All of the *_by_id columns below reference `users.id` — outside this table group.</summary>
    public int? CreatedById { get; set; }
    public int? ModifiedById { get; set; }
    public int? ApprovedById { get; set; }
    public int? NeedChangesById { get; set; }
    public int? WithdrawnById { get; set; }
    public int? DeclinedById { get; set; }
    public int? WrittenOffById { get; set; }
    public int? DisbursedById { get; set; }
    public int? RescheduledById { get; set; }
    public int? ClosedById { get; set; }

    public DateOnly? CreatedDate { get; set; }
    public DateOnly? ModifiedDate { get; set; }
    public DateOnly? ApprovedDate { get; set; }
    public DateOnly? NeedChangesDate { get; set; }
    public DateOnly? WithdrawnDate { get; set; }
    public DateOnly? DeclinedDate { get; set; }
    public DateOnly? WrittenOffDate { get; set; }
    public DateOnly? RescheduledDate { get; set; }
    public DateOnly? ClosedDate { get; set; }

    public string? Month { get; set; }
    public string? Year { get; set; }

    public string? Notes { get; set; }
    public string? ApprovedNotes { get; set; }
    public string? DeclinedNotes { get; set; }
    public string? WrittenOffNotes { get; set; }
    public string? DisbursedNotes { get; set; }
    public string? WithdrawnNotes { get; set; }
    public string? RescheduledNotes { get; set; }
    public string? ClosedNotes { get; set; }

    /// <summary>New in Phase 5 — the legacy schema has no such columns. FR-LN-25's NPA flag and income-suspension state, computed by LoanNpaService and persisted here so they're queryable without recomputing on every read.</summary>
    public bool IsNpa { get; set; }
    public bool IncomeSuspended { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public LoanProduct? LoanProduct { get; set; }
    public LoanPurpose? LoanPurpose { get; set; }

    public ICollection<LoanApplication> LoanApplications { get; set; } = new List<LoanApplication>();
    public ICollection<LoanCharge> LoanCharges { get; set; } = new List<LoanCharge>();
    public ICollection<LoanRepaymentSchedule> LoanRepaymentSchedules { get; set; } = new List<LoanRepaymentSchedule>();
    public ICollection<LoanRescheduleRequest> LoanRescheduleRequests { get; set; } = new List<LoanRescheduleRequest>();
    public ICollection<LoanTransaction> LoanTransactions { get; set; } = new List<LoanTransaction>();
    public ICollection<Guarantor> Guarantors { get; set; } = new List<Guarantor>();
    public ICollection<Collateral> Collaterals { get; set; } = new List<Collateral>();
    public ICollection<GroupLoanAllocation> GroupLoanAllocations { get; set; } = new List<GroupLoanAllocation>();
}
