namespace BCKash.Application.Clients;

public enum LivenessOutcome
{
    /// <summary>The check finished; <see cref="LivenessResult.Confidence"/> says how sure it is that a live person was there.</summary>
    Completed,

    /// <summary>The person hasn't finished the check yet.</summary>
    InProgress,

    /// <summary>The check failed, expired, or never started.</summary>
    Failed,
}

/// <summary>A finished liveness check: how confident it is the face was live, and the best frame of it.</summary>
public record LivenessResult(LivenessOutcome Outcome, float? Confidence = null, byte[]? ReferenceImage = null, string? Error = null);

/// <summary>Short-lived AWS credentials that allow only streaming a liveness session from the browser.</summary>
public record LivenessStreamingCredentials(string AccessKeyId, string SecretAccessKey, string SessionToken, DateTime ExpiresAtUtc);

/// <summary>
/// Face biometrics (AWS Rekognition): Face Liveness proves a live person is in front of the camera,
/// not a photo, screen or mask, and returns a reference image of their face; face comparison scores
/// how alike two faces are (0–100).
/// </summary>
public interface IFaceBiometrics
{
    /// <summary>The AWS region the browser streams the liveness session to.</summary>
    string LivenessRegion { get; }

    Task<string> CreateLivenessSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>Credentials for the browser, valid for about 15 minutes and good for nothing but streaming a liveness session.</summary>
    Task<LivenessStreamingCredentials> GetStreamingCredentialsAsync(string requesterName, CancellationToken cancellationToken = default);

    Task<LivenessResult> GetLivenessResultAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>The highest similarity (0–100) between the face in <paramref name="source"/> and any face in <paramref name="target"/>, or 0 when none is found.</summary>
    Task<float> CompareFacesAsync(byte[] source, byte[] target, CancellationToken cancellationToken = default);
}
