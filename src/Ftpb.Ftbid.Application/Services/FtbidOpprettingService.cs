using Ftpb.Ftbid.Application.Validation;
using Ftpb.Ftbid.Application.Configuration;
using Ftpb.Ftbid.Application.Contracts;
using Ftpb.Ftbid.Application.Repositories;
using Ftpb.Ftbid.Domain.Prosjekter;
using static Ftpb.Ftbid.Application.Validation.FtbidValidation;
using Ftpb.Ftbid.Domain.Errors.Exceptions;
using FtbidEntity = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Application.Services;

/// <summary>Validerer prosjektdata og oppretter en ftbid etter duplikatkontroll.</summary>
public sealed class FtbidOpprettingService(IFtbidRepository repository, IKommuneregister kommuneregister, FtbidOptions settings) : IFtbidOpprettingService
{
    /// <inheritdoc />
    public async Task<string> CreateAsync(OpprettFtbid request, CancellationToken cancellationToken = default)
    {
        var ansvarligSoker = NormalizeAnsvarligSoker(request.AnsvarligSokerOrganisasjonsnummer);
        var name = RequireText(request.Prosjektnavn, "Prosjektnavn", 500);
        var system = RequireText(request.System, "System", 200);
        if (request.Eiendom is null || request.Eiendom.Count == 0) throw new RequiredValueMissingException("Eiendom");
        if (request.Eiendom.Count > 100) throw new TooManyValuesException("Eiendom", 100);
        var eiendommer = new List<Eiendom>();
        foreach (var data in request.Eiendom)
        {
            var eiendom = await EiendomValidation.ToEntityAsync(data, kommuneregister, cancellationToken);
            eiendom.Rekkefolge = eiendommer.Count;
            eiendommer.Add(eiendom);
        }
        if (request.Tiltakstyper is null || request.Tiltakstyper.Count == 0)
            throw new RequiredValueMissingException("Tiltakstyper");
        var tiltakstyper = request.Tiltakstyper.Select(t => RequireText(t, "Tiltakstyper", 200))
            .DistinctBy(TiltakstypeSett.NormalizeText, StringComparer.Ordinal).ToList();
        var prosjekt = new FtbidEntity
        {
            Prosjektnavn = name, NormalisertProsjektnavn = name.ToLowerInvariant(), System = system,
            Prosjektnummer = RequireText(request.Prosjektnr, "Prosjektnummer", 200),
            EksternProsjektreferanse = OptionalText(request.EksternProsjektreferanse, "Ekstern prosjektreferanse", 200),
            KommunaltSaksnummer = OptionalText(request.KommunaltSaksnummer, "Kommunalt saksnummer", 200),
            Tiltakshaver = OptionalText(request.Tiltakshaver, "Tiltakshaver", 500),
            Organisasjonsnummer = OptionalText(request.Organisasjonsnummer, "Organisasjonsnummer", 9),
            AnsvarligSokerOrganisasjonsnummer = ansvarligSoker,
            Tiltakstyper = tiltakstyper, TiltakstyperHash = TiltakstypeSett.ComputeHash(tiltakstyper),
            Utstedt = DateTimeOffset.UtcNow, Eiendom = eiendommer
        };
        return await repository.CreateAsync(prosjekt, settings.Gs1Prefiks, cancellationToken);
    }

    private static string? OptionalText(string? value, string field, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : RequireText(value, field, maxLength);
}
