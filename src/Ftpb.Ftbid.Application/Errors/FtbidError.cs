using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Application.Errors;

/// <summary>Strukturert regelbrudd i ProblemDetails, etter samme modell som NIREG.</summary>
public sealed record FtbidError(string Kode, string Beskrivelse, FeilType Type)
{
    /// <summary>Oppretter et regelbrudd fra et forventet ftbid-unntak.</summary>
    public static FtbidError FromException(FtbidException error) => new(error.Kode.ToString(), error.Message, error.FeilType);
}
