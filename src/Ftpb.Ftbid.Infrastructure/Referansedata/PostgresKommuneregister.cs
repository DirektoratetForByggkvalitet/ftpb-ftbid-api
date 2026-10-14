using Microsoft.EntityFrameworkCore;
using Ftpb.Ftbid.Infrastructure.Persistens;
using Ftpb.Ftbid.Application.Lagring;

namespace Ftpb.Ftbid.Infrastructure.Referansedata;

/// <summary>Leser kommuners gyldighet fra PostgreSQL.</summary>
public sealed class PostgresKommuneregister(FtbidDbContext database) : IKommuneregister
{
    /// <inheritdoc />
    public Task<bool> ErKommuneGyldigAsync(string kommunenummer, DateTimeOffset tidspunkt, CancellationToken avbrudd) =>
        database.Kommuner.AnyAsync(k => k.Kommunenummer == kommunenummer &&
            (k.GyldigFra == null || k.GyldigFra <= tidspunkt) && (k.GyldigTil == null || k.GyldigTil >= tidspunkt), avbrudd);
}
