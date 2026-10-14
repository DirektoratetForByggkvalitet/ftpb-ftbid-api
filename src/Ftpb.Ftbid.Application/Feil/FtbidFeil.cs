using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Application.Feil;

/// <summary>Strukturert regelbrudd i ProblemDetails, etter samme modell som NIREG.</summary>
public sealed record FtbidFeil(string Kode, string Beskrivelse, FeilType Type)
{
    /// <summary>Oppretter et regelbrudd fra et forventet ftbid-unntak.</summary>
    public static FtbidFeil FraUnntak(FtbidException feil) => new(feil.Kode.ToString(), feil.Message, feil.FeilType);
}
