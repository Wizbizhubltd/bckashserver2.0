using System.Security.Claims;
using BCKash.Api.Contracts;
using BCKash.Application.Auth;
using BCKash.Application.Clients;
using BCKash.Application.Identity;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

/// <summary>
/// Clients' face biometrics (see IClientBiometricsService). The browser runs AWS's Face Liveness
/// check against a session made here, with short-lived credentials from <c>POST /biometrics/credentials</c>;
/// completing the capture checks liveness and, for a loan, compares the face with the client's enrolled one.
/// Enrolling is part of the client's documentation, so only the staff member who onboarded them does it;
/// loan face matches are done by whoever services the loan.
/// </summary>
[ApiController]
[Route("api/v1")]
[Authorize]
public class ClientBiometricsController : ControllerBase
{
    private const string ServicingPermission = "loan-servicing.manage";
    private const string ClientsPermission = "clients.manage";

    private readonly BCKashDbContext _db;
    private readonly IClientAccess _access;
    private readonly IClientBiometricsService _biometrics;
    private readonly IOfficeScope _scope;

    public ClientBiometricsController(BCKashDbContext db, IClientAccess access, IClientBiometricsService biometrics, IOfficeScope scope)
    {
        _db = db;
        _access = access;
        _biometrics = biometrics;
        _scope = scope;
    }

    [HttpGet("clients/{clientId:int}/biometrics")]
    public async Task<ActionResult<ClientBiometricsResponse>> Get(int clientId, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(clientId, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        var captures = await _db.ClientBiometrics
            .Where(b => b.ClientId == clientId)
            .OrderByDescending(b => b.CreatedAt)
            .ThenByDescending(b => b.Id)
            .Take(50)
            .ToListAsync(cancellationToken);
        var responses = await ToResponsesAsync(captures, cancellationToken);
        var enrollment = responses
            .Where(c => c.Purpose == ClientBiometric.EnrollmentPurpose && c.Status == ClientBiometric.PassedStatus)
            .OrderByDescending(c => c.CompletedAt)
            .FirstOrDefault();

        string? blocked = null;
        if (!_biometrics.IsConfigured)
        {
            blocked = "Face capture isn't configured on the server yet.";
        }
        else if (!await _access.IsOnboarderAsync(client, cancellationToken))
        {
            blocked = "Only the staff member who onboarded this client can capture their face.";
        }
        else if (enrollment is not null && ClientDeletionRules.WasApproved(client))
        {
            blocked = "The client is approved, so their enrolled face can't be replaced.";
        }

        return Ok(new ClientBiometricsResponse(
            _biometrics.IsConfigured,
            _biometrics.FaceMatchThreshold,
            _biometrics.LivenessThreshold,
            enrollment is not null,
            client.BiometricEnrolledAt,
            enrollment,
            blocked is null,
            blocked,
            responses,
            await FaceCapturePolicy.IsRequiredAsync(_db, cancellationToken)));
    }

    [HttpPost("clients/{clientId:int}/biometrics/sessions")]
    public async Task<IActionResult> Start(int clientId, StartFaceCaptureRequest request, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(clientId, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        var purpose = request.Purpose?.Trim().ToLowerInvariant();
        if (purpose is not (ClientBiometric.EnrollmentPurpose or ClientBiometric.LoanPurpose))
        {
            return Problem(title: "Purpose must be 'enrollment' or 'loan'.", statusCode: StatusCodes.Status400BadRequest);
        }

        var denied = await DenyAsync(client, purpose, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var result = await _biometrics.StartAsync(client, purpose, request.LoanId, cancellationToken);
        return result.Outcome == BiometricOutcome.Success
            ? Ok(new FaceCaptureSessionResponse(result.Capture!.Id, result.SessionId!, result.Region!))
            : ToProblem(result);
    }

    [HttpPost("clients/{clientId:int}/biometrics/sessions/{sessionId}/complete")]
    public async Task<IActionResult> Complete(int clientId, string sessionId, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(clientId, cancellationToken);
        var capture = client is null ? null : await _db.ClientBiometrics.FirstOrDefaultAsync(b => b.ClientId == clientId && b.SessionId == sessionId, cancellationToken);
        if (client is null || capture is null)
        {
            return NotFound();
        }

        var denied = await DenyAsync(client, capture.Purpose, cancellationToken);
        if (denied is not null)
        {
            return denied;
        }

        var result = await _biometrics.CompleteAsync(client, sessionId, cancellationToken);
        return result.Outcome is BiometricOutcome.Success or BiometricOutcome.Rejected
            ? Ok((await ToResponsesAsync([result.Capture!], cancellationToken)).Single())
            : ToProblem(result);
    }

    /// <summary>Short-lived credentials the browser uses to stream the liveness check — good for nothing else.</summary>
    [HttpPost("biometrics/credentials")]
    public async Task<ActionResult<LivenessCredentialsResponse>> Credentials(CancellationToken cancellationToken)
    {
        if (!HasPermission(ClientsPermission) && !HasPermission(ServicingPermission) && !await CanRunLoanFaceMatchAsync(cancellationToken))
        {
            return Forbid();
        }

        if (!_biometrics.IsConfigured || !int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Problem(title: "Face capture isn't configured on the server yet.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var credentials = await _biometrics.GetStreamingCredentialsAsync(userId, cancellationToken);
        return Ok(new LivenessCredentialsResponse(credentials.AccessKeyId, credentials.SecretAccessKey, credentials.SessionToken, credentials.ExpiresAtUtc));
    }

    /// <summary>Everyone who must pass a face match before the loan can be disbursed, and whether they have.</summary>
    [HttpGet("loans/{loanId:int}/face-checks")]
    public async Task<ActionResult<IReadOnlyList<LoanFaceCheckResponse>>> LoanChecks(int loanId, CancellationToken cancellationToken)
    {
        var checks = await _biometrics.LoanChecksAsync(loanId, cancellationToken);
        var canVerify = await CanRunLoanFaceMatchAsync(cancellationToken);
        var required = await FaceCapturePolicy.IsRequiredAsync(_db, cancellationToken);
        var visible = new List<LoanFaceCheckResponse>();
        foreach (var check in checks)
        {
            if (await _access.FindVisibleAsync(check.ClientId, cancellationToken) is not null)
            {
                visible.Add(new LoanFaceCheckResponse(
                    check.ClientId, check.ClientName, check.Enrolled, check.Verified, check.Similarity, check.VerifiedAt, canVerify,
                    check.LastFailureReason, check.LastFailureSimilarity, check.LastFailedAt, required));
            }
        }

        return checks.Count > 0 && visible.Count == 0 ? NotFound() : Ok(visible);
    }

    /// <summary>Enrolling is the onboarding staff member's job; a loan face match is for the roles ticked in Settings (see <see cref="CanRunLoanFaceMatchAsync"/>).</summary>
    private async Task<IActionResult?> DenyAsync(Client client, string purpose, CancellationToken cancellationToken)
    {
        // The enrolled face has its own lock (the client's approval, even under an edit privilege), which
        // IClientBiometricsService enforces — here it only matters who is capturing.
        if (purpose == ClientBiometric.EnrollmentPurpose)
        {
            return await _access.IsOnboarderAsync(client, cancellationToken)
                ? null
                : Problem(title: "Only the staff member who onboarded this client can capture their face.", statusCode: StatusCodes.Status403Forbidden);
        }

        return await CanRunLoanFaceMatchAsync(cancellationToken)
            ? null
            : Problem(title: "Your role isn't allowed to run the face match before disbursement.", statusCode: StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// Whether the caller may run a loan face match: their user type must be ticked in Settings → Loan.
    /// Until that's saved — and for users with no user_type (see IOfficeScope) — it's anyone with the
    /// loan-servicing permission, as before.
    /// </summary>
    private async Task<bool> CanRunLoanFaceMatchAsync(CancellationToken cancellationToken)
    {
        var userType = await _scope.GetUserTypeAsync(cancellationToken);
        var saved = await _db.Settings
            .Where(s => s.SettingKey == LoanFaceMatchRoles.SettingKey)
            .OrderByDescending(s => s.Id)
            .Select(s => new { s.SettingValue })
            .FirstOrDefaultAsync(cancellationToken);
        var roles = saved is null ? null : LoanFaceMatchRoles.Parse(saved.SettingValue ?? string.Empty);

        return userType is null || roles is null ? HasPermission(ServicingPermission) : roles.Contains(userType);
    }

    private bool HasPermission(string slug) => User.HasClaim(AuthClaimTypes.Permission, slug);

    private ObjectResult ToProblem(BiometricResult result) => result.Outcome switch
    {
        BiometricOutcome.NotFound => Problem(title: "That face capture wasn't found.", statusCode: StatusCodes.Status404NotFound),
        BiometricOutcome.InProgress => Problem(title: result.Error ?? "The face capture hasn't finished yet.", statusCode: StatusCodes.Status409Conflict),
        BiometricOutcome.Unavailable => Problem(title: result.Error, statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => Problem(title: result.Error, statusCode: StatusCodes.Status409Conflict),
    };

    private async Task<List<FaceCaptureResponse>> ToResponsesAsync(IReadOnlyList<ClientBiometric> captures, CancellationToken cancellationToken)
    {
        var userIds = captures.Where(c => c.CreatedById.HasValue).Select(c => c.CreatedById!.Value).Distinct().ToList();
        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim(), cancellationToken);

        return captures.Select(c => new FaceCaptureResponse(
            c.Id, c.Purpose, c.LoanId, c.Status, c.LivenessConfidence, c.Similarity, c.FailureReason,
            c.CreatedById.HasValue ? names.GetValueOrDefault(c.CreatedById.Value) : null, c.CreatedAt, c.CompletedAt)).ToList();
    }
}
