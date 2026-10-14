using Ftpb.Ftbid.Domain.Referansedata;

namespace Ftpb.Ftbid.Domain.Referansedata;

/// <summary>Kommuneregisteret som brukes til å kontrollere gyldighet ved oppretting.</summary>
public sealed class Kommune
{
    public string Kommunenummer { get; set; } = "";
    public DateTimeOffset? GyldigFra { get; set; }
    public DateTimeOffset? GyldigTil { get; set; }
    public string? NyttKommunenummer { get; set; }
}
