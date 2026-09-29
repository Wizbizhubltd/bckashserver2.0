using System.Text.Json;
using BCKash.Application.Clients;
using BCKash.Application.Groups;
using BCKash.Application.Identity;
using BCKash.Domain.Clients;
using BCKash.Domain.Groups;
using BCKash.Domain.Identity;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

/// <summary>See <see cref="IClientOnboardingService"/>.</summary>
public class ClientOnboardingService : IClientOnboardingService
{
    private readonly BCKashDbContext _db;
    private readonly IBvnVerificationProvider _bvnProvider;
    private readonly IClientService _clientService;
    private readonly IGroupService _groupService;
    private readonly IOfficeScope _scope;
    private readonly ICurrentUserContext _currentUser;

    public ClientOnboardingService(
        BCKashDbContext db,
        IBvnVerificationProvider bvnProvider,
        IClientService clientService,
        IGroupService groupService,
        IOfficeScope scope,
        ICurrentUserContext currentUser)
    {
        _db = db;
        _bvnProvider = bvnProvider;
        _clientService = clientService;
        _groupService = groupService;
        _scope = scope;
        _currentUser = currentUser;
    }

    public async Task<BvnCheckResult> CheckBvnAsync(string bvn, string fullName, string? phone, CancellationToken cancellationToken = default)
    {
        bvn = bvn?.Trim() ?? string.Empty;
        if (!ClientOnboardingRules.IsValidBvn(bvn))
        {
            return new BvnCheckResult(BvnCheckOutcome.InvalidBvn, Error: "A BVN is 11 digits.");
        }

        var (firstName, middleName, lastName) = ClientOnboardingRules.SplitFullName(fullName ?? string.Empty);
        if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
        {
            return new BvnCheckResult(BvnCheckOutcome.NameRequired, Error: "Enter the client's first and last name.");
        }

        if (await BvnInUseAsync(bvn, cancellationToken))
        {
            return new BvnCheckResult(BvnCheckOutcome.AlreadyRegistered, Error: "A client with this BVN is already on the platform.");
        }

        var lookup = await _bvnProvider.LookupAsync(bvn, firstName, middleName, lastName, phone, cancellationToken);
        if (lookup.Outcome == BvnLookupOutcome.Unavailable)
        {
            return new BvnCheckResult(BvnCheckOutcome.ProviderUnavailable, Error: lookup.Error ?? "BVN verification is unavailable right now.");
        }

        var verification = new BvnVerification
        {
            Bvn = bvn,
            Found = lookup.Outcome == BvnLookupOutcome.Found,
            FirstName = lookup.FirstName,
            MiddleName = lookup.MiddleName,
            LastName = lookup.LastName,
            Phone = lookup.Phone,
            BirthDate = lookup.BirthDate,
            Gender = lookup.Gender,
            RequestedById = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _db.BvnVerifications.Add(verification);
        await _db.SaveChangesAsync(cancellationToken);

        if (!verification.Found)
        {
            return new BvnCheckResult(BvnCheckOutcome.NotFound, verification.Id, Error: "No one is registered with this BVN.");
        }

        var comparisons = Compare(verification, firstName, middleName, lastName, phone);
        return new BvnCheckResult(BvnCheckOutcome.Checked, verification.Id, comparisons.All(c => c.Matches), comparisons, lookup);
    }

    public async Task<OnboardingResult> OnboardClientAsync(int? officeId, OnboardClientInput client, CancellationToken cancellationToken = default)
    {
        var (resolvedOffice, failure) = await PrepareAsync(officeId, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var errors = new Dictionary<string, string>();
        var prepared = await PrepareMembersAsync([client], errors, cancellationToken);
        if (errors.Count > 0)
        {
            return new OnboardingResult(OnboardingOutcome.Invalid, Errors: errors);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var clients = await CreateClientsAsync(prepared, resolvedOffice, cancellationToken);
        if (clients is null)
        {
            return new OnboardingResult(OnboardingOutcome.AccountNumberGenerationFailed);
        }

        await transaction.CommitAsync(cancellationToken);
        return new OnboardingResult(OnboardingOutcome.Success, clients);
    }

    public async Task<OnboardingResult> OnboardGroupAsync(int? officeId, OnboardGroupInput group, IReadOnlyList<OnboardClientInput> members, CancellationToken cancellationToken = default)
    {
        var (resolvedOffice, failure) = await PrepareAsync(officeId, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(group?.Name))
        {
            errors["group"] = "The group needs a name.";
        }

        members ??= [];
        if (members.Count < ClientOnboardingRules.MinimumGroupSize)
        {
            errors["members"] = $"A group needs at least {ClientOnboardingRules.MinimumGroupSize} clients.";
        }

        var prepared = await PrepareMembersAsync(members, errors, cancellationToken);
        if (errors.Count > 0)
        {
            return new OnboardingResult(OnboardingOutcome.Invalid, Errors: errors);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var newGroup = new Group
        {
            Name = group!.Name.Trim(),
            OfficeId = resolvedOffice,
            Mobile = group.Phone,
            Email = group.Email,
            Address = group.Address,
            JoinedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = GroupStatus.Pending,
            CreatedById = _currentUser.UserId,
        };
        await _groupService.CreateAsync(newGroup, cancellationToken);

        var clients = await CreateClientsAsync(prepared, resolvedOffice, cancellationToken);
        if (clients is null)
        {
            return new OnboardingResult(OnboardingOutcome.AccountNumberGenerationFailed);
        }

        for (var i = 0; i < clients.Count; i++)
        {
            _db.GroupClients.Add(new GroupClient
            {
                GroupId = newGroup.Id,
                ClientId = clients[i].Id,
                Role = GroupMemberRoles.ForPosition(i),
                CreatedById = _currentUser.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OnboardingResult(OnboardingOutcome.Success, clients, newGroup);
    }

    /// <summary>Checks the caller may onboard, and settles which office the clients join.</summary>
    private async Task<(int? OfficeId, OnboardingResult? Failure)> PrepareAsync(int? officeId, CancellationToken cancellationToken)
    {
        if (!UserTypeSlugs.CanCreateClients(await _scope.GetUserTypeAsync(cancellationToken)))
        {
            return (null, new OnboardingResult(OnboardingOutcome.NotPermitted));
        }

        // Someone who works in one office onboards into it unless they say otherwise.
        var scopeOfficeIds = await _scope.GetOfficeIdsAsync(cancellationToken);
        if (!officeId.HasValue && scopeOfficeIds is { Count: 1 })
        {
            officeId = scopeOfficeIds.First();
        }

        if (!officeId.HasValue)
        {
            return (null, new OnboardingResult(OnboardingOutcome.Invalid, Errors: new Dictionary<string, string> { ["office"] = "Choose the office the clients join." }));
        }

        if (!await _scope.CanAccessOfficeAsync(officeId, cancellationToken))
        {
            return (null, new OnboardingResult(OnboardingOutcome.OfficeOutOfScope));
        }

        return (officeId, null);
    }

    private sealed record PreparedMember(Client Client, BvnVerification Verification, IReadOnlyList<BvnFieldComparison> Comparisons);

    private async Task<List<PreparedMember>> PrepareMembersAsync(IReadOnlyList<OnboardClientInput> members, Dictionary<string, string> errors, CancellationToken cancellationToken)
    {
        var prepared = new List<PreparedMember>();
        var verificationIds = members.Select(m => m.BvnVerificationId).ToList();
        var verifications = await _db.BvnVerifications.Where(v => verificationIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id, cancellationToken);
        var seenBvns = new HashSet<string>();

        for (var i = 0; i < members.Count; i++)
        {
            var key = $"members[{i}]";
            var member = members[i];
            var bvn = member.Bvn?.Trim() ?? string.Empty;
            var (firstName, middleName, lastName) = ClientOnboardingRules.SplitFullName(member.FullName ?? string.Empty);

            if (string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName))
            {
                errors[key] = "Enter the client's first and last name.";
                continue;
            }

            if (!ClientOnboardingRules.IsValidBvn(bvn))
            {
                errors[key] = "A BVN is 11 digits.";
                continue;
            }

            if (!seenBvns.Add(bvn))
            {
                errors[key] = "This BVN is entered for more than one client.";
                continue;
            }

            if (!verifications.TryGetValue(member.BvnVerificationId, out var verification)
                || verification.Bvn != bvn
                || verification.ClientId.HasValue
                || verification.CreatedAt < DateTime.UtcNow - ClientOnboardingRules.VerificationLifetime)
            {
                errors[key] = "Verify this client's BVN before onboarding them.";
                continue;
            }

            if (!verification.Found)
            {
                errors[key] = "No one is registered with this BVN.";
                continue;
            }

            if (await BvnInUseAsync(bvn, cancellationToken))
            {
                errors[key] = "A client with this BVN is already on the platform.";
                continue;
            }

            var comparisons = Compare(verification, firstName, middleName, lastName, member.Phone);
            var matches = comparisons.All(c => c.Matches);
            var source = matches ? ClientOnboardingRules.DetailsFromBvn : member.DetailsSource?.Trim().ToLowerInvariant();

            if (source is not (ClientOnboardingRules.DetailsFromBvn or ClientOnboardingRules.DetailsFromClient))
            {
                errors[key] = "The BVN details differ from what was entered — choose whose details to keep.";
                continue;
            }

            if (source == ClientOnboardingRules.DetailsFromClient && string.IsNullOrWhiteSpace(member.OverrideReason))
            {
                errors[key] = "Give a reason for keeping the client's details over the BVN's.";
                continue;
            }

            var useBvn = source == ClientOnboardingRules.DetailsFromBvn;
            var client = new Client
            {
                FirstName = useBvn ? verification.FirstName ?? firstName : firstName,
                MiddleName = useBvn ? verification.MiddleName ?? (matches ? middleName : null) : middleName,
                LastName = useBvn ? verification.LastName ?? lastName : lastName,
                Mobile = useBvn && !string.IsNullOrWhiteSpace(verification.Phone) ? verification.Phone : member.Phone,
                Email = string.IsNullOrWhiteSpace(member.Email) ? null : member.Email.Trim(),
                Bvn = bvn,
                Status = ClientStatus.Pending,
                ClientType = ClientType.Individual,
                JoinedDate = DateOnly.FromDateTime(DateTime.UtcNow),
                BvnVerifiedAt = verification.CreatedAt,
                BvnDetailsSource = source,
                IsHighRisk = !useBvn,
                HighRiskReason = useBvn ? null : member.OverrideReason!.Trim(),
                HighRiskFlaggedById = useBvn ? null : _currentUser.UserId,
                HighRiskFlaggedAt = useBvn ? null : DateTime.UtcNow,
                CreatedById = _currentUser.UserId,
            };
            client.DisplayName = string.Join(' ', new[] { client.FirstName, client.MiddleName, client.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
            client.FullName = client.DisplayName;
            prepared.Add(new PreparedMember(client, verification, comparisons));
        }

        return prepared;
    }

    /// <summary>Null when an account number couldn't be generated (the caller rolls back).</summary>
    private async Task<List<Client>?> CreateClientsAsync(List<PreparedMember> prepared, int? officeId, CancellationToken cancellationToken)
    {
        var created = new List<Client>();
        foreach (var member in prepared)
        {
            member.Client.OfficeId = officeId;
            var result = await _clientService.CreateAsync(member.Client, cancellationToken);
            if (result.Outcome != ClientWriteOutcome.Success)
            {
                return null;
            }

            member.Verification.ClientId = member.Client.Id;
            member.Verification.UpdatedAt = DateTime.UtcNow;
            _db.AuditTrail.Add(new AuditTrailEntry
            {
                UserId = _currentUser.UserId,
                OfficeId = officeId,
                Module = "Client",
                Action = member.Client.IsHighRisk ? "Onboarded (high risk)" : "Onboarded",
                Name = "Client onboarding",
                EntityId = member.Client.Id,
                Notes = JsonSerializer.Serialize(new
                {
                    bvnDetailsSource = member.Client.BvnDetailsSource,
                    mismatches = member.Comparisons.Where(c => !c.Matches).Select(c => new { c.Field, c.Given, c.FromBvn }),
                    highRiskReason = member.Client.HighRiskReason,
                }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            created.Add(member.Client);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return created;
    }

    private Task<bool> BvnInUseAsync(string bvn, CancellationToken cancellationToken) =>
        _db.Clients.AnyAsync(c => c.Bvn == bvn && c.DeletedAt == null, cancellationToken);

    private static List<BvnFieldComparison> Compare(BvnVerification fromBvn, string firstName, string? middleName, string? lastName, string? phone)
    {
        var comparisons = new List<BvnFieldComparison>
        {
            new("First name", firstName, fromBvn.FirstName, ClientOnboardingRules.NamesMatch(firstName, fromBvn.FirstName)),
            new("Last name", lastName, fromBvn.LastName, ClientOnboardingRules.NamesMatch(lastName, fromBvn.LastName)),
        };

        // Only compared when both sides have one — many BVN records carry no middle name.
        if (!string.IsNullOrWhiteSpace(middleName) && !string.IsNullOrWhiteSpace(fromBvn.MiddleName))
        {
            comparisons.Add(new("Middle name", middleName, fromBvn.MiddleName, ClientOnboardingRules.NamesMatch(middleName, fromBvn.MiddleName)));
        }

        comparisons.Add(new("Phone", phone, fromBvn.Phone, ClientOnboardingRules.PhonesMatch(phone, fromBvn.Phone)));
        return comparisons;
    }
}
