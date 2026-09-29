using BCKash.SharedKernel;

namespace BCKash.Domain.Clients;

/// <summary>
/// One face capture of a client — a liveness-checked video selfie. The enrollment capture becomes
/// the client's biometric record and profile picture; a loan capture is compared against it before
/// that loan can be disbursed.
/// </summary>
public class ClientBiometric : IHasTimestamps
{
    public const string EnrollmentPurpose = "enrollment";
    public const string LoanPurpose = "loan";

    public const string PendingStatus = "pending";
    public const string PassedStatus = "passed";
    public const string FailedStatus = "failed";

    public int Id { get; set; }
    public int ClientId { get; set; }

    /// <summary><see cref="EnrollmentPurpose"/> or <see cref="LoanPurpose"/>.</summary>
    public string Purpose { get; set; } = EnrollmentPurpose;

    /// <summary>The loan being disbursed, for a <see cref="LoanPurpose"/> capture.</summary>
    public int? LoanId { get; set; }

    /// <summary>The Rekognition Face Liveness session.</summary>
    public string SessionId { get; set; } = string.Empty;
    public string Status { get; set; } = PendingStatus;

    /// <summary>0–100: how sure the liveness check is that a live person was in front of the camera.</summary>
    public decimal? LivenessConfidence { get; set; }

    /// <summary>0–100: how alike this face is to the enrollment face (loan captures only).</summary>
    public decimal? Similarity { get; set; }

    /// <summary>Where the captured face image is stored.</summary>
    public string? ImageLocation { get; set; }
    public string? FailureReason { get; set; }
    public int? CreatedById { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
