using BCKash.Api.Contracts;
using BCKash.Application.Clients;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController : ControllerBase
{
    private const string ManagePolicy = "Permission:clients.manage";
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly BCKashDbContext _db;
    private readonly IClientService _clientService;

    public ClientsController(BCKashDbContext db, IClientService clientService)
    {
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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.Clients.AsQueryable();

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
            query = query.Where(c => c.Mobile != null && c.Mobile.StartsWith(mobile));
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

        var totalCount = await query.CountAsync(cancellationToken);

        var clients = await query
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = clients.Select(ToListItemResponse).ToList();
        return Ok(new PagedResult<ClientListItemResponse>(items, page, pageSize, totalCount));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClientResponse>> Get(int id, CancellationToken cancellationToken)
    {
        var client = await _db.Clients.FindAsync([id], cancellationToken);
        return client is null ? NotFound() : Ok(ToResponse(client));
    }

    [HttpPost]
    [Authorize(Policy = ManagePolicy)]
    public async Task<ActionResult<ClientResponse>> Create(CreateClientRequest request, CancellationToken cancellationToken)
    {
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
    public async Task<IActionResult> Update(int id, UpdateClientRequest request, CancellationToken cancellationToken)
    {
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
        };

        var result = await _clientService.UpdateAsync(id, updated, cancellationToken);
        return result.Outcome == ClientWriteOutcome.NotFound ? NotFound() : Ok(ToResponse(result.Client!));
    }

    [HttpPost("{id:int}/activate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Activate(int id, ActivateClientRequest? request, CancellationToken cancellationToken)
    {
        var result = await _clientService.ActivateAsync(id, request?.ActivatedDate, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Deactivate(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _clientService.DeactivateAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/reactivate")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Reactivate(int id, CancellationToken cancellationToken)
    {
        var result = await _clientService.ReactivateAsync(id, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/decline")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Decline(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _clientService.DeclineAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    [HttpPost("{id:int}/close")]
    [Authorize(Policy = ManagePolicy)]
    public async Task<IActionResult> Close(int id, ReasonRequest request, CancellationToken cancellationToken)
    {
        var result = await _clientService.CloseAsync(id, request.Reason, cancellationToken);
        return ToTransitionResult(result);
    }

    private IActionResult ToTransitionResult(ClientWriteResult result) => result.Outcome switch
    {
        ClientWriteOutcome.Success => Ok(ToResponse(result.Client!)),
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
        c.Mobile, c.Bvn, c.OfficeId, c.StaffId, c.Status, c.ClientType, c.JoinedDate);

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
        c.Notes, c.Occupation, c.PostalCode, c.Country, c.State, c.City);
}
