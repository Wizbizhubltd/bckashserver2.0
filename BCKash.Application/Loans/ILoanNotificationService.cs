namespace BCKash.Application.Loans;

/// <summary>A message the customer gets about their loan (Settings → Notifications).</summary>
public enum LoanNotificationKind
{
    LoanApproved,
    LoanDisbursed,
    PaymentReceived,
    UpcomingRepayment,
    MissedRepayment,
    LoanOverdue,
}

/// <summary>
/// The legacy `settings` keys behind one kind of message: its email and SMS switches, and its
/// templates. A switch that has never been saved is off. A blank template falls back to the default.
/// </summary>
public record LoanNotificationKeys(
    string EmailSwitch,
    string SmsSwitch,
    string EmailSubject,
    string EmailTemplate,
    string SmsTemplate,
    string DefaultSubject,
    string DefaultMessage)
{
    public static readonly IReadOnlyDictionary<LoanNotificationKind, LoanNotificationKeys> For = new Dictionary<LoanNotificationKind, LoanNotificationKeys>
    {
        [LoanNotificationKind.LoanApproved] = new(
            "loan_approved_auto_email", "loan_approved_auto_sms",
            "loan_approved_email_subject", "loan_approved_email_template", "loan_approved_sms_template",
            "Loan approved",
            "Dear {clientName}, your loan {loanNumber} has been approved for {approvedAmount}. Thank you."),
        [LoanNotificationKind.LoanDisbursed] = new(
            "loan_disbursed_auto_email", "loan_disbursed_auto_sms",
            "loan_disbursed_email_subject", "loan_disbursed_email_template", "loan_disbursed_sms_template",
            "Loan disbursed",
            "Dear {clientName}, your loan {loanNumber} has been disbursed. First payment: {firstPaymentAmount} on {firstPaymentDate}. Thank you."),
        [LoanNotificationKind.PaymentReceived] = new(
            "auto_payment_receipt_email", "auto_payment_receipt_sms",
            "payment_received_email_subject", "payment_received_email_template", "payment_received_sms_template",
            "Payment received",
            "Dear {clientName}, we have received your payment of {paymentAmount} for loan {loanNumber}. New loan balance: {loanBalance}. Thank you."),
        [LoanNotificationKind.UpcomingRepayment] = new(
            "auto_repayment_email_reminder", "auto_repayment_sms_reminder",
            "loan_payment_reminder_subject", "loan_payment_reminder_email_template", "loan_payment_reminder_sms_template",
            "Upcoming loan repayment",
            "Dear {clientName}, you have an upcoming payment of {paymentAmount} due on {paymentDate} for loan {loanNumber}. Please make your payment. Thank you."),
        [LoanNotificationKind.MissedRepayment] = new(
            "auto_overdue_repayment_email_reminder", "auto_overdue_repayment_sms_reminder",
            "missed_payment_email_subject", "missed_payment_email_template", "missed_payment_sms_template",
            "Missed loan repayment",
            "Dear {clientName}, you missed a payment of {paymentAmount} which was due on {paymentDate} for loan {loanNumber}. Please make your payment. Thank you."),
        [LoanNotificationKind.LoanOverdue] = new(
            "auto_overdue_loan_email_reminder", "auto_overdue_loan_sms_reminder",
            "loan_overdue_email_subject", "loan_overdue_email_template", "loan_overdue_sms_template",
            "Loan overdue",
            "Dear {clientName}, your loan {loanNumber} is overdue, with {loanBalance} outstanding. Please make your payment. Thank you."),
    };

    /// <summary>How many days before the due date the upcoming-repayment reminder goes out.</summary>
    public const string ReminderDaysKey = "auto_repayment_days";

    public const int DefaultReminderDays = 3;

    /// <summary>
    /// How far back a reminder run looks for repayments or loans that have just become overdue. It catches
    /// up on a few missed days without texting every customer about years-old arrears the first time it's on.
    /// </summary>
    public const int CatchUpDays = 7;
}

public record LoanReminderRunResult(int UpcomingRepayments, int MissedRepayments, int OverdueLoans);

/// <summary>
/// The customer's loan messages, each sent by email and/or SMS as its switches in Settings →
/// Notifications say, worded by its templates. Sending is queued, and failures are logged rather
/// than surfaced: a message never holds up or fails the action behind it. SMS also obeys the master
/// "SMS sending" switch.
/// </summary>
public interface ILoanNotificationService
{
    Task LoanApprovedAsync(int loanId, CancellationToken cancellationToken = default);

    Task LoanDisbursedAsync(int loanId, CancellationToken cancellationToken = default);

    Task PaymentReceivedAsync(int loanId, decimal amount, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// The upcoming-repayment, missed-repayment and loan-overdue reminders that have fallen due. Each goes
    /// once per instalment (or once per loan, for overdue), so running it more than once a day is harmless.
    /// </summary>
    Task<LoanReminderRunResult> SendDueRemindersAsync(DateOnly? asOf = null, CancellationToken cancellationToken = default);
}
