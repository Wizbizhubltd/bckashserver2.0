namespace BCKash.Infrastructure.Clients;

/// <summary>Pass marks for face biometrics — Biometrics__* in .env.</summary>
public class BiometricsSettings
{
    public const string SectionName = "Biometrics";

    /// <summary>0–100: how alike a loan capture must be to the enrollment face.</summary>
    public decimal FaceMatchThreshold { get; set; } = 90;

    /// <summary>0–100: how sure the liveness check must be that a live person, not a photo, screen or mask, was in front of the camera.</summary>
    public decimal LivenessThreshold { get; set; } = 80;

    /// <summary>How long a passed loan face match stays good for disbursing that loan.</summary>
    public int LoanVerificationValidHours { get; set; } = 24;
}
