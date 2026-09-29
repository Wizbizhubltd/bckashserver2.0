using BCKash.Api.Authorization;
using BCKash.Api.Contracts;
using BCKash.Application.Communications;
using BCKash.Api.Infrastructure;
using BCKash.Application.Identity;
using BCKash.Application.Clients;
using BCKash.Application.Files;
using BCKash.Domain.Groups;
using BCKash.Domain.Loans;
using BCKash.Domain.Clients;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/v1/clients")]
[Authorize]
public class ClientsController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IClientService _clientService;

    private readonly IOfficeScope _scope;
    private readonly IClientAccess _access;
    private readonly IFileStorageService _files;
    private readonly ICurrentUserContext _currentUser;

    public ClientsController(BCKashDbContext db, IClientService clientService, IOfficeScope scope, IClientAccess access, IFileStorageService files, ICurrentUserContext currentUser)
    {
        _scope = scope;
        _access = access;
        _files = files;
        _currentUser = currentUser;
        _db = db;
        _clientService = clientService;
    }

    /// <summary>
    /// FR-CLI-5's search/filter screen, paginated per NFR-3. <paramref name="search"/> matches
    /// FirstName/MiddleName/LastName/DisplayName — not FullName, a legacy denormalized
    /// concatenation that isn't kept authoritative by anything in this phase.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItemResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] string? accountNo,
        [FromQuery] string? bvn,
        [FromQuery] string? mobile,
        [FromQuery] int? officeId,
        [FromQuery] ClientStatus? status,
        [FromQuery] int? staffId,
        [FromQuery] bool? highRisk,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Clients.AsQueryable();
        query = await ScopedAsync(query, cancellationToken);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(c =>
                EF.Functions.Like(c.FirstName, pattern) ||
                EF.Functions.Like(c.MiddleName, pattern) ||
                EF.Functions.Like(c.LastName, pattern) ||
                EF.Functions.Like(c.DisplayName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(accountNo))
        {
            query = query.Where(c => c.AccountNo != null && c.AccountNo.StartsWith(accountNo));
        }

        if (!string.IsNullOrWhiteSpace(bvn))
        {
            query = query.Where(c => c.Bvn == bvn);
        }

        if (!string.IsNullOrWhiteSpace(mobile))
        {
            // Stored numbers are +234-formatted, so match that form of what was typed (0803… → +234803…),
            // plus the raw input for rows saved before normalization.
            var normalizedMobile = PhoneNumbers.ToNigerianInternational(mobile)!;
            query = query.Where(c => c.Mobile != null && (c.Mobile.StartsWith(normalizedMobile) || c.Mobile.StartsWith(mobile)));
        }

        if (officeId.HasValue)
        {
            query = query.Where(c => c.OfficeId == officeId);
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status);
        }

        if (staffId.HasValue)
        {
            query = query.Where(c => c.StaffId == staffId);
        }

        if (highRisk.HasValue)
        {
            query = query.Where(c => c.IsHighRisk == highRisk);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var clients = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var clientIds = clients.Select(c => c.Id).ToList();
        var groups = await _db.GroupClients
            .Where(gc => gc.ClientId != null && clientIds.Contains(gc.ClientId.Value) && gc.RemovedAt == null && gc.GroupId != null)
            .OrderByDescending(gc => gc.Id)
            .Select(gc => new { ClientId = gc.ClientId!.Value, GroupId = gc.GroupId!.Value, gc.Group!.Name })
            .ToListAsync(cancellationToken);
        var groupByClient = groups.GroupBy(g => g.ClientId).ToDictionary(g => g.Key, g => g.First());

        var items = clients
            .Select(c => groupByClient.TryGetValue(c.Id, out var g) ? ToListItemResponse(c) with { GroupId = g.GroupId, GroupName = g.Name } : ToListItemResponse(c))
            .ToList();
        return Ok(new PagedResult<ClientListItemResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientResponse>> Get(int id, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var client = await _db.Clients.FindAsync([id], cancellationToken);
        return client is null ? NotFound() : Ok(await ToDetailAsync(client, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
        var userType = await _scope.GetUserTypeAsync(cancellationToken);
        if (!UserTypeSlugs.CanCreateClients(userType))
        {
            return Problem(title: "Only managers and marketers can add clients.", statusCode: StatusCodes.Status403Forbidden);
        }

        // Staff onboard clients through the BVN-verified flow (POST /onboarding/clients or /groups);
        // this plain create stays only for legacy accounts with no user_type.
        if (userType is not null)
        {
            return Problem(title: "Onboard clients through the onboarding flow, which verifies their BVN.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (!await _scope.CanAccessOfficeAsync(request.OfficeId, cancellationToken))
        {
            return OfficeOutOfScope();
        }

        if (!string.IsNullOrWhiteSpace(request.Bvn) && await _access.BvnTakenAsync(request.Bvn, null, cancellationToken))
        {
            return BvnTaken();
        }

        var client = new Client
        {
            Bvn = request.Bvn,
            CountryId = request.CountryId,
            OfficeId = request.OfficeId,
            StaffId = request.StaffId,
            ReferredById = request.ReferredById,
            ExternalId = request.ExternalId,
            Title = request.Title,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            FullName = request.FullName,
            IncorporationNumber = request.IncorporationNumber,
            DisplayName = request.DisplayName,
            Picture = request.Picture,
            Mobile = request.Mobile,
            Phone = request.Phone,
            Email = request.Email,
            Gender = request.Gender,
            ClientType = request.ClientType,
            MaritalStatus = request.MaritalStatus,
            Dob = request.Dob,
            Street = request.Street,
            Ward = request.Ward,
            District = request.District,
            Region = request.Region,
            Address = request.Address,
            JoinedDate = request.JoinedDate,
            Occupation = request.Occupation,
            PostalCode = request.PostalCode,
            Country = request.Country,
            State = request.State,
            City = request.City,
            BusinessAddress = request.BusinessAddress,
            Nationality = request.Nationality,
        };

        var result = await _clientService.CreateAsync(client, cancellationToken);

        return result.Outcome switch
        {
            ClientWriteOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Client!.Id }, ToResponse(result.Client)),
            ClientWriteOutcome.AccountNumberGenerationFailed => Problem(
                title: "Could not generate a unique account number — please retry.",
                statusCode: StatusCodes.Status409Conflict),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Update(int id, UpdateClientRequest request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var existing = (await _db.Clients.FindAsync([id], cancellationToken))!;
        var wasPending = existing.Status == ClientStatus.Pending;
        if (await _access.DocumentationBlockAsync(existing, cancellationToken) is { } block)
        {
            return Problem(title: block, statusCode: StatusCodes.Status403Forbidden);
        }

        // No two clients share a BVN. Checked only when it changes, so records imported with a shared
        // BVN can still be edited — they just can't take on another client's.
        if (!string.IsNullOrWhiteSpace(request.Bvn) && request.Bvn.Trim() != existing.Bvn?.Trim()
            && await _access.BvnTakenAsync(request.Bvn, id, cancellationToken))
        {
            return BvnTaken();
        }

        if (!await _scope.CanAccessOfficeAsync(request.OfficeId, cancellationToken))
        {
            return OfficeOutOfScope();
        }

        var updated = new Client
        {
            Bvn = request.Bvn,
            CountryId = request.CountryId,
            OfficeId = request.OfficeId,
            StaffId = request.StaffId,
            ReferredById = request.ReferredById,
            ExternalId = request.ExternalId,
            Title = request.Title,
            FirstName = request.FirstName,
            MiddleName = request.MiddleName,
            LastName = request.LastName,
            FullName = request.FullName,
            IncorporationNumber = request.IncorporationNumber,
            DisplayName = request.DisplayName,
            Picture = request.Picture,
            Mobile = request.Mobile,
            Phone = request.Phone,
            Email = request.Email,
            Gender = request.Gender,
            ClientType = request.ClientType,
            MaritalStatus = request.MaritalStatus,
            Dob = request.Dob,
            Street = request.Street,
            Ward = request.Ward,
            District = request.District,
            Region = request.Region,
            Address = request.Address,
            JoinedDate = request.JoinedDate,
            Occupation = request.Occupation,
            PostalCode = request.PostalCode,
            Country = request.Country,
            State = request.State,
            City = request.City,
            BusinessAddress = request.BusinessAddress,
            Nationality = request.Nationality,
        };

        var result = await _clientService.UpdateAsync(id, updated, cancellationToken);
        if (result.Outcome == ClientWriteOutcome.NotFound)
        {
            return NotFound();
        }

        await _access.NoteEditedAsync(id, cancellationToken);
        if (!wasPending && result.Client!.Status == ClientStatus.Pending)
        {
            // Editing an approved client under an edit privilege sends them back for approval.
            await notifications.ClientPendingAsync(result.Client, afterEdit: true, cancellationToken);
        }

        return Ok(await ToDetailAsync(result.Client!, cancellationToken));
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Activate(int id, ActivateClientRequest? request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!UserTypeSlugs.CanApproveClients(await _scope.GetUserTypeAsync(cancellationToken)))
        {
            return Problem(title: "Only controllers can approve clients.", statusCode: StatusCodes.Status403Forbidden);
        }

        var target = await _db.Clients.FindAsync([id], cancellationToken);
        if (target is { IsHighRisk: true })
        {
            return Problem(title: "This client is flagged high risk. A super admin must mark them safe before they can be approved.", statusCode: StatusCodes.Status409Conflict);
        }

        // Guarantors, a reference and (while mandatory) a face capture come first. Users with no user_type keep their old behaviour (see IOfficeScope).
        if (target is not null && await _scope.GetUserTypeAsync(cancellationToken) is not null)
        {
            var blockers = await _access.ApprovalBlockersAsync(target, cancellationToken);
            if (blockers.Count > 0)
            {
                return Problem(title: $"Before this client can be approved, add {string.Join(" and ", blockers)}.", statusCode: StatusCodes.Status409Conflict);
            }
        }

        var result = await _clientService.ActivateAsync(id, request?.ActivatedDate, cancellationToken);
        if (result.Outcome == ClientWriteOutcome.Success)
        {
            await CloseEditPrivilegeAsync(result.Client!, cancellationToken);
            await ApproveCompletedGroupsAsync(id, cancellationToken);
            await notifications.ClientReviewedAsync(result.Client!, approved: true, cancellationToken);
        }

        return await ToTransitionResultAsync(result, cancellationToken);
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Deactivate(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _clientService.DeactivateAsync(id, request.Reason, cancellationToken);
        return await ToTransitionResultAsync(result, cancellationToken);
    }

    [HttpPost("{id:int}/reactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Reactivate(int id, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _clientService.ReactivateAsync(id, cancellationToken);
        return await ToTransitionResultAsync(result, cancellationToken);
    }

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        if (!UserTypeSlugs.CanApproveClients(await _scope.GetUserTypeAsync(cancellationToken)))
        {
            return Problem(title: "Only controllers can decline clients.", statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await _clientService.DeclineAsync(id, request.Reason, cancellationToken);
        if (result.Outcome == ClientWriteOutcome.Success)
        {
            await notifications.ClientReviewedAsync(result.Client!, approved: false, cancellationToken);
        }

        return await ToTransitionResultAsync(result, cancellationToken);
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Close(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        if (!await InScopeAsync(id, cancellationToken))
        {
            return NotFound();
        }

        var result = await _clientService.CloseAsync(id, request.Reason, cancellationToken);
        return await ToTransitionResultAsync(result, cancellationToken);
    }

    /// <summary>Super admins clear a high-risk flag once they're satisfied the client is genuine; the client can then be approved.</summary>
    [HttpPost("{id:int}/mark-safe")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public async Task<IActionResult> MarkSafe(int id, MarkSafeRequest? request, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        if (!client.IsHighRisk)
        {
            return Problem(title: "This client isn't flagged high risk.", statusCode: StatusCodes.Status400BadRequest);
        }

        client.IsHighRisk = false;
        client.HighRiskClearedById = _currentUser.UserId;
        client.HighRiskClearedAt = DateTime.UtcNow;
        client.HighRiskClearedNote = string.IsNullOrWhiteSpace(request?.Note) ? null : request.Note.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await ToDetailAsync(client, cancellationToken));
    }

    /// <summary>The client's profile picture — the face captured when their biometrics were enrolled (see ClientBiometricsController).</summary>
    [HttpGet("{id:int}/photo")]
    public async Task<IActionResult> GetPhoto(int id, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(id, cancellationToken);
        if (client?.Picture is null)
        {
            return NotFound();
        }

        var content = await _files.ReadAsync(client.Picture, client.Picture, cancellationToken);
        return content is null ? NotFound() : File(content.Content, content.ContentType);
    }

    /// <summary>
    /// Everything the client's printed membership/loan form needs besides the client record: the account
    /// officer, the loan they're applying for (their latest application, or their latest loan), their NIN,
    /// their group and its members, and the application form fee currently in force.
    /// </summary>
    [HttpGet("{id:int}/loan-form")]
    public async Task<ActionResult<ClientLoanFormResponse>> LoanForm(int id, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        var staffId = client.StaffId ?? client.CreatedById;
        var staffName = staffId.HasValue
            ? await _db.Users.Where(u => u.Id == staffId).Select(u => new { u.FirstName, u.LastName, u.Email }).FirstOrDefaultAsync(cancellationToken) is { } u
                ? (string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim())
                : null
            : null;

        var application = await _db.LoanApplications
            .Where(a => a.ClientId == id)
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => new { a.Amount, a.LoanTerm, a.LoanTermType, ProductName = a.LoanProduct!.Name })
            .FirstOrDefaultAsync(cancellationToken);
        var loan = application is null
            ? await _db.Loans
                .Where(l => l.ClientId == id)
                .OrderByDescending(l => l.CreatedAt)
                .ThenByDescending(l => l.Id)
                .Select(l => new { l.AppliedAmount, l.LoanTerm, l.LoanTermType, ProductName = l.LoanProduct!.Name })
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        var nin = await _db.Documents
            .Where(d => d.Type == ReferenceEntityType.Client && d.RecordId == id && d.Category == ClientDocumentRules.NinSlip)
            .OrderByDescending(d => d.Id)
            .Select(d => d.IdNumber)
            .FirstOrDefaultAsync(cancellationToken);

        var group = await _db.GroupClients
            .Where(gc => gc.ClientId == id && gc.RemovedAt == null)
            .OrderByDescending(gc => gc.CreatedAt)
            .Select(gc => new { Id = gc.GroupId!.Value, gc.Group!.Name })
            .FirstOrDefaultAsync(cancellationToken);
        var members = group is null
            ? []
            : (await _db.GroupClients
                .Where(gc => gc.GroupId == group.Id && gc.RemovedAt == null && gc.Client!.DeletedAt == null)
                .OrderByDescending(gc => gc.CreatedAt)
                .ThenByDescending(gc => gc.Id)
                .Select(gc => new { gc.Client!.Id, gc.Client.DisplayName, gc.Client.FirstName, gc.Client.LastName, gc.Role })
                .ToListAsync(cancellationToken))
                .Select(m => new ClientLoanFormGroupMember(
                    m.Id,
                    string.IsNullOrWhiteSpace(m.DisplayName) ? $"{m.FirstName} {m.LastName}".Trim() : m.DisplayName,
                    m.Role))
                .ToList();

        var formFee = await BCKash.Infrastructure.Loans.ApplicationFormFee.CurrentAsync(_db, cancellationToken);

        return Ok(new ClientLoanFormResponse(
            staffName,
            application?.Amount ?? loan?.AppliedAmount,
            application?.LoanTerm ?? loan?.LoanTerm,
            application?.LoanTermType ?? loan?.LoanTermType,
            application?.ProductName ?? loan?.ProductName,
            nin,
            formFee?.Amount,
            group?.Id,
            group?.Name,
            members));
    }

    /// <summary>The groups the client belongs to, with their role in each.</summary>
    [HttpGet("{id:int}/groups")]
    public async Task<ActionResult<IReadOnlyList<ClientGroupMembershipResponse>>> Groups(int id, CancellationToken cancellationToken)
    {
        if (await _access.FindVisibleAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        var memberships = await _db.GroupClients
            .Where(gc => gc.ClientId == id && gc.RemovedAt == null)
            .OrderByDescending(gc => gc.CreatedAt)
            .Select(gc => new ClientGroupMembershipResponse(gc.GroupId!.Value, gc.Group!.Name, gc.Group.AccountNo, gc.Group.Status, gc.Role, gc.CreatedAt))
            .ToListAsync(cancellationToken);
        return Ok(memberships);
    }

    /// <summary>Everything recorded against the client, newest first — onboarding, edits, approvals, flags and deletion requests.</summary>
    [HttpGet("{id:int}/audit")]
    public async Task<ActionResult<IReadOnlyList<ClientAuditEntryResponse>>> Audit(int id, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        var requestIds = await _db.DeletionRequests
            .Where(r => r.EntityType == DeletionRequest.ClientEntity && r.EntityId == id)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        var entries = await _db.AuditTrail
            .Where(a => (a.Module == nameof(Client) && a.EntityId == id) || (a.Module == nameof(DeletionRequest) && a.EntityId != null && requestIds.Contains(a.EntityId.Value)))
            .OrderByDescending(a => a.Id)
            .Take(200)
            .ToListAsync(cancellationToken);

        var userIds = entries.Where(e => e.UserId.HasValue).Select(e => e.UserId!.Value).Append(client.CreatedById ?? 0).Distinct().ToList();
        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim(), cancellationToken);

        var items = entries
            .Select(e => new ClientAuditEntryResponse(e.Id, e.CreatedAt, e.Action, e.Module, e.Notes, e.UserId, e.UserId.HasValue ? names.GetValueOrDefault(e.UserId.Value) : null))
            .ToList();

        // Creations aren't tied to a record id in the audit trail (the id doesn't exist until the insert), so the client row itself says when and by whom.
        if (!entries.Any(e => e.Action is not null && e.Action.StartsWith("Onboarded", StringComparison.Ordinal)))
        {
            items.Add(new ClientAuditEntryResponse(null, client.CreatedAt, "Created", nameof(Client), null, client.CreatedById,
                client.CreatedById.HasValue ? names.GetValueOrDefault(client.CreatedById.Value) : null));
        }

        return Ok(items);
    }

    /// <summary>Deletes a client who has never been approved. An approved client needs a deletion request instead.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Delete(int id, [FromServices] IDeletionService deletions, [FromServices] IStaffNotificationService notifications, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        if (!(await _access.ActionsForAsync(client, cancellationToken)).CanDelete && !ClientDeletionRules.WasApproved(client))
        {
            return Problem(title: "Only the staff member who onboarded this client, or a controller, can delete them.", statusCode: StatusCodes.Status403Forbidden);
        }

        var deleted = await deletions.DeleteClientAsync(id, cancellationToken);
        if (deleted.Outcome == DeletionOutcome.Success)
        {
            await notifications.ClientRemovedAsync(id, cancellationToken);
        }

        return DeletionResult(deleted, "client");
    }

    /// <summary>Asks a super admin to delete an approved client, giving the reason.</summary>
    [HttpPost("{id:int}/deletion-requests")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> RequestDeletion(int id, ReasonRequest request, [FromServices] IDeletionService deletions, CancellationToken cancellationToken)
    {
        var client = await _access.FindVisibleAsync(id, cancellationToken);
        if (client is null)
        {
            return NotFound();
        }

        if (!ClientDeletionRules.WasApproved(client))
        {
            return Problem(title: "This client hasn't been approved, so they can be deleted directly.", statusCode: StatusCodes.Status400BadRequest);
        }

        return DeletionResult(await deletions.RequestAsync(DeletionRequest.ClientEntity, id, request.Reason, cancellationToken), "client");
    }

    internal static IActionResult DeletionResultFor(ControllerBase controller, DeletionResult result, string noun) => result.Outcome switch
    {
        DeletionOutcome.Success when result.Request is not null => controller.StatusCode(StatusCodes.Status201Created, new { requestId = result.Request.Id }),
        DeletionOutcome.Success => controller.NoContent(),
        DeletionOutcome.NotFound => controller.NotFound(),
        DeletionOutcome.RequiresRequest => controller.Problem(
            title: $"An approved {noun} can't be deleted directly. Raise a deletion request with a reason for a super admin to review.",
            statusCode: StatusCodes.Status409Conflict),
        DeletionOutcome.HasOpenLoans => controller.Problem(title: $"This {noun} still has loans or loan applications, so it can't be deleted.", statusCode: StatusCodes.Status409Conflict),
        DeletionOutcome.ReasonRequired => controller.Problem(title: "Give a reason for the deletion.", statusCode: StatusCodes.Status400BadRequest),
        DeletionOutcome.AlreadyRequested => controller.Problem(title: $"A deletion request for this {noun} is already waiting for a super admin.", statusCode: StatusCodes.Status409Conflict),
        _ => controller.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private IActionResult DeletionResult(DeletionResult result, string noun) => DeletionResultFor(this, result, noun);

    private ObjectResult BvnTaken() =>
        Problem(title: "Another client already has this BVN. No two clients can share one.", statusCode: StatusCodes.Status409Conflict);

    /// <summary>Approving the client again after an edit closes the edit privilege and locks the profile once more.</summary>
    private async Task CloseEditPrivilegeAsync(Client client, CancellationToken cancellationToken)
    {
        if (client.EditPrivilegeRequestId is not { } requestId)
        {
            return;
        }

        var request = await _db.ClientEditRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);
        if (request is not null)
        {
            request.Status = ClientEditRequest.CompletedStatus;
            request.CompletedAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
        }

        client.EditPrivilegeRequestId = null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A group is approved once every one of its members is — checked whenever a member is approved.</summary>
    private async Task ApproveCompletedGroupsAsync(int clientId, CancellationToken cancellationToken)
    {
        var groupIds = await _db.GroupClients.Where(gc => gc.ClientId == clientId && gc.RemovedAt == null).Select(gc => gc.GroupId!.Value).ToListAsync(cancellationToken);
        foreach (var group in await _db.Groups.Where(g => groupIds.Contains(g.Id) && g.Status == GroupStatus.Pending).ToListAsync(cancellationToken))
        {
            var allApproved = !await _db.GroupClients
                .Where(gc => gc.GroupId == group.Id && gc.RemovedAt == null)
                .AnyAsync(gc => gc.Client!.Status != ClientStatus.Active, cancellationToken);
            if (allApproved)
            {
                group.Status = GroupStatus.Active;
                group.ActivatedDate = DateOnly.FromDateTime(DateTime.UtcNow);
                group.ActivatedById = _currentUser.UserId;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IActionResult> ToTransitionResultAsync(ClientWriteResult result, CancellationToken cancellationToken) => result.Outcome switch
    {
        ClientWriteOutcome.Success => Ok(await ToDetailAsync(result.Client!, cancellationToken)),
        ClientWriteOutcome.NotFound => NotFound(),
        ClientWriteOutcome.InvalidTransition => Problem(
            title: "This transition isn't valid from the client's current status.",
            statusCode: StatusCodes.Status400BadRequest),
        ClientWriteOutcome.ReasonRequired => Problem(
            title: "A reason is required for this transition.",
            statusCode: StatusCodes.Status400BadRequest),
        _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    /// <summary>
    /// `display_name`/`full_name` are legacy denormalized columns that, on real imported data,
    /// are routinely blank — never populated by whatever process wrote the original rows. Rather
    /// than have every consumer of this API re-derive a fallback (and risk the same bug the
    /// control portal hit, where a blank-but-non-empty stored value defeats a naive `||`
    /// fallback), the response's DisplayName is always a real name: the stored value if it's
    /// non-blank, otherwise FirstName + LastName computed here once.
    /// </summary>
    private static string? SafeDisplayName(Client c)
    {
        if (!string.IsNullOrWhiteSpace(c.DisplayName))
        {
            return c.DisplayName;
        }

        var computed = string.Join(" ", new[] { c.FirstName, c.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrWhiteSpace(computed) ? null : computed;
    }

    private static ClientListItemResponse ToListItemResponse(Client c) => new(
        c.Id, c.AccountNo, SafeDisplayName(c), c.FirstName, c.MiddleName, c.LastName,
        c.Mobile, c.Bvn, c.OfficeId, c.StaffId, c.Status, c.ClientType, c.JoinedDate, c.IsHighRisk, FaceEnrolled: c.BiometricEnrolledAt != null);

    private static ClientResponse ToResponse(Client c) => new(
        c.Id, c.LegacyClientId, c.Bvn, c.CountryId, c.OfficeId, c.StaffId, c.ReferredById,
        c.AccountNo, c.OldAccountNo, c.ExternalId, c.Title, c.FirstName,
        c.MiddleName, c.LastName, c.FullName, c.IncorporationNumber, SafeDisplayName(c),
        c.Picture, c.Mobile, c.Phone, c.Email, c.Gender, c.ClientType,
        c.Status, c.MaritalStatus, c.Dob, c.Street, c.Ward,
        c.District, c.Region, c.Address, c.JoinedDate,
        c.ActivatedDate, c.ActivatedById, c.ReactivatedDate, c.ReactivatedById,
        c.DeclinedDate, c.DeclinedById, c.DeclinedReason,
        c.ClosedDate, c.ClosedById, c.ClosedReason,
        c.InactiveDate, c.InactiveById, c.InactiveReason,
        c.Notes, c.Occupation, c.PostalCode, c.Country, c.State, c.City,
        c.CreatedById, c.CreatedAt,
        c.BvnVerifiedAt, c.BvnDetailsSource,
        c.IsHighRisk, c.HighRiskReason, c.HighRiskFlaggedAt, c.HighRiskClearedAt, c.HighRiskClearedNote,
        !string.IsNullOrWhiteSpace(c.Picture))
    {
        BusinessAddress = c.BusinessAddress,
        Nationality = c.Nationality,
        BiometricEnrolledAt = c.BiometricEnrolledAt,
    };

    /// <summary>The full record for the client page: who onboarded/approved them, their office, and what the viewer may do.</summary>
    private async Task<ClientResponse> ToDetailAsync(Client c, CancellationToken cancellationToken)
    {
        var userIds = new[] { c.CreatedById, c.ActivatedById, c.StaffId }.Where(i => i.HasValue).Select(i => i!.Value).ToList();
        var names = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToDictionaryAsync(u => u.Id, u => string.IsNullOrWhiteSpace($"{u.FirstName} {u.LastName}") ? u.Email : $"{u.FirstName} {u.LastName}".Trim(), cancellationToken);
        var officeName = c.OfficeId.HasValue ? await _db.Offices.Where(o => o.Id == c.OfficeId).Select(o => o.Name).FirstOrDefaultAsync(cancellationToken) : null;
        var actions = await _access.ActionsForAsync(c, cancellationToken);
        var pendingDeletion = await _db.DeletionRequests.AnyAsync(
            r => r.EntityType == DeletionRequest.ClientEntity && r.EntityId == c.Id && r.Status == DeletionRequestStatus.Pending, cancellationToken);

        return ToResponse(c) with
        {
            CreatedByName = c.CreatedById.HasValue ? names.GetValueOrDefault(c.CreatedById.Value) : null,
            ActivatedByName = c.ActivatedById.HasValue ? names.GetValueOrDefault(c.ActivatedById.Value) : null,
            OfficeName = officeName,
            Actions = new ClientActionsResponse(
                actions.CanEditDetails, actions.CanApprove, actions.CanDecline, actions.CanDelete, actions.CanRequestDeletion, actions.CanMarkSafe,
                actions.CanRequestEdit, actions.EditPrivilegeOpen, actions.CanReviewEditRequests),
            PendingDeletionRequest = pendingDeletion,
            ApprovalBlockers = await _access.ApprovalBlockersAsync(c, cancellationToken),
            CurrentEditRequest = await ClientEditRequestsController.CurrentForAsync(_db, c, cancellationToken),
            ActiveLoan = await _access.ActiveLoanAsync(c.Id, cancellationToken),
            StaffName = c.StaffId.HasValue ? names.GetValueOrDefault(c.StaffId.Value) : null,
            LoanBlocker = BCKash.Infrastructure.Loans.LoanApplicantRules.ClientProblem(c, await BCKash.Infrastructure.Clients.FaceCapturePolicy.IsRequiredAsync(_db, cancellationToken)),
        };
    }

    /// <summary>Only records in the caller's offices (see <see cref="IOfficeScope"/>); everything for a super admin.</summary>
    private async Task<IQueryable<Client>> ScopedAsync(IQueryable<Client> query, CancellationToken cancellationToken)
    {
        var officeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        query = query.Where(c => c.DeletedAt == null);
        return officeIds is null ? query : query.Where(c => c.OfficeId.HasValue && officeIds.Contains(c.OfficeId.Value));
    }

    /// <summary>False for a client outside the caller's offices — callers answer 404, so it stays invisible.</summary>
    private async Task<bool> InScopeAsync(int id, CancellationToken cancellationToken) =>
        await (await ScopedAsync(_db.Clients.Where(c => c.Id == id), cancellationToken)).AnyAsync(cancellationToken);

    private ObjectResult OfficeOutOfScope() =>
        Problem(title: "You can only work with records in your own office(s).", statusCode: StatusCodes.Status403Forbidden);
}
