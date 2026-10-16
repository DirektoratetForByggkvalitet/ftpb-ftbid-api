using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ftpb.Ftbid.Infrastructure.Migrasjoner
{
    /// <inheritdoc />
    public partial class InnforEiendomsmodell : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bevar alle gamle verdier før tabeller og prosjektkolonner erstattes.
            migrationBuilder.Sql("""
                SELECT pg_advisory_xact_lock(7099999);
                CREATE TEMP TABLE gamle_eiendom_prosjekter ON COMMIT DROP AS SELECT * FROM ftbid;
                CREATE TEMP TABLE gamle_eiendom_matrikler ON COMMIT DROP AS
                  SELECT m.*, (row_number() OVER (PARTITION BY ftbid ORDER BY rekkefolge, id) - 1)::integer AS ny_rekkefolge
                  FROM matrikkelopplysning m;
                CREATE TEMP TABLE gamle_eiendom_adresser ON COMMIT DROP AS SELECT * FROM adresse;
                """);

            migrationBuilder.DropTable(
                name: "adresse");

            migrationBuilder.DropTable(
                name: "matrikkelopplysning");

            migrationBuilder.DropIndex(
                name: "IX_ftbid_kommunenummer",
                table: "ftbid");

            migrationBuilder.DropIndex(
                name: "IX_ftbid_prosjektidentitet",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "adresse",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "bruksenhetsnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "bruksnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "bygningsnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "festenummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "gaardsnummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "kommunenummer",
                table: "ftbid");

            migrationBuilder.DropColumn(
                name: "seksjonsnummer",
                table: "ftbid");

            migrationBuilder.CreateTable(
                name: "eiendom",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ftbid = table.Column<string>(type: "character varying(18)", nullable: false),
                    rekkefolge = table.Column<int>(type: "integer", nullable: false),
                    kommunenummer = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    gaardsnummer = table.Column<int>(type: "integer", nullable: false),
                    bruksnummer = table.Column<int>(type: "integer", nullable: false),
                    festenummer = table.Column<int>(type: "integer", nullable: false),
                    seksjonsnummer = table.Column<int>(type: "integer", nullable: false),
                    bygningsnummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bolignummer = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    kommunenavn = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiendom", x => x.id);
                    table.ForeignKey(
                        name: "FK_eiendom_ftbid_ftbid",
                        column: x => x.ftbid,
                        principalTable: "ftbid",
                        principalColumn: "ean",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "eiendomsadresse",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    eiendom_id = table.Column<long>(type: "bigint", nullable: false),
                    rekkefolge = table.Column<int>(type: "integer", nullable: false),
                    adresselinje1 = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    adresselinje2 = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    adresselinje3 = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    postnr = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    poststed = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    landkode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    gatenavn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    husnr = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    bokstav = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_eiendomsadresse", x => x.id);
                    table.ForeignKey(
                        name: "FK_eiendomsadresse_eiendom_eiendom_id",
                        column: x => x.eiendom_id,
                        principalTable: "eiendom",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_eiendom_ftbid_rekkefolge",
                table: "eiendom",
                columns: new[] { "ftbid", "rekkefolge" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_eiendom_kommunenummer_gaardsnummer_bruksnummer",
                table: "eiendom",
                columns: new[] { "kommunenummer", "gaardsnummer", "bruksnummer" });

            migrationBuilder.CreateIndex(
                name: "IX_eiendomsadresse_eiendom_id_rekkefolge",
                table: "eiendomsadresse",
                columns: new[] { "eiendom_id", "rekkefolge" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO eiendom (id, ftbid, rekkefolge, kommunenummer, gaardsnummer, bruksnummer,
                  festenummer, seksjonsnummer, bygningsnummer, bolignummer)
                SELECT m.id, m.ftbid, m.ny_rekkefolge, m.kommunenummer, m.gaardsnummer, m.bruksnummer,
                  m.festenummer, m.seksjonsnummer,
                  CASE WHEN m.ny_rekkefolge = 0 THEN f.bygningsnummer END,
                  CASE WHEN m.ny_rekkefolge = 0 THEN f.bruksenhetsnummer END
                FROM gamle_eiendom_matrikler m JOIN gamle_eiendom_prosjekter f ON f.ean = m.ftbid;
                SELECT setval(pg_get_serial_sequence('eiendom', 'id'),
                  GREATEST(COALESCE((SELECT max(id) FROM eiendom), 0), 1), EXISTS (SELECT 1 FROM eiendom));
                -- Bevar også eldre hovedopplysninger hvis en prosjektliste manglet barn.
                INSERT INTO eiendom (ftbid, rekkefolge, kommunenummer, gaardsnummer, bruksnummer,
                  festenummer, seksjonsnummer, bygningsnummer, bolignummer)
                SELECT f.ean, 0, f.kommunenummer, f.gaardsnummer, f.bruksnummer, f.festenummer,
                  f.seksjonsnummer, f.bygningsnummer, f.bruksenhetsnummer
                FROM gamle_eiendom_prosjekter f WHERE NOT EXISTS (SELECT 1 FROM eiendom e WHERE e.ftbid = f.ean);
                INSERT INTO eiendomsadresse (id, eiendom_id, rekkefolge, adresselinje1)
                SELECT a.id, e.id, a.rekkefolge, a.adressetekst FROM gamle_eiendom_adresser a
                  JOIN eiendom e ON e.ftbid = a.ftbid AND e.rekkefolge = 0;
                SELECT setval(pg_get_serial_sequence('eiendomsadresse', 'id'),
                  GREATEST(COALESCE((SELECT max(id) FROM eiendomsadresse), 0), 1), EXISTS (SELECT 1 FROM eiendomsadresse));
                INSERT INTO eiendomsadresse (eiendom_id, rekkefolge, adresselinje1)
                SELECT e.id, 0, f.adresse FROM gamle_eiendom_prosjekter f JOIN eiendom e ON e.ftbid = f.ean AND e.rekkefolge = 0
                  WHERE f.adresse IS NOT NULL AND NOT EXISTS (SELECT 1 FROM eiendomsadresse a WHERE a.eiendom_id = e.id);
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM gamle_eiendom_matrikler m LEFT JOIN eiendom e ON e.id = m.id WHERE e.id IS NULL)
                    OR EXISTS (SELECT 1 FROM gamle_eiendom_adresser a LEFT JOIN eiendomsadresse n ON n.id = a.id WHERE n.id IS NULL)
                  THEN RAISE EXCEPTION 'Eiendomsmigrasjonen kunne ikke bevare alle tidligere matrikler og adresser.'; END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new System.NotSupportedException(
                "Eiendomsmodellen kan ikke rulles tilbake uten tap av strukturerte adresser og eiendomsløse prosjekter. Gjenopprett en sikkerhetskopi ved behov.");
        }
    }
}
