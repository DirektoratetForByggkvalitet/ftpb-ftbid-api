namespace Ftpb.Ftbid.Application.Repositories;

/// <summary>Oppslag i kommuneregisterets gyldighetsperioder.</summary>
public interface IKommuneregister
{
    /// <summary>Kontrollerer at matrikkelenhetens kommune er gyldig på angitt tidspunkt.</summary>
    Task<bool> ErKommuneGyldigAsync(string kommunenummer, DateTimeOffset tidspunkt, CancellationToken cancellationToken);
}
