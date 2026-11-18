namespace Ftpb.Ftbid.Application.Contracts;

/// <summary>Eiendom med tilhørende ftbid for listing på tvers av prosjekter.</summary>
public sealed record EiendomListResult(string Ftbid, EiendomResult Eiendom);
