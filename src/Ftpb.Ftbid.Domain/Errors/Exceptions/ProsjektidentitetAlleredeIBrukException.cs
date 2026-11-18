using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Eiendommen gir samme prosjektidentitet som et annet prosjekt.</summary>
public sealed class ProsjektidentitetAlleredeIBrukException(string ftbid, Exception? innerException = null)
    : ValidationException($"Eiendommen kan ikke legges til eller endres fordi prosjektidentiteten allerede er i bruk. Ftbid: {ftbid}.", Feilkode.V0062, innerException)
{
    public string Ftbid { get; } = ftbid;
}
