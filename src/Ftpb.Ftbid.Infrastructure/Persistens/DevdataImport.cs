using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Ftpb.Ftbid.Infrastructure.Persistens;

/// <summary>Engangsinnlasting av lokalt datadump i en tom dev-database.</summary>
public static class DevdataImport
{
    // Fast nøkkel: et nytt snapshot skal heller ikke importeres over eksisterende data.
    private const string ImportId = "lokal-database-v1";

    /// <summary>Returnerer true ved import, false når importen allerede er utført.</summary>
    public static async Task<bool> ImporterAsync(FtbidDbContext database, string filsti)
    {
        await using var fil = File.OpenRead(filsti);
        await using var pakket = new GZipStream(fil, CompressionMode.Decompress);
        using var leser = new StreamReader(pakket);
        var sql = await leser.ReadToEndAsync();
        var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sql)));
        await using var transaksjon = await database.Database.BeginTransactionAsync();
        await database.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7099999)");
        await database.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS public.__devdata_import (
                id text PRIMARY KEY, sha256 text NOT NULL, importert timestamptz NOT NULL DEFAULT now())
            """);
        var ferdig = await database.Database.SqlQuery<bool>(
            $"SELECT EXISTS (SELECT 1 FROM public.__devdata_import WHERE id = {ImportId}) AS \"Value\"").SingleAsync();
        if (ferdig)
        {
            await transaksjon.CommitAsync();
            return false;
        }

        // Lås også tabellene, slik at andre skrivere ikke kan fylle dem mellom kontroll og import.
        await database.Database.ExecuteSqlRawAsync("""
            LOCK TABLE public.ftbid, public.eiendom, public.eiendomsadresse,
                public.identifikasjonsmetadata, public.kommune, public.skjemametadata IN EXCLUSIVE MODE;
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM public.ftbid) OR EXISTS (SELECT 1 FROM public.eiendom)
                  OR EXISTS (SELECT 1 FROM public.eiendomsadresse) OR EXISTS (SELECT 1 FROM public.identifikasjonsmetadata)
                  OR EXISTS (SELECT 1 FROM public.kommune) OR EXISTS (SELECT 1 FROM public.skjemametadata)
                THEN RAISE EXCEPTION 'Dev-import krever tomme datatabeller. Ingen eksisterende data er endret.';
                END IF;
            END $$;
            """);
        await using var kommando = database.Database.GetDbConnection().CreateCommand();
        kommando.Transaction = transaksjon.GetDbTransaction();
        kommando.CommandTimeout = 300;
        kommando.CommandText = sql;
        await kommando.ExecuteNonQueryAsync();
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO public.__devdata_import (id, sha256) VALUES ({ImportId}, {hash})");
        await transaksjon.CommitAsync();
        return true;
    }
}
