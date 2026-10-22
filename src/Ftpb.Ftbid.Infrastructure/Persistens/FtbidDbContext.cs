using Ftpb.Ftbid.Domain.Prosjekter;
using Ftpb.Ftbid.Domain.Referansedata;
using Microsoft.EntityFrameworkCore;
using FtbidEntitet = Ftpb.Ftbid.Domain.Prosjekter.Ftbid;

namespace Ftpb.Ftbid.Infrastructure.Persistens;

/// <summary>PostgreSQL-kontekst for ftbid, identifikasjonsmetadata og referansedata.</summary>
public sealed class FtbidDbContext(DbContextOptions<FtbidDbContext> alternativer) : DbContext(alternativer)
{
    public DbSet<FtbidEntitet> Ftbider => Set<FtbidEntitet>();
    public DbSet<Identifikasjonsmetadata> Identifikasjonsmetadata => Set<Identifikasjonsmetadata>();
    public DbSet<Skjemametadata> Skjemametadata => Set<Skjemametadata>();
    public DbSet<Kommune> Kommuner => Set<Kommune>();

    /// <summary>Definerer tabellnavn, relasjoner, indekser og sekvens for PostgreSQL.</summary>
    protected override void OnModelCreating(ModelBuilder modell)
    {
        modell.HasSequence<long>("ftbid_sekvens").StartsAt(1).HasMax(9_999_999);
        var ftbid = modell.Entity<FtbidEntitet>();
        ftbid.ToTable("ftbid");
        ftbid.HasKey(f => f.Ean);
        ftbid.Property(f => f.Ean).HasMaxLength(18);
        ftbid.Property(f => f.Prosjektnavn).HasMaxLength(500);
        ftbid.Property(f => f.NormalisertProsjektnavn).HasMaxLength(500);
        ftbid.Property(f => f.Prosjektnummer).HasMaxLength(200);
        ftbid.Property(f => f.EksternProsjektreferanse).HasMaxLength(200);
        ftbid.Property(f => f.KommunaltSaksnummer).HasMaxLength(200);
        ftbid.Property(f => f.Tiltakshaver).HasMaxLength(500);
        ftbid.Property(f => f.Organisasjonsnummer).HasMaxLength(9);
        ftbid.Property(f => f.AnsvarligSokerOrganisasjonsnummer).HasMaxLength(9);
        ftbid.Property(f => f.Tiltakstyper).HasColumnType("text[]").HasDefaultValueSql("ARRAY[]::text[]");
        ftbid.Property(f => f.TiltakstyperHash).HasMaxLength(64);
        ftbid.HasIndex(f => f.TiltakstyperHash);
        ftbid.Property(f => f.System).HasMaxLength(200);
        ftbid.HasMany(f => f.Identifikasjonsmetadata).WithOne().HasForeignKey(m => m.Ftbid).OnDelete(DeleteBehavior.Restrict);
        ftbid.HasMany(f => f.Eiendom).WithOne().HasForeignKey(e => e.Ftbid).OnDelete(DeleteBehavior.Cascade);
        var eiendom = modell.Entity<Eiendom>();
        eiendom.ToTable("eiendom");
        eiendom.HasKey(e => e.Id);
        eiendom.HasIndex(e => new { e.Ftbid, e.Rekkefolge }).IsUnique();
        eiendom.Property(e => e.Bygningsnummer).HasMaxLength(50);
        eiendom.Property(e => e.Bolignummer).HasMaxLength(50);
        eiendom.Property(e => e.Kommunenavn).HasMaxLength(200);
        eiendom.OwnsOne(e => e.Eiendomsidentifikasjon, ident =>
        {
            ident.Property(i => i.Kommunenummer).HasMaxLength(4);
            ident.HasIndex(i => new { i.Kommunenummer, i.Gaardsnummer, i.Bruksnummer });
        });
        eiendom.Navigation(e => e.Eiendomsidentifikasjon).IsRequired();
        eiendom.HasMany(e => e.Adresse).WithOne().HasForeignKey(a => a.EiendomId).OnDelete(DeleteBehavior.Cascade);
        var adresse = modell.Entity<Eiendomsadresse>();
        adresse.ToTable("eiendomsadresse");
        adresse.HasKey(a => a.Id);
        adresse.HasIndex(a => new { a.EiendomId, a.Rekkefolge }).IsUnique();
        adresse.Property(a => a.Adresselinje1).HasMaxLength(1000);
        adresse.Property(a => a.Adresselinje2).HasMaxLength(1000);
        adresse.Property(a => a.Adresselinje3).HasMaxLength(1000);
        adresse.Property(a => a.Postnr).HasMaxLength(20);
        adresse.Property(a => a.Poststed).HasMaxLength(200);
        adresse.Property(a => a.Landkode).HasMaxLength(2);
        adresse.Property(a => a.Gatenavn).HasMaxLength(500);
        adresse.Property(a => a.Husnr).HasMaxLength(50);
        adresse.Property(a => a.Bokstav).HasMaxLength(20);
        var identifikasjon = modell.Entity<Identifikasjonsmetadata>();
        identifikasjon.ToTable("identifikasjonsmetadata");
        identifikasjon.HasKey(m => m.Arkivreferanse);
        identifikasjon.Property(m => m.Arkivreferanse).HasMaxLength(80);
        identifikasjon.Property(m => m.Ftbid).HasMaxLength(18);
        identifikasjon.HasOne(m => m.Skjemametadata).WithOne().HasForeignKey<Identifikasjonsmetadata>(m => m.Arkivreferanse)
            .OnDelete(DeleteBehavior.Restrict);
        var skjema = modell.Entity<Skjemametadata>();
        skjema.ToTable("skjemametadata");
        skjema.HasKey(m => m.Arkivreferanse);
        skjema.Property(m => m.Arkivreferanse).HasMaxLength(80);
        skjema.Property(m => m.Skjematype).HasMaxLength(500);
        skjema.Property(m => m.Tiltakstyper).HasColumnType("text[]");
        var kommune = modell.Entity<Kommune>();
        kommune.ToTable("kommune");
        kommune.HasKey(k => k.Kommunenummer);
        kommune.Property(k => k.Kommunenummer).HasMaxLength(4);
        kommune.Property(k => k.NyttKommunenummer).HasMaxLength(4);
        foreach (var entitet in modell.Model.GetEntityTypes())
            foreach (var egenskap in entitet.GetProperties())
                egenskap.SetColumnName(TilSlangeform(egenskap.Name));
        // Identifikasjonen deler tabell og primærnøkkel med eiendommen.
        modell.Entity<Eiendom>().OwnsOne(e => e.Eiendomsidentifikasjon)
            .Property<long>("EiendomId").HasColumnName("id");
    }

    private static string TilSlangeform(string tekst) => string.Concat(tekst.Select((tegn, i) =>
        char.IsUpper(tegn) && i > 0 ? "_" + char.ToLowerInvariant(tegn) : char.ToLowerInvariant(tegn).ToString()));
}
