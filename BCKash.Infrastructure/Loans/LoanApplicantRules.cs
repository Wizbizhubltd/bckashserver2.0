using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.Domain.Loans;
using BCKash.Infrastructure.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Loans;

/// <summary>
/// Who a loan can be raised for: an approved client whose face has been enrolled (see
/// ClientBiometricsService) — or, for a group, an approved group all of whose current members are.
/// A client sent back to pending (e.g. after an edit) can't take a loan until approved again. The face
/// requirement applies only while face capture is mandatory (see FaceCaptureRules).
/// </summary>
public static class LoanApplicantRules
{
    /// <summary>Why a loan can't be raised for this applicant, in plain words; null when it can.</summary>
    public static async Task<string?> ProblemAsync(BCKashDbContext db, LoanClientType type, int? clientId, int? groupId, CancellationToken cancellationToken = default)
    {
        var faceRequired = await FaceCapturePolicy.IsRequiredAsync(db, cancellationToken);
        if (type == LoanClientType.Group)
        {
            var group = await db.Groups.Where(g => g.Id == groupId).Select(g => new { g.Status }).FirstOrDefaultAsync(cancellationToken);
            if (group is null || group.Status != GroupStatus.Active)
            {
                return "Only an approved group can apply for a loan.";
            }

            var members = await db.GroupClients
                .Where(gc => gc.GroupId == groupId && gc.RemovedAt == null && gc.Client!.DeletedAt == null)
                .Select(gc => gc.Client!)
                .ToListAsync(cancellationToken);
            var notApproved = members.Where(c => c.Status != ClientStatus.Active).Select(Name).ToList();
            if (notApproved.Count > 0)
            {
                return $"Every member must be approved before the group can take a loan. Not approved yet: {string.Join(", ", notApproved)}.";
            }

            var noFace = faceRequired ? members.Where(c => c.BiometricEnrolledAt is null).Select(Name).ToList() : [];
            return noFace.Count > 0
                ? $"Every member's face must be captured before the group can take a loan. Not captured yet: {string.Join(", ", noFace)}."
                : null;
        }

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.DeletedAt == null, cancellationToken);
        return ClientProblem(client, faceRequired);
    }

    /// <summary>Why a loan can't be raised for <paramref name="client"/>; null when it can.</summary>
    public static string? ClientProblem(Client? client, bool faceRequired) => client switch
    {
        null => "Only an approved client can apply for a loan.",
        { Status: ClientStatus.Pending } => $"{Name(client)} is pending approval. A loan can only be raised once a controller approves them.",
        { Status: not ClientStatus.Active } => "Only an approved client can apply for a loan.",
        { BiometricEnrolledAt: null } when faceRequired => $"{Name(client)}'s face hasn't been captured. Capture it under Biometrics on the client's page before raising a loan.",
        _ => null,
    };

    private static string Name(Client c) =>
        !string.IsNullOrWhiteSpace(c.DisplayName)
            ? c.DisplayName
            : string.Join(' ', new[] { c.FirstName, c.LastName }.Where(p => !string.IsNullOrWhiteSpace(p))) is { Length: > 0 } name ? name : $"Client {c.Id}";
}
