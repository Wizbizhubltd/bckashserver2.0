using System.Collections.Concurrent;
using System.Text;
using BCKash.Application.Clients;

namespace BCKash.Api.IntegrationTests;

/// <summary>
/// The test host's stand-in for AWS Rekognition. Each liveness session captures whichever face and
/// confidence are set on <see cref="NextFace"/>/<see cref="NextConfidence"/> when it's created; two
/// captures of the same face compare at 99.5% and different faces at 12%.
/// </summary>
public class FakeFaceBiometrics : IFaceBiometrics
{
    private readonly ConcurrentDictionary<string, (string Face, float Confidence)> _sessions = new();

    /// <summary>Whose face the next session "sees". Tests running in parallel use distinct face names.</summary>
    public string NextFace { get; set; } = "face-a";

    public float NextConfidence { get; set; } = 99;

    public string LivenessRegion => "eu-west-1";

    public Task<string> CreateLivenessSessionAsync(CancellationToken cancellationToken = default)
    {
        var sessionId = Guid.NewGuid().ToString();
        _sessions[sessionId] = (NextFace, NextConfidence);
        return Task.FromResult(sessionId);
    }

    public Task<LivenessStreamingCredentials> GetStreamingCredentialsAsync(string requesterName, CancellationToken cancellationToken = default) =>
        Task.FromResult(new LivenessStreamingCredentials("ASIAFAKE", "fake-secret", "fake-token", DateTime.UtcNow.AddMinutes(15)));

    public Task<LivenessResult> GetLivenessResultAsync(string sessionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sessions.TryGetValue(sessionId, out var session)
            ? new LivenessResult(LivenessOutcome.Completed, session.Confidence, Encoding.UTF8.GetBytes(session.Face))
            : new LivenessResult(LivenessOutcome.Failed, Error: "No such session."));

    public Task<float> CompareFacesAsync(byte[] source, byte[] target, CancellationToken cancellationToken = default) =>
        Task.FromResult(source.AsSpan().SequenceEqual(target) ? 99.5f : 12f);
}
