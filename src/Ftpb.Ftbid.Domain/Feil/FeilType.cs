namespace Ftpb.Ftbid.Domain.Feil;

/// <summary>Alvorlighetsgrad etter samme modell som NIREG.</summary>
public enum FeilType
{
    /// <summary>En feil som hindrer operasjonen.</summary>
    Feil = 0,
    /// <summary>En advarsel som beskriver en uønsket tilstand.</summary>
    Advarsel = 1
}
