using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>Ansvarlig søkers organisasjonsnummer må bestå av ni sifre.</summary>
public sealed class UgyldigOrganisasjonsnummerException() : ValideringsException("Ansvarlig søkers organisasjonsnummer må bestå av ni sifre.", Feilkode.V0070);
