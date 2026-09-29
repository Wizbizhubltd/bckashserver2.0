namespace BCKash.Infrastructure.Aws;

/// <summary>
/// AWS access for file storage (S3) and face biometrics (Rekognition) — set via Aws__* in .env.
/// Face Liveness isn't offered in every region (not London), so it has its own region.
/// </summary>
public class AwsSettings
{
    public const string SectionName = "Aws";

    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }

    /// <summary>Region for S3 and face comparison.</summary>
    public string Region { get; set; } = "eu-west-2";

    /// <summary>
    /// Region for Rekognition Face Liveness sessions. Liveness runs only in a few regions (see the
    /// Rekognition endpoints page); Europe (Ireland) is the nearest to London.
    /// </summary>
    public string LivenessRegion { get; set; } = "eu-west-1";

    /// <summary>Bucket every uploaded file goes to: images under img/, everything else under documents/.</summary>
    public string? S3Bucket { get; set; }

    public bool HasCredentials => !string.IsNullOrWhiteSpace(AccessKeyId) && !string.IsNullOrWhiteSpace(SecretAccessKey);
}
