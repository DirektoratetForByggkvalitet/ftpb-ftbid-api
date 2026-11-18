using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Identifikatoren finnes ikke ved tilknytning eller endring.</summary>
public sealed class FtbidIkkeFunnetException(string ftbid, Exception? innerException = null)
    : ValidationException($"Ftbid {ftbid} eksisterer ikke.", Feilkode.V0054, innerException)
{
    public string Ftbid { get; } = ftbid;
}
