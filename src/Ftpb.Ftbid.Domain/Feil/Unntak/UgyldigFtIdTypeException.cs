using Ftpb.Ftbid.Domain.Feil;

namespace Ftpb.Ftbid.Domain.Feil.Unntak;

/// <summary>FtIdType må være en støttet identifikatortype.</summary>
public sealed class UgyldigFtIdTypeException() : ValideringsException("FtIdType må være en støttet identifikatortype.", Feilkode.V0069);
