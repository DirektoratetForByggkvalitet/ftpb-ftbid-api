namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Typebeskrivelse og tiltakstyper for en tilknyttet referanse.</summary>
public sealed record Tilknytning(
    string ReferanseId,
    string? Typebeskrivelse,
    List<string> Tiltakstyper);
