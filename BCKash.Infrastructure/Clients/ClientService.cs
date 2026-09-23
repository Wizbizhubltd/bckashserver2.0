using BCKash.Application.Clients;
using BCKash.Domain.Clients;
using BCKash.Infrastructure.Data;
using BCKash.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Clients;

public class ClientService : IClientService
{
    // Read-generate-insert isn't transactionally safe against a concurrent create picking
    // the same sequence number — the unique index on AccountNo (ClientConfiguration) is the
    // real safety net; this just bounds how many times we retry the whole cycle before giving up.
    private const int MaxAccountNumberRetries = 5;

    private readonly BCKashDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public ClientService(BCKashDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ClientWriteResult> CreateAsync(Client client, CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < MaxAccountNumberRetries; attempt++)
        {
            client.AccountNo = await NextAccountNumberAsync(cancellationToken);

            try
            {
                _db.Clients.Add(client);
                await _db.SaveChangesAsync(cancellationToken);
                return new ClientWriteResult(ClientWriteOutcome.Success, client);
            }
            catch (DbUpdateException)
            {
                _db.Entry(client).State = EntityState.Detached;
            }
        }

        return new ClientWriteResult(ClientWriteOutcome.AccountNumberGenerationFailed);
    }

    private async Task<string> NextAccountNumberAsync(CancellationToken cancellationToken)
    {
        var accountNumbers = await _db.Clients
            .Select(c => c.AccountNo)
            .ToListAsync(cancellationToken);

        long maxSequence = 0;
        foreach (var accountNo in accountNumbers)
        {
            if (ClientAccountNumberFormat.TryParseSequence(accountNo, out var sequence) && sequence > maxSequence)
            {
                maxSequence = sequence;
            }
        }

        return ClientAccountNumberFormat.Format(maxSequence + 1);
    }

    public async Task<ClientWriteResult> UpdateAsync(int id, Client updated, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        client.Bvn = updated.Bvn;
        client.CountryId = updated.CountryId;
        client.OfficeId = updated.OfficeId;
        client.StaffId = updated.StaffId;
        client.ReferredById = updated.ReferredById;
        client.ExternalId = updated.ExternalId;
        client.Title = updated.Title;
        client.FirstName = updated.FirstName;
        client.MiddleName = updated.MiddleName;
        client.LastName = updated.LastName;
        client.FullName = updated.FullName;
        client.IncorporationNumber = updated.IncorporationNumber;
        client.DisplayName = updated.DisplayName;
        client.Picture = updated.Picture;
        client.Mobile = updated.Mobile;
        client.Phone = updated.Phone;
        client.Email = updated.Email;
        client.Gender = updated.Gender;
        client.ClientType = updated.ClientType;
        client.MaritalStatus = updated.MaritalStatus;
        client.Dob = updated.Dob;
        client.Street = updated.Street;
        client.Ward = updated.Ward;
        client.District = updated.District;
        client.Region = updated.Region;
        client.Address = updated.Address;
        client.JoinedDate = updated.JoinedDate;
        client.Occupation = updated.Occupation;
        client.PostalCode = updated.PostalCode;
        client.Country = updated.Country;
        client.State = updated.State;
        client.City = updated.City;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> ActivateAsync(int id, DateOnly? activatedDate, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanActivate(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Active;
        client.ActivatedDate = activatedDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        client.ActivatedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> DeactivateAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ClientWriteResult(ClientWriteOutcome.ReasonRequired);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanDeactivate(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Inactive;
        client.InactiveDate = DateOnly.FromDateTime(DateTime.UtcNow);
        client.InactiveById = _currentUser.UserId;
        client.InactiveReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> ReactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanReactivate(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Active;
        client.ReactivatedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        client.ReactivatedById = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> DeclineAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ClientWriteResult(ClientWriteOutcome.ReasonRequired);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanDecline(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Declined;
        client.DeclinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        client.DeclinedById = _currentUser.UserId;
        client.DeclinedReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }

    public async Task<ClientWriteResult> CloseAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return new ClientWriteResult(ClientWriteOutcome.ReasonRequired);
        }

        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (client is null)
        {
            return new ClientWriteResult(ClientWriteOutcome.NotFound);
        }

        if (!ClientStatusTransitionRules.CanClose(client.Status))
        {
            return new ClientWriteResult(ClientWriteOutcome.InvalidTransition);
        }

        client.Status = ClientStatus.Closed;
        client.ClosedDate = DateOnly.FromDateTime(DateTime.UtcNow);
        client.ClosedById = _currentUser.UserId;
        client.ClosedReason = reason;

        await _db.SaveChangesAsync(cancellationToken);
        return new ClientWriteResult(ClientWriteOutcome.Success, client);
    }
}
