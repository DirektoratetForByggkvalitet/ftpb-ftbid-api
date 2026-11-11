namespace Ftpb.Ftbid.Application.Kontrakter;

/// <summary>Eiendom med tilhørende ftbid for listing på tvers av prosjekter.</summary>
public sealed record EiendomListeResultat(string Ftbid, EiendomResultat Eiendom);
