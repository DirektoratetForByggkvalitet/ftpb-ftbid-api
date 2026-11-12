#!/usr/bin/env python3
"""Eksporterer kun data fra lokal Compose-PostgreSQL til dev-imaget."""
import gzip
import hashlib
import subprocess
from pathlib import Path


def klargjor(sql):
    # psql-met kommandoer kan ikke kjøres gjennom Npgsql. transaction_timeout
    # finnes først i PostgreSQL 17; dumpet skal også kunne leses av PostgreSQL 16.
    return "\n".join(linje for linje in sql.splitlines()
                     if not linje.startswith("\\")
                     and linje != "SET transaction_timeout = 0;") + "\n"


if __name__ == "__main__":
    prosjekt = Path(__file__).resolve().parents[1]
    tabeller = ["ftbid", "eiendom", "eiendomsadresse", "identifikasjonsmetadata",
                "kommune", "skjemametadata", "ftbid_sekvens"]
    resultat = subprocess.run(
        ["docker", "compose", "exec", "-T", "postgres", "pg_dump", "-U", "ftbid", "-d", "ftbid",
         "--data-only", "--column-inserts", "--rows-per-insert=500", "--no-owner", "--no-privileges",
         *[f"--table=public.{tabell}" for tabell in tabeller]],
        cwd=prosjekt, capture_output=True, check=True)
    data = klargjor(resultat.stdout.decode("utf-8")).encode("utf-8")
    fil = prosjekt / "data/dev-seed/lokal.sql.gz"
    fil.parent.mkdir(parents=True, exist_ok=True)
    fil.write_bytes(gzip.compress(data, mtime=0))
    print(f"Skrev {fil.relative_to(prosjekt)} ({fil.stat().st_size} byte), SQL SHA256: {hashlib.sha256(data).hexdigest()}")
