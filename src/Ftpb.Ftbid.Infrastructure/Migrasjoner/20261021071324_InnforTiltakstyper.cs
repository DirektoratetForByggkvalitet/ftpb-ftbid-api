using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ftpb.Ftbid.Infrastructure.Migrasjoner
{
    /// <inheritdoc />
    public partial class InnforTiltakstyper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "tiltakstyper",
                table: "ftbid",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            // Bevar historiske verdier. Hash beregnes i .NET ved sammenligning,
            // slik at eldre data følger nøyaktig samme Unicode-regler som nye.
            migrationBuilder.Sql("""
                UPDATE ftbid SET tiltakstyper = ARRAY[tiltakstype]
                WHERE tiltakstype IS NOT NULL AND btrim(tiltakstype) <> '';
                """);
            migrationBuilder.DropColumn(name: "tiltakstype", table: "ftbid");

            migrationBuilder.AddColumn<string>(
                name: "tiltakstyper_hash",
                table: "ftbid",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ftbid_tiltakstyper_hash",
                table: "ftbid",
                column: "tiltakstyper_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Den gamle modellen kan ikke representere flere typer uten datatap.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ftbid WHERE cardinality(tiltakstyper) > 1) THEN
                        RAISE EXCEPTION 'Kan ikke nedgradere: ftbid har flere tiltakstyper.';
                    END IF;
                END $$;
                """);
            migrationBuilder.AddColumn<string>(
                name: "tiltakstype", table: "ftbid", type: "character varying(200)",
                maxLength: 200, nullable: true);
            migrationBuilder.Sql("UPDATE ftbid SET tiltakstype = tiltakstyper[1];");
            migrationBuilder.DropIndex(name: "IX_ftbid_tiltakstyper_hash", table: "ftbid");
            migrationBuilder.DropColumn(name: "tiltakstyper", table: "ftbid");
            migrationBuilder.DropColumn(name: "tiltakstyper_hash", table: "ftbid");
        }
    }
}
