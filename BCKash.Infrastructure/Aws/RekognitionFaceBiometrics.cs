using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using BCKash.Application.Clients;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Aws;

/// <summary>
/// <see cref="IFaceBiometrics"/> on AWS Rekognition. Liveness sessions run in
/// <see cref="AwsSettings.LivenessRegion"/> (Face Liveness isn't available in every region); face
/// comparison runs in <see cref="AwsSettings.Region"/>. The browser never sees the account's own
/// keys: it gets a 15-minute federation token whose only permission is
/// <c>rekognition:StartFaceLivenessSession</c>.
/// </summary>
public class RekognitionFaceBiometrics : IFaceBiometrics
{
    private const string StreamingOnlyPolicy =
        """{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Action":"rekognition:StartFaceLivenessSession","Resource":"*"}]}""";

    private const int CredentialLifetimeSeconds = 900;

    private readonly AwsSettings _settings;
    private readonly AmazonRekognitionClient _liveness;
    private readonly AmazonRekognitionClient _faces;
    private readonly AmazonSecurityTokenServiceClient _sts;

    public RekognitionFaceBiometrics(IOptions<AwsSettings> settings)
    {
        _settings = settings.Value;
        var credentials = new BasicAWSCredentials(_settings.AccessKeyId, _settings.SecretAccessKey);
        _liveness = new AmazonRekognitionClient(credentials, RegionEndpoint.GetBySystemName(_settings.LivenessRegion));
        _faces = new AmazonRekognitionClient(credentials, RegionEndpoint.GetBySystemName(_settings.Region));
        _sts = new AmazonSecurityTokenServiceClient(credentials, RegionEndpoint.GetBySystemName(_settings.Region));
    }

    public string LivenessRegion => _settings.LivenessRegion;

    public async Task<string> CreateLivenessSessionAsync(CancellationToken cancellationToken = default)
    {
        // No output bucket: the reference image comes back in the results, and the application
        // stores it itself under the client's name.
        var response = await _liveness.CreateFaceLivenessSessionAsync(new CreateFaceLivenessSessionRequest(), cancellationToken);
        return response.SessionId;
    }

    public async Task<LivenessStreamingCredentials> GetStreamingCredentialsAsync(string requesterName, CancellationToken cancellationToken = default)
    {
        var response = await _sts.GetFederationTokenAsync(new GetFederationTokenRequest
        {
            // Federated user names are at most 32 characters.
            Name = requesterName.Length > 32 ? requesterName[..32] : requesterName,
            Policy = StreamingOnlyPolicy,
            DurationSeconds = CredentialLifetimeSeconds,
        }, cancellationToken);

        var c = response.Credentials;
        return new LivenessStreamingCredentials(c.AccessKeyId, c.SecretAccessKey, c.SessionToken, (c.Expiration ?? DateTime.UtcNow.AddSeconds(CredentialLifetimeSeconds)).ToUniversalTime());
    }

    public async Task<LivenessResult> GetLivenessResultAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        GetFaceLivenessSessionResultsResponse response;
        try
        {
            response = await _liveness.GetFaceLivenessSessionResultsAsync(new GetFaceLivenessSessionResultsRequest { SessionId = sessionId }, cancellationToken);
        }
        catch (SessionNotFoundException)
        {
            return new LivenessResult(LivenessOutcome.Failed, Error: "The face capture session has expired. Start a new capture.");
        }

        var status = response.Status?.Value;
        if (status == LivenessSessionStatus.SUCCEEDED.Value)
        {
            return new LivenessResult(LivenessOutcome.Completed, response.Confidence, response.ReferenceImage?.Bytes?.ToArray());
        }

        if (status == LivenessSessionStatus.CREATED.Value || status == LivenessSessionStatus.IN_PROGRESS.Value)
        {
            return new LivenessResult(LivenessOutcome.InProgress, Error: "The face capture hasn't finished yet.");
        }

        return new LivenessResult(LivenessOutcome.Failed, Error: status == LivenessSessionStatus.EXPIRED.Value
            ? "The face capture session has expired. Start a new capture."
            : "The face capture didn't complete. Try again in good light, facing the camera.");
    }

    public async Task<float> CompareFacesAsync(byte[] source, byte[] target, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _faces.CompareFacesAsync(new CompareFacesRequest
            {
                SourceImage = new Image { Bytes = new MemoryStream(source) },
                TargetImage = new Image { Bytes = new MemoryStream(target) },
                SimilarityThreshold = 0, // return every candidate; the caller applies the pass mark
                QualityFilter = QualityFilter.AUTO,
            }, cancellationToken);

            return response.FaceMatches is { Count: > 0 } matches ? matches.Max(m => m.Similarity ?? 0) : 0;
        }
        catch (InvalidParameterException)
        {
            // Rekognition raises this when it can't find a face in one of the images.
            return 0;
        }
    }
}
