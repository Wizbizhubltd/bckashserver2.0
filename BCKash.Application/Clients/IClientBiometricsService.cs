using BCKash.Domain.Clients;

namespace BCKash.Application.Clients;

public enum BiometricOutcome
{
    Success,

    /// <summary>The capture finished but didn't pass — not live enough, or (for a loan) not the enrolled face. <see cref="BiometricResult.Error"/> says which.</summary>
    Rejected,

    /// <summary>The person hasn't finished the capture yet.</summary>
    InProgress,
    NotFound,

    /// <summary>A loan capture needs the client to have been enrolled first.</summary>
    NotEnrolled,

    /// <summary>The enrollment face can't be replaced once the client is approved.</summary>
    EnrollmentLocked,

    /// <summary>The loan isn't this client's, or isn't waiting to be disbursed.</summary>
    LoanMismatch,

    /// <summary>Face capture isn't configured (no AWS credentials).</summary>
    Unavailable,
}

public record BiometricResult(BiometricOutcome Outcome, ClientBiometric? Capture = null, string? Error = null, string? SessionId = null, string? Region = null);

/// <summary>Whether one client on a loan has passed the face match needed to disburse it.</summary>
/// <summary><c>LastFailure*</c>: the client's latest failed face match for the loan since they last passed — null when there's none.</summary>
public record LoanFaceCheck(
    int ClientId, string? ClientName, bool Enrolled, bool Verified, decimal? Similarity, DateTime? VerifiedAt,
    string? LastFailureReason = null, decimal? LastFailureSimilarity = null, DateTime? LastFailedAt = null);

/// <summary>
/// Clients' face biometrics. A liveness-checked enrollment capture becomes the client's biometric
/// record and profile picture. Before a loan is disbursed, each client receiving money is captured
/// again and must match their enrollment face at or above the configured threshold (90).
/// </summary>
public interface IClientBiometricsService
{
    bool IsConfigured { get; }

    decimal FaceMatchThreshold { get; }

    decimal LivenessThreshold { get; }

    Task<BiometricResult> StartAsync(Client client, string purpose, int? loanId, CancellationToken cancellationToken = default);

    Task<BiometricResult> CompleteAsync(Client client, string sessionId, CancellationToken cancellationToken = default);

    Task<LivenessStreamingCredentials> GetStreamingCredentialsAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Every client who must pass a face match before <paramref name="loanId"/> can be disbursed, and whether they have.</summary>
    Task<IReadOnlyList<LoanFaceCheck>> LoanChecksAsync(int loanId, CancellationToken cancellationToken = default);
}
