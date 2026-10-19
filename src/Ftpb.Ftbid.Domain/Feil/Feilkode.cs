namespace Ftpb.Ftbid.Domain.Feil;

/// <summary>Stabile regelkoder. Delte regler bruker samme kode som i NIREG.</summary>
public enum Feilkode
{
    /// <summary>Gyldighetsperioden er i feil rekkefølge.</summary>
    V0026,
    /// <summary>En referansedatafil mangler eller inneholder ingen data.</summary>
    V003,
    /// <summary>Kommunenummeret har ugyldig format eller er ikke gyldig i kommuneregisteret.</summary>
    V0034,
    /// <summary>Inndata kan ikke deserialiseres til forventet modell.</summary>
    V0038,
    /// <summary>En påkrevd verdi mangler.</summary>
    V0042,
    /// <summary>En tekstverdi overskrider tillatt lengde.</summary>
    V0047,
    /// <summary>En liste inneholder flere verdier enn tillatt.</summary>
    V0048,
    /// <summary>En matrikkelenhet mangler eller inneholder ugyldige nummer.</summary>
    V0049,
    /// <summary>Ingen søkeparameter er oppgitt.</summary>
    V0050,
    /// <summary>Et matrikkelnummer brukt i søk er negativt.</summary>
    V0051,
    /// <summary>Identifikatoren har ugyldig format.</summary>
    V0052,
    /// <summary>Arkivreferansen finnes ikke i skjemametadata.</summary>
    V0053,
    /// <summary>Identifikatoren finnes ikke ved tilknytning eller endring.</summary>
    V0054,
    /// <summary>Importfilens referansedata har ugyldig struktur.</summary>
    V0055,
    /// <summary>Importerte skjemametadata mangler verdier eller bryter begrensningene.</summary>
    V0056,
    /// <summary>GS1-prefikset i servicens konfigurasjon er ugyldig.</summary>
    V0057,
    /// <summary>Sekvensnummeret kan ikke representeres i ftbid-formatet.</summary>
    V0058,
    /// <summary>En nødvendig konfigurasjonsverdi mangler.</summary>
    V0059,
    /// <summary>Maskinportens utsteder eller scopes er ugyldige.</summary>
    V0060,

    /// <summary>Eiendommens identifikasjon og prosjektnavn kolliderer med et annet prosjekt.</summary>
    V0062 = 20, // V0061 er utgått.
    /// <summary>Prosjektet er en dublett av en eksisterende ftbid.</summary>
    V0067 = 25, // V0063–V0066 er utgått; behold eksisterende enumverdier.
    /// <summary>Utgått kode for sannsynlig dublett.</summary>
    V0068,
    /// <summary>Ansvarlig søkers organisasjonsnummer har ugyldig format.</summary>
    V0070 = 28, // V0069 er utgått; behold eksisterende enumverdi.
    /// <summary>Eiendommen finnes ikke på denne ftbid-en.</summary>
    V0071,
    /// <summary>Ingen ftbider samsvarer med søkefiltrene.</summary>
    V0072,
}
