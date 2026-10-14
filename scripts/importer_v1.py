#!/usr/bin/env python3
"""Validerer og importerer v1-uttrekk til prosjektets lokale PostgreSQL-database."""
import argparse
import csv
import hashlib
import io
import json
import random
import subprocess
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from zoneinfo import ZoneInfo


def sqlverdi(verdi):
    """Siterer data som SQL-litteraler, uten å tolke filinnhold som instruksjoner."""
    return "NULL" if verdi is None else "'" + str(verdi).replace("'", "''") + "'"


def legg_til_rader(utdata, tabell, rader):
    """Skriver innsettinger i avgrensede grupper til en midlertidig tabell."""
    for start in range(0, len(rader), 1000):
        gruppe = rader[start:start + 1000]
        utdata.write(f"INSERT INTO {tabell} VALUES\n")
        utdata.write(",\n".join("(" + ",".join(map(sqlverdi, rad)) + ")" for rad in gruppe))
        utdata.write(";\n")


def les_csv(filsti, kolonner):
    """Leser et CSV-uttrekk uten overskriftsrad og kontrollerer kolonneantallet."""
    with filsti.open(encoding="utf-8-sig", newline="") as fil:
        rader = list(csv.reader(fil))
    if not rader or any(len(rad) != kolonner for rad in rader):
        raise ValueError(f"{filsti.name} må ha {kolonner} kolonner og minst én rad.")
    return rader


def lag_import(ftbidfil, loggfil, antall, fro):
    """Validerer kildeverdier og lager atomisk SQL med reproduserbare testdata."""
    ftbider = les_csv(ftbidfil, 11)
    logger = les_csv(loggfil, 2)
    identifikatorer = {rad[0] for rad in ftbider}
    if len(identifikatorer) != len(ftbider):
        raise ValueError("FtbIds.csv inneholder duplikate ftbid-er.")
    if len({rad[0] for rad in logger}) != len(logger):
        raise ValueError("FormWorkflowLogs.csv inneholder duplikate arkivreferanser.")
    if any(rad[1] not in identifikatorer for rad in logger):
        raise ValueError("En arkivreferanse viser til en ftbid som mangler i uttrekket.")
    if any(not rad[0] or len(rad[0]) > 80 for rad in logger):
        raise ValueError("En arkivreferanse er tom eller for lang.")
    prefikser = {rad[0][:7] for rad in ftbider}
    if len(prefikser) != 1:
        raise ValueError("Uttrekket må bruke ett GS1-prefiks for kortoppslag.")
    if antall < 0 or antall > len(ftbider):
        raise ValueError("Antallet testprosjekter er utenfor tillatt område.")
    prosjekter, matrikler, adresser = [], [], []
    identiteter = set()
    for rad in ftbider:
        ean, system, utstedt, kommune, gard, bruk, nummer, navn, feste, seksjon, adresse = rad
        if len(ean) != 18 or not ean.isascii() or not ean.isdigit() or ean[7:10] != "000":
            raise ValueError("En ftbid har ugyldig format.")
        kontroll = (10 - sum(int(c) * (3 if i % 2 == 0 else 1) for i, c in enumerate(ean[:-1])) % 10) % 10
        if kontroll != int(ean[-1]):
            raise ValueError("En ftbid har ugyldig kontrollsiffer.")
        if len(kommune) != 4 or not kommune.isascii() or not kommune.isdigit():
            raise ValueError("Et kommunenummer har ugyldig format.")
        tall = [int(verdi) for verdi in [gard, bruk, feste, seksjon]]
        if min(tall) < 0 or max(tall) > 2_147_483_647:
            raise ValueError("Et matrikkelnummer er utenfor tillatt område.")
        if not system.strip() or not navn.strip() or len(system) > 200 or len(navn) > 500 or len(nummer) > 200 or len(adresse) > 1000:
            raise ValueError("Prosjektopplysninger bryter feltbegrensningene.")
        normalisert = navn.strip().lower()
        identitet = (kommune, *tall, normalisert)
        if identitet in identiteter:
            raise ValueError("Uttrekket inneholder flere ftbid-er for samme prosjektidentitet.")
        identiteter.add(identitet)
        tidspunkt = datetime.fromisoformat(utstedt)
        if tidspunkt.tzinfo is not None:
            raise ValueError("V1-formatet skal inneholde lokale tidsstempler uten tidssone.")
        tidspunkt = tidspunkt.replace(tzinfo=ZoneInfo("Europe/Oslo")).astimezone(timezone.utc).isoformat()
        prosjekter.append([ean, kommune, *tall, adresse or None, navn, normalisert, nummer or None, system, tidspunkt])
        matrikler.append([ean, 0, kommune, *tall])
        if adresse.strip():
            adresser.append([ean, 0, adresse])
    utvalg = sorted(random.Random(fro).sample(sorted(identifikatorer), antall))
    kart = {rad[0]: rad for rad in ftbider}
    testdata = []
    for i, ean in enumerate(utvalg, 1):
        rad = kart[ean]
        for j in range(1, 3):
            rekkefolge = j if rad[10].strip() else j - 1
            tekst = f"[SYNTETISKE TESTDATA] Testadresse {i}-{j}"
            adresser.append([ean, rekkefolge, tekst])
            # Store, tydelig kunstige gårds-/bruksnummer, med opprinnelig kommunenummer.
            matrikkel = [ean, j, rad[3], 9_000_000 + i, 900_000 + j, 0, j]
            matrikler.append(matrikkel)
            testdata.append({"Ftbid": ean, "Adresse": tekst, "AdresseRekkefolge": rekkefolge,
                             "MatrikkelRekkefolge": j, "Kommunenummer": rad[3],
                             "Gaardsnummer": matrikkel[3], "Bruksnummer": matrikkel[4],
                             "Festenummer": 0, "Seksjonsnummer": j})
    utdata = io.StringIO()
    utdata.write("""\\set ON_ERROR_STOP on
BEGIN;
SET LOCAL standard_conforming_strings = on;
SELECT pg_advisory_xact_lock(7099999);
CREATE TEMP TABLE v1_ftbid ON COMMIT DROP AS SELECT ean, kommunenummer, gaardsnummer, bruksnummer, festenummer, seksjonsnummer, adresse, prosjektnavn, normalisert_prosjektnavn, prosjektnummer, system, utstedt FROM ftbid WITH NO DATA;
CREATE TEMP TABLE v1_kobling (arkivreferanse varchar(80), ftbid varchar(18)) ON COMMIT DROP;
CREATE TEMP TABLE v1_adresse (ftbid varchar(18), rekkefolge integer, adressetekst varchar(1000)) ON COMMIT DROP;
CREATE TEMP TABLE v1_matrikkel (ftbid varchar(18), rekkefolge integer, kommunenummer varchar(4), gaardsnummer integer,
 bruksnummer integer, festenummer integer, seksjonsnummer integer) ON COMMIT DROP;
""")
    legg_til_rader(utdata, "v1_ftbid", prosjekter)
    legg_til_rader(utdata, "v1_kobling", logger)
    legg_til_rader(utdata, "v1_adresse", adresser)
    legg_til_rader(utdata, "v1_matrikkel", matrikler)
    utdata.write("""
DO $$ BEGIN
 IF EXISTS (SELECT 1 FROM ftbid f JOIN v1_ftbid v USING (ean)
   WHERE ROW(f.kommunenummer,f.gaardsnummer,f.bruksnummer,f.festenummer,f.seksjonsnummer,f.adresse,f.prosjektnavn,
     f.normalisert_prosjektnavn,f.prosjektnummer,f.system,f.utstedt)
   IS DISTINCT FROM ROW(v.kommunenummer,v.gaardsnummer,v.bruksnummer,v.festenummer,v.seksjonsnummer,v.adresse,v.prosjektnavn,
     v.normalisert_prosjektnavn,v.prosjektnummer,v.system,v.utstedt))
 THEN RAISE EXCEPTION 'En eksisterende ftbid har andre verdier. Importen er avbrutt uten overskriving.'; END IF;
 IF EXISTS (SELECT 1 FROM identifikasjonsmetadata m JOIN v1_kobling v USING (arkivreferanse) WHERE m.ftbid <> v.ftbid)
 THEN RAISE EXCEPTION 'En arkivreferanse har en annen tilknytning. Importen er avbrutt.'; END IF;
 IF EXISTS (SELECT 1 FROM adresse a JOIN v1_adresse v USING (ftbid, rekkefolge) WHERE a.adressetekst <> v.adressetekst)
 THEN RAISE EXCEPTION 'En adresseposisjon inneholder andre verdier. Importen er avbrutt.'; END IF;
 IF EXISTS (SELECT 1 FROM matrikkelopplysning m JOIN v1_matrikkel v USING (ftbid, rekkefolge)
   WHERE ROW(m.kommunenummer,m.gaardsnummer,m.bruksnummer,m.festenummer,m.seksjonsnummer)
   IS DISTINCT FROM ROW(v.kommunenummer,v.gaardsnummer,v.bruksnummer,v.festenummer,v.seksjonsnummer))
 THEN RAISE EXCEPTION 'En matrikkelposisjon inneholder andre verdier. Importen er avbrutt.'; END IF;
END $$;
INSERT INTO ftbid (ean, kommunenummer, gaardsnummer, bruksnummer, festenummer, seksjonsnummer, adresse,
 prosjektnavn, normalisert_prosjektnavn, prosjektnummer, system, utstedt)
 SELECT * FROM v1_ftbid ON CONFLICT (ean) DO NOTHING;
INSERT INTO skjemametadata (arkivreferanse, skjematype, tiltakstyper)
 SELECT arkivreferanse, NULL, ARRAY[]::text[] FROM v1_kobling ON CONFLICT (arkivreferanse) DO NOTHING;
INSERT INTO identifikasjonsmetadata (arkivreferanse, ftbid)
 SELECT * FROM v1_kobling ON CONFLICT (arkivreferanse) DO NOTHING;
INSERT INTO adresse (ftbid, rekkefolge, adressetekst)
 SELECT * FROM v1_adresse ON CONFLICT (ftbid, rekkefolge) DO NOTHING;
INSERT INTO matrikkelopplysning (ftbid, rekkefolge, kommunenummer, gaardsnummer, bruksnummer, festenummer, seksjonsnummer)
 SELECT * FROM v1_matrikkel ON CONFLICT (ftbid, rekkefolge) DO NOTHING;
DO $$ BEGIN
 IF (SELECT count(*) FROM ftbid f JOIN v1_ftbid v USING (ean)) <> (SELECT count(*) FROM v1_ftbid)
 THEN RAISE EXCEPTION 'Importkontrollen av ftbid-er feilet.'; END IF;
 IF (SELECT count(*) FROM identifikasjonsmetadata m JOIN v1_kobling v USING (arkivreferanse,ftbid)) <> (SELECT count(*) FROM v1_kobling)
 THEN RAISE EXCEPTION 'Importkontrollen av koblinger feilet.'; END IF;
END $$;
SELECT setval('ftbid_sekvens', GREATEST((SELECT last_value FROM ftbid_sekvens),
 (SELECT max(substring(ean from 11 for 7)::bigint) FROM ftbid)), true);
COMMIT;
SELECT 'ftbid' AS tabell, count(*) AS antall FROM ftbid
UNION ALL SELECT 'identifikasjonsmetadata', count(*) FROM identifikasjonsmetadata
UNION ALL SELECT 'adresse', count(*) FROM adresse
UNION ALL SELECT 'matrikkelopplysning', count(*) FROM matrikkelopplysning;
""")
    rapport = {"Ftbider": len(ftbider), "Koblinger": len(logger), "AdresserFraKilden": sum(bool(r[10].strip()) for r in ftbider),
               "Testprosjekter": antall, "Tilfeldighetsfro": fro, "Gs1Prefiks": next(iter(prefikser)),
               "HoyesteImporterteSekvens": max(int(r[0][10:17]) for r in ftbider), "TidssoneForKildedata": "Europe/Oslo",
               "ManglendeSkjemadata": "Skjematype er null og tiltakstyper er tomme; verdier er ikke oppdiktet.",
               "SyntetiskeVerdier": testdata,
               "Kilder": [{"Fil": p.name, "Sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in [ftbidfil, loggfil]]}
    return utdata.getvalue(), rapport


def hoved():
    """Lager import og rapport; skriver bare til databasen når --utfor er oppgitt."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--ftbider", type=Path, required=True)
    parser.add_argument("--koblinger", type=Path, required=True)
    parser.add_argument("--testprosjekter", type=int, default=30)
    parser.add_argument("--fro", type=int, default=20261008)
    parser.add_argument("--utfor", action="store_true")
    argumenter = parser.parse_args()
    sql, rapport = lag_import(argumenter.ftbider, argumenter.koblinger, argumenter.testprosjekter, argumenter.fro)
    prosjekt = Path(__file__).resolve().parents[1]
    mappe = prosjekt / "data" / "import-v1"
    mappe.mkdir(parents=True, exist_ok=True)
    (mappe / ".gitignore").write_text("*\n!.gitignore\n", encoding="utf-8")
    sqlfil = mappe / "import.sql"
    sqlfil.write_text(sql, encoding="utf-8")
    if argumenter.utfor:
        docker = ["docker", "compose", "exec", "-T", "postgres"]
        tidspunkt = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
        with (mappe / f"sikkerhetskopi-{tidspunkt}.sql").open("wb") as kopi:
            subprocess.run(docker + ["pg_dump", "-U", "ftbid", "-d", "ftbid"], cwd=prosjekt, stdout=kopi, check=True)
        with sqlfil.open("rb") as fil:
            subprocess.run(docker + ["psql", "-U", "ftbid", "-d", "ftbid", "-q", "-v", "ON_ERROR_STOP=1"], cwd=prosjekt, stdin=fil, check=True)
        rapport["Importert"] = True
    else:
        rapport["Importert"] = False
    (mappe / "rapport.json").write_text(json.dumps(rapport, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in rapport.items() if k not in ["SyntetiskeVerdier", "Kilder"]}, ensure_ascii=False))


if __name__ == "__main__":
    hoved()
