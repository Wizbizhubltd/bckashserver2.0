using System.Globalization;
using System.Text.Json;
using BCKash.Application.Clients;
using BCKash.Application.Files;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BCKash.Infrastructure.Clients;

/// <summary>See <see cref="IClientBiometricsService"/>.</summary>
public class ClientBiometricsService : IClientBiometricsService
{
    // A loan can still be disbursed from any of these; a List so EF can translate Contains.
    private static readonly List<LoanStatus> AwaitingDisbursement = [LoanStatus.New, LoanStatus.Pending, LoanStatus.Approved, LoanStatus.NeedChanges];

    private readonly BCKashDbContext _db;
    private readonly IFaceBiometrics? _faces;
    private readonly IFileStorageService _files;
    private readonly ICurrentUserContext _currentUser;
    private readonly BiometricsSettings _settings;

    public ClientBiometricsService(
        BCKashDbContext db,
        IFileStorageService files,
        ICurrentUserContext currentUser,
        IOptions<BiometricsSettings> settings,
        IEnumerable<IFaceBiometrics> faces)
    {
        _db = db;
        _files = files;
        _currentUser = currentUser;
        _settings = settings.Value;
        _faces = faces.FirstOrDefault();
    }

    public bool IsConfigured => _faces is not null;

    public decimal FaceMatchThreshold => _settings.FaceMatchThreshold;

    public decimal LivenessThreshold => _settings.LivenessThreshold;

    public async Task<BiometricResult> StartAsync(Client client, string purpose, int? loanId, CancellationToken cancellationToken = default)
    {
        if (_faces is null)
        {
            return Unavailable();
        }

        var enrollment = await EnrollmentAsync(client.Id, cancellationToken);
        if (purpose == ClientBiometric.EnrollmentPurpose)
        {
            // Once the client is approved their enrolled face is what loans are checked against — it can't be swapped.
            if (enrollment is not null && ClientDeletionRules.WasApproved(client))
            {
                return new BiometricResult(BiometricOutcome.EnrollmentLocked,
                    Error: "This client's face is already enrolled and they've been approved, so it can't be replaced.");
            }

            loanId = null;
        }
        else
        {
            if (enrollment is null)
            {
                return new BiometricResult(BiometricOutcome.NotEnrolled,
                    Error: "This client has no enrolled face to compare against. The staff member who onboarded them must capture it first.");
            }

            var loan = loanId.HasValue ? await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken) : null;
            if (loan is null || !AwaitingDisbursement.Contains(loan.Status) || !(await RequiredClientIdsAsync(loan, cancellationToken)).Contains(client.Id))
            {
                return new BiometricResult(BiometricOutcome.LoanMismatch, Error: "This loan isn't this client's, or it isn't waiting to be disbursed.");
            }
        }

        var sessionId = await _faces.CreateLivenessSessionAsync(cancellationToken);
        var capture = new ClientBiometric
        {
            ClientId = client.Id,
            Purpose = purpose,
            LoanId = loanId,
            SessionId = sessionId,
            Status = ClientBiometric.PendingStatus,
            CreatedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.ClientBiometrics.Add(capture);
        await _db.SaveChangesAsync(cancellationToken);

        return new BiometricResult(BiometricOutcome.Success, capture, SessionId: sessionId, Region: _faces.LivenessRegion);
    }

    public async Task<BiometricResult> CompleteAsync(Client client, string sessionId, CancellationToken cancellationToken = default)
    {
        if (_faces is null)
        {
            return Unavailable();
        }

        var capture = await _db.ClientBiometrics.FirstOrDefaultAsync(b => b.ClientId == client.Id && b.SessionId == sessionId, cancellationToken);
        if (capture is null)
        {
            return new BiometricResult(BiometricOutcome.NotFound);
        }

        if (capture.Status != ClientBiometric.PendingStatus)
        {
            return new BiometricResult(capture.Status == ClientBiometric.PassedStatus ? BiometricOutcome.Success : BiometricOutcome.Rejected, capture, capture.FailureReason);
        }

        var liveness = await _faces.GetLivenessResultAsync(sessionId, cancellationToken);
        if (liveness.Outcome == LivenessOutcome.InProgress)
        {
            return new BiometricResult(BiometricOutcome.InProgress, capture, liveness.Error);
        }

        capture.CompletedAt = DateTime.UtcNow;
        capture.UpdatedAt = DateTime.UtcNow;
        capture.LivenessConfidence = liveness.Confidence.HasValue ? Math.Round((decimal)liveness.Confidence.Value, 2) : null;

        if (liveness.Outcome == LivenessOutcome.Failed || liveness.ReferenceImage is not { Length: > 0 })
        {
            return await RejectAsync(client, capture, liveness.Error ?? "No face was captured. Try again facing the camera in good light.", cancellationToken);
        }

        if (capture.LivenessConfidence < _settings.LivenessThreshold)
        {
            return await RejectAsync(client, capture,
                $"Couldn't confirm a live person in front of the camera (score {capture.LivenessConfidence:0.#} of the {_settings.LivenessThreshold:0.#} needed). Try again without glasses or a hat, in good light.",
                cancellationToken);
        }

        var name = ClientName(client);
        if (capture.Purpose == ClientBiometric.LoanPurpose)
        {
            var enrollment = await EnrollmentAsync(client.Id, cancellationToken);
            var enrolledFace = enrollment?.ImageLocation is null ? null : await _files.ReadAsync(enrollment.ImageLocation, "enrollment.jpg", cancellationToken);
            if (enrolledFace is null)
            {
                return await RejectAsync(client, capture, "The client's enrolled face couldn't be loaded, so there's nothing to compare against.", cancellationToken);
            }

            byte[] enrolledBytes;
            await using (var stream = enrolledFace.Content)
            using (var copy = new MemoryStream())
            {
                await stream.CopyToAsync(copy, cancellationToken);
                enrolledBytes = copy.ToArray();
            }

            capture.Similarity = Math.Round((decimal)await _faces.CompareFacesAsync(enrolledBytes, liveness.ReferenceImage, cancellationToken), 2);
            capture.ImageLocation = (await _files.SaveAsync(new MemoryStream(liveness.ReferenceImage), $"{name} - loan {capture.LoanId} face match.jpg", cancellationToken)).Location;

            if (capture.Similarity < _settings.FaceMatchThreshold)
            {
                return await RejectAsync(client, capture,
                    $"The face doesn't match the client's enrolled face ({capture.Similarity:0.#}% similar; {_settings.FaceMatchThreshold:0.#}% needed).",
                    cancellationToken);
            }

            capture.Status = ClientBiometric.PassedStatus;
            Audit(client, "Face match passed", capture);
            await _db.SaveChangesAsync(cancellationToken);
            return new BiometricResult(BiometricOutcome.Success, capture);
        }

        // Enrollment: the captured face becomes the client's biometric record and profile picture.
        capture.ImageLocation = (await _files.SaveAsync(new MemoryStream(liveness.ReferenceImage), $"{name} - biometric enrollment.jpg", cancellationToken)).Location;
        capture.Status = ClientBiometric.PassedStatus;
        client.Picture = capture.ImageLocation;
        client.BiometricEnrolledAt = DateTime.UtcNow;
        Audit(client, "Face enrolled", capture);
        await _db.SaveChangesAsync(cancellationToken);
        return new BiometricResult(BiometricOutcome.Success, capture);
    }

    public Task<LivenessStreamingCredentials> GetStreamingCredentialsAsync(int userId, CancellationToken cancellationToken = default) =>
        _faces is null
            ? throw new InvalidOperationException("Face capture isn't configured.")
            : _faces.GetStreamingCredentialsAsync($"bckash-liveness-{userId}", cancellationToken);

    public async Task<IReadOnlyList<LoanFaceCheck>> LoanChecksAsync(int loanId, CancellationToken cancellationToken = default)
    {
        var loan = await _db.Loans.FirstOrDefaultAsync(l => l.Id == loanId, cancellationToken);
        if (loan is null)
        {
            return [];
        }

        var clientIds = await RequiredClientIdsAsync(loan, cancellationToken);
        var clients = await _db.Clients.Where(c => clientIds.Contains(c.Id)).ToListAsync(cancellationToken);
        var enrolled = await _db.ClientBiometrics
            .Where(b => clientIds.Contains(b.ClientId) && b.Purpose == ClientBiometric.EnrollmentPurpose && b.Status == ClientBiometric.PassedStatus)
            .Select(b => b.ClientId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var validFrom = DateTime.UtcNow.AddHours(-_settings.LoanVerificationValidHours);
        var matches = await _db.ClientBiometrics
            .Where(b => b.LoanId == loanId && b.Purpose == ClientBiometric.LoanPurpose && b.Status == ClientBiometric.PassedStatus && b.CompletedAt >= validFrom)
            .ToListAsync(cancellationToken);
        var failures = await _db.ClientBiometrics
            .Where(b => b.LoanId == loanId && b.Purpose == ClientBiometric.LoanPurpose && b.Status == ClientBiometric.FailedStatus && b.CreatedAt >= validFrom)
            .ToListAsync(cancellationToken);

        return clients.Select(c =>
        {
            var match = matches.Where(m => m.ClientId == c.Id).OrderByDescending(m => m.CompletedAt).FirstOrDefault();
            // A failed attempt only matters until the client passes.
            var failure = match is not null
                ? null
                : failures.Where(f => f.ClientId == c.Id).OrderByDescending(f => f.CompletedAt ?? f.CreatedAt).ThenByDescending(f => f.Id).FirstOrDefault();
            return new LoanFaceCheck(
                c.Id, ClientName(c), enrolled.Contains(c.Id), match is not null, match?.Similarity, match?.CompletedAt,
                failure?.FailureReason, failure?.Similarity, failure?.CompletedAt ?? failure?.CreatedAt);
        }).ToList();
    }

    /// <summary>Who receives money from the loan: its group allocations, else its client, else the group's current members.</summary>
    private async Task<List<int>> RequiredClientIdsAsync(Loan loan, CancellationToken cancellationToken)
    {
        var allocated = await _db.GroupLoanAllocations
            .Where(a => a.LoanId == loan.Id && a.ClientId != null)
            .Select(a => a.ClientId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (allocated.Count > 0)
        {
            return allocated;
        }

        if (loan.ClientId.HasValue)
        {
            return [loan.ClientId.Value];
        }

        return loan.GroupId.HasValue
            ? await _db.GroupClients.Where(gc => gc.GroupId == loan.GroupId && gc.RemovedAt == null && gc.ClientId != null).Select(gc => gc.ClientId!.Value).ToListAsync(cancellationToken)
            : [];
    }

    private Task<ClientBiometric?> EnrollmentAsync(int clientId, CancellationToken cancellationToken) =>
        _db.ClientBiometrics
            .Where(b => b.ClientId == clientId && b.Purpose == ClientBiometric.EnrollmentPurpose && b.Status == ClientBiometric.PassedStatus)
            .OrderByDescending(b => b.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<BiometricResult> RejectAsync(Client client, ClientBiometric capture, string reason, CancellationToken cancellationToken)
    {
        capture.Status = ClientBiometric.FailedStatus;
        capture.FailureReason = reason;
        Audit(client, capture.Purpose == ClientBiometric.LoanPurpose ? "Face match failed" : "Face capture failed", capture);
        await _db.SaveChangesAsync(cancellationToken);
        return new BiometricResult(BiometricOutcome.Rejected, capture, reason);
    }

    private void Audit(Client client, string action, ClientBiometric capture) =>
        _db.AuditTrail.Add(new AuditTrailEntry
        {
            UserId = _currentUser.UserId,
            OfficeId = client.OfficeId,
            Module = nameof(Client),
            Action = action,
            Name = "Client biometrics",
            EntityId = client.Id,
            Notes = JsonSerializer.Serialize(new
            {
                loanId = capture.LoanId,
                livenessConfidence = capture.LivenessConfidence,
                similarity = capture.Similarity,
                reason = capture.FailureReason,
            }),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });

    private static string ClientName(Client client) =>
        !string.IsNullOrWhiteSpace(client.DisplayName)
            ? client.DisplayName
            : string.Join(' ', new[] { client.FirstName, client.MiddleName, client.LastName }.Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } name
                ? name
                : string.Create(CultureInfo.InvariantCulture, $"Client {client.Id}");

    private static BiometricResult Unavailable() =>
        new(BiometricOutcome.Unavailable, Error: "Face capture isn't configured — set the Aws__AccessKeyId and Aws__SecretAccessKey settings.");
}
