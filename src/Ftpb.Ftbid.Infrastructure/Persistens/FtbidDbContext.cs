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
        ftbid.Property(f => f.Kommunenummer).HasMaxLength(4);
        ftbid.Property(f => f.Prosjektnavn).HasMaxLength(500);
        ftbid.Property(f => f.NormalisertProsjektnavn).HasMaxLength(500);
        ftbid.Property(f => f.Prosjektnummer).HasMaxLength(200);
        ftbid.Property(f => f.EksternProsjektreferanse).HasMaxLength(200);
        ftbid.Property(f => f.KommunaltSaksnummer).HasMaxLength(200);
        ftbid.Property(f => f.Tiltakshaver).HasMaxLength(500);
        ftbid.Property(f => f.Organisasjonsnummer).HasMaxLength(9);
        ftbid.Property(f => f.AnsvarligSokerOrganisasjonsnummer).HasMaxLength(9);
        ftbid.Property(f => f.Bruksenhetsnummer).HasMaxLength(50);
        ftbid.Property(f => f.Bygningsnummer).HasMaxLength(50);
        ftbid.Property(f => f.Tiltakstype).HasMaxLength(200);
        ftbid.HasIndex(f => f.Kommunenummer);
        ftbid.Property(f => f.System).HasMaxLength(200);
        ftbid.Property(f => f.Adresse).HasMaxLength(1000);
        ftbid.HasIndex(f => new { f.Kommunenummer, f.Gaardsnummer, f.Bruksnummer, f.Festenummer, f.Seksjonsnummer, f.NormalisertProsjektnavn }).IsUnique().HasDatabaseName("IX_ftbid_prosjektidentitet");
        ftbid.HasMany(f => f.Adresser).WithOne().HasForeignKey(a => a.Ftbid).OnDelete(DeleteBehavior.Cascade);
        ftbid.HasMany(f => f.Matrikkelopplysninger).WithOne().HasForeignKey(m => m.Ftbid).OnDelete(DeleteBehavior.Cascade);
        ftbid.HasMany(f => f.Identifikasjonsmetadata).WithOne().HasForeignKey(m => m.Ftbid).OnDelete(DeleteBehavior.Restrict);
        var adresse = modell.Entity<Adresse>();
        adresse.ToTable("adresse");
        adresse.HasKey(a => a.Id);
        adresse.Property(a => a.Adressetekst).HasMaxLength(1000);
        adresse.HasIndex(a => new { a.Ftbid, a.Rekkefolge }).IsUnique();
        var matrikkel = modell.Entity<Matrikkelopplysning>();
        matrikkel.ToTable("matrikkelopplysning");
        matrikkel.HasKey(m => m.Id);
        matrikkel.Property(m => m.Kommunenummer).HasMaxLength(4);
        matrikkel.HasIndex(m => new { m.Kommunenummer, m.Gaardsnummer, m.Bruksnummer });
        matrikkel.HasIndex(m => new { m.Ftbid, m.Rekkefolge }).IsUnique();
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
    }

    private static string TilSlangeform(string tekst) => string.Concat(tekst.Select((tegn, i) =>
        char.IsUpper(tegn) && i > 0 ? "_" + char.ToLowerInvariant(tegn) : char.ToLowerInvariant(tegn).ToString()));
}
