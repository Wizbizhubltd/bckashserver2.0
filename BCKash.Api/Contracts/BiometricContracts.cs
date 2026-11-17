namespace BCKash.Api.Contracts;

/// <summary>"enrollment" (the client's face on record) or "loan" (a face match before disbursing <c>LoanId</c>).</summary>
public record StartFaceCaptureRequest(string Purpose, int? LoanId);

/// <summary>Everything the browser needs to run the liveness check: the session and the region to stream to.</summary>
public record FaceCaptureSessionResponse(int CaptureId, string SessionId, string Region);

public record FaceCaptureResponse(
    int Id,
    string Purpose,
    int? LoanId,
    string Status,
    decimal? LivenessConfidence,
    decimal? Similarity,
    string? FailureReason,
    string? CapturedByName,
    DateTime? CreatedAt,
    DateTime? CompletedAt);

/// <summary>A client's biometrics: whether their face is enrolled, whether the viewer may capture it, and every capture so far.</summary>
public record ClientBiometricsResponse(
    bool Configured,
    decimal FaceMatchThreshold,
    decimal LivenessThreshold,
    bool Enrolled,
    DateTime? EnrolledAt,
    FaceCaptureResponse? Enrollment,
    bool CanEnroll,
    string? EnrollBlockedReason,
    IReadOnlyList<FaceCaptureResponse> Captures,
    /// <summary>Whether face capture is mandatory (Settings → Loan): needed to approve the client and raise a loan.</summary>
    bool Required = true,
    /// <summary>Whether the client has a profile picture — their enrolled face, or a passport photo uploaded while face capture is off.</summary>
    bool HasPhoto = false,
    /// <summary>Whether the caller can upload a passport photo now: face capture is off, the client's face isn't enrolled, and they may document the client.</summary>
    bool CanUploadPhoto = false);

public record LivenessCredentialsResponse(string AccessKeyId, string SecretAccessKey, string SessionToken, DateTime Expiration);

/// <summary>A client on a loan and whether they've passed the face match needed to disburse it.</summary>
/// <summary><c>CanVerify</c>: whether the viewer's role may run this face match (Settings → Loan → face match roles).</summary>
public record LoanFaceCheckResponse(
    int ClientId, string? ClientName, bool Enrolled, bool Verified, decimal? Similarity, DateTime? VerifiedAt, bool CanVerify = false,
    string? LastFailureReason = null, decimal? LastFailureSimilarity = null, DateTime? LastFailedAt = null,
    /// <summary>Whether face capture is mandatory (Settings → Loan); when it isn't, the loan can be disbursed without the match.</summary>
    bool Required = true);
