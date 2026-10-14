namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Skjematype og tiltakstyper for en tilknyttet arkivreferanse.</summary>
public sealed record TilknyttetSkjema(
    string Arkivreferanse,
    string? Skjematype,
    List<string> Tiltakstyper);
