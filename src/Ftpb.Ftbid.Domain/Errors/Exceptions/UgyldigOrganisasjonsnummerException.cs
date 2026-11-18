using Ftpb.Ftbid.Domain.Errors;

namespace Ftpb.Ftbid.Domain.Errors.Exceptions;

/// <summary>Ansvarlig søkers organisasjonsnummer må bestå av ni sifre.</summary>
public sealed class UgyldigOrganisasjonsnummerException() : ValidationException("Ansvarlig søkers organisasjonsnummer må bestå av ni sifre.", Feilkode.V0070);
