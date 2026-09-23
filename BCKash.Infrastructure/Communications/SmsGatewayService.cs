using BCKash.Application.Communications;
using BCKash.Domain.Communications;
using BCKash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BCKash.Infrastructure.Communications;

public class SmsGatewayService : ISmsGatewayService
{
    private readonly BCKashDbContext _db;

    public SmsGatewayService(BCKashDbContext db)
    {
        _db = db;
    }

    public async Task<SmsGatewayWriteResult> CreateAsync(SmsGateway gateway, CancellationToken cancellationToken = default)
    {
        _db.SmsGateways.Add(gateway);
        await _db.SaveChangesAsync(cancellationToken);
        return new SmsGatewayWriteResult(SmsGatewayWriteOutcome.Success, gateway);
    }

    public async Task<SmsGatewayWriteResult> UpdateAsync(int id, SmsGateway updated, CancellationToken cancellationToken = default)
    {
        var gateway = await _db.SmsGateways.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (gateway is null)
        {
            return new SmsGatewayWriteResult(SmsGatewayWriteOutcome.NotFound);
        }

        gateway.Name = updated.Name;
        gateway.FromName = updated.FromName;
        gateway.ToName = updated.ToName;
        gateway.Url = updated.Url;
        gateway.MsgName = updated.MsgName;
        gateway.Notes = updated.Notes;

        await _db.SaveChangesAsync(cancellationToken);
        return new SmsGatewayWriteResult(SmsGatewayWriteOutcome.Success, gateway);
    }

    public async Task<SmsGatewayWriteResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var gateway = await _db.SmsGateways.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (gateway is null)
        {
            return new SmsGatewayWriteResult(SmsGatewayWriteOutcome.NotFound);
        }

        _db.SmsGateways.Remove(gateway);
        await _db.SaveChangesAsync(cancellationToken);
        return new SmsGatewayWriteResult(SmsGatewayWriteOutcome.Success);
    }
}
