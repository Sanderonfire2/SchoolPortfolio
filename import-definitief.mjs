// import-definitief.mjs — de échte data van de Google Site in app.db zetten
// v1: projecten (3de/4de jaar), foto's naar wwwroot/uploads/projecten, POV-locaties + rapporten,
//     profiel-over-mij tekst, en opruimen van testdata.
import fs from 'fs';
import path from 'path';
import { DatabaseSync } from 'node:sqlite';

const ROOT = path.resolve(import.meta.dirname);
const RAW = JSON.parse(fs.readFileSync(path.join(ROOT, 'import-raw.json'), 'utf8'));
const DB = new DatabaseSync(path.join(ROOT, 'app.db'));
const NU = new Date().toISOString().replace('T', ' ').slice(0, 19);

// ---------- helpers ----------
function slug(c) {
  return c.toLowerCase()
    .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
}
function kap(t) {
  const s = String(t ?? '').trim();
  return s.charAt(0).toUpperCase() + s.slice(1);
}
function korteSam(oms) {
  const eerste = oms.split('\n')[0] ?? '';
  const zin = eerste.split(/(?<=[.!?])\s/)[0] ?? eerste;
  return zin.length > 220 ? zin.slice(0, 217).trimEnd() + '…' : zin;
}

// ---------- 1. testdata weg ----------
for (const t of DB.prepare("SELECT Id FROM Projecten WHERE Categorie = 'test' OR Slug = 'test'").all()) {
  DB.prepare('DELETE FROM GallerijAfbeeldingen WHERE ProjectId = ?').run(t.Id);
  DB.prepare('DELETE FROM Projecten WHERE Id = ?').run(t.Id);
  console.log('testproject verwijderd:', t.Id);
}
for (const s of DB.prepare("SELECT Id FROM Stages WHERE Omschrijving = 'test' AND Taken = 'test'").all()) {
  DB.prepare('DELETE FROM Rapporten WHERE StageId = ?').run(s.Id);
  DB.prepare('DELETE FROM Stages WHERE Id = ?').run(s.Id);
  console.log('teststage verwijderd:', s.Id);
}

// ---------- 2. foto's downloaden ----------
const UPDIR = path.join(ROOT, 'wwwroot', 'uploads', 'projecten');
fs.mkdirSync(UPDIR, { recursive: true });
async function download(url, bestandsnaam) {
  const doelpad = path.join(UPDIR, bestandsnaam);
  if (fs.existsSync(doelpad)) return '/uploads/projecten/' + bestandsnaam;
  try {
    const r = await fetch(url);
    if (!r.ok) { console.log('  ! download ' + r.status, url.slice(0, 80)); return null; }
    const buf = Buffer.from(await r.arrayBuffer());
    // geen lege of html-antwoorden opslaan als beeld
    const tekst = buf.slice(0, 200).toString('latin1');
    if (buf.length < 1000 && tekst.includes('<')) return null;
    fs.writeFileSync(doelpad, buf);
    return '/uploads/projecten/' + bestandsnaam;
  } catch (e) { console.log('  ! download-fout', e.message); return null; }
}

// ---------- 3. projectpagina's ----------
const NAVBEKKEND = new Set(['Home', '3de jaar', '4de jaar', '5de jaar', 'Elektriciteit', 'Constructie',
  'Constructietechnieken', 'Over mij', 'More', 'Stage', 'POV Erperheide', 'POV Sint-Oda']);

function contentFrags(p) {
  const start = p.tekst.lastIndexOf('Over mij') + 1;
  return p.tekst.slice(start).filter(f => f && !NAVBEKKEND.has(f) && f !== p.titel);
}

const GROEPEN = [
  { label: '3de jaar · Elektriciteit', pad: '/3de-jaar/elektriciteit', soort: 'punct-kop' },
  { label: '3de jaar · Constructie', pad: '/3de-jaar/constructie', soort: 'punct-kop' },
  { label: '4de jaar · Constructietechnieken', pad: '/4de-jaar/constructietechnieken', soort: 'echte-pagina' },
  { label: '4de jaar · Elektriciteit', pad: '/4de-jaar/elektriciteit', soort: 'echte-pagina' },
];

async function verwerkProject({ titel, categorie, omschrijving, coverUrl, gallerijUrls }, volgNr) {
  const s = slug(titel);
  if (DB.prepare('SELECT Id FROM Projecten WHERE Slug = ?').get(s)) { console.log('  bestaat al:', s); return; }
  const oms = kap(omschrijving);
  const covPad = coverUrl ? await download(coverUrl, s + '-cover.jpg') : null;
  const galPaden = [];
  for (const [i, u] of gallerijUrls.entries()) {
    const pad = await download(u, `${s}-${i + 1}.jpg`);
    if (pad) galPaden.push(pad);
  }
  DB.prepare(`INSERT INTO Projecten (Titel, Slug, Categorie, KorteSamenvatting, Omschrijving, CoverPad, IsUitgelicht, IsGepubliceerd, Volgorde, BijgewerktOp)
    VALUES (?, ?, ?, ?, ?, ?, 0, 1, ?, ?)`).run(titel, s, categorie, korteSam(oms), oms, covPad, volgNr, NU);
  const proj = DB.prepare('SELECT Id FROM Projecten WHERE Slug = ?').get(s);
  let r = 1;
  for (const pad of galPaden) {
    DB.prepare(`INSERT INTO GallerijAfbeeldingen (ProjectId, Pad, Onderschrift, Volgorde, IsHoofdCover, Soort)
      VALUES (?, ?, NULL, ?, 0, 'afbeelding')`).run(proj.Id, pad, r++);
  }
  console.log('  ✓ ' + titel + ' — ' + (covPad ? 'cover +' : '') + ' ' + galPaden.length + ' galerij-foto\'s');
}

for (const groep of GROEPEN) {
  const pag = RAW.find(p => p.pad === groep.pad);
  if (!pag) { console.log('!' + groep.pad + ' niet in raw'); continue; }
  console.log('== ' + groep.label);

  if (groep.soort === 'punct-kop') {
    const frags = contentFrags(pag);
    const paren = [];
    for (let i = 0; i + 1 < frags.length; i += 2) paren.push({ kop: String(frags[i]), tek: String(frags[i + 1]).trim() });
    let volgNr = 1;
    for (const [i, pr] of paren.entries()) {
      await verwerkProject({
        titel: kap(pr.kop),
        categorie: groep.label,
        omschrijving: pr.tek,
        coverUrl: null,
        gallerijUrls: pag.imgs[i] ? [pag.imgs[i]] : [],
      }, volgNr++);
    }
    console.log('   → ' + paren.length + ' mini-projecten');
  } else {
    const kids = RAW.filter(p => p.pad.startsWith(groep.pad + '/') && contentFrags(p).length > 0);
    let volgNr = 1;
    for (const k of kids) {
      const frags = contentFrags(k);
      const titel = kap(String(frags[0]));
      const alinien = frags.slice(1).map(x => String(x).trim());
      await verwerkProject({
        titel,
        categorie: groep.label,
        omschrijving: alinien.join('\n\n'),
        coverUrl: k.imgs[0] ?? null,
        gallerijUrls: k.imgs.slice(1),
      }, volgNr++);
    }
    console.log('   → ' + kids.length + ' projectpagina\'s');
  }
}

// ---------- 4. PovLocaties + Rapporten ----------
DB.prepare(`INSERT OR IGNORE INTO PovLocaties (Naam, Slug, Bedrijf, Adres, Periode, Omschrijving, IsGepubliceerd, Volgorde)
  VALUES ('POV Erperheide', 'pov-erperheide', 'Center Parcs Erperheide', 'Peer', '2026 — heden', ?, 1, 1)`)
  .run('De vierde-klassraadperiode op Center Parcs Erperheide: elke week een volledig dagrapport geschreven.');
DB.prepare(`INSERT OR IGNORE INTO PovLocaties (Naam, Slug, Bedrijf, Adres, Periode, Omschrijving, IsGepubliceerd, Volorde, Volgorde)
  VALUES ('POV Sint-Oda', 'pov-sint-oda', 'Sint-Oda', 'Overpelt', 'aangekondigd — 2026', ?, 1, 2)`)
  .run('Nog te starten: hier verschijnen de dagrapporten zodra de periode een aanvang neemt.');
const erperheide = DB.prepare("SELECT Id FROM PovLocaties WHERE Slug = 'pov-erperheide'").get();
const RAPORTEN = [
  { pad: '/uploads/rapporten/2026-09-08-VanPraet-Sander.pdf', datum: '2026-09-08', titel: 'Dagrapport 08-09-2026' },
  { pad: '/uploads/rapporten/2026-09-15-VanPraet-Sander.pdf', datum: '2026-09-15', titel: 'Dagrapport 15-09-2026' },
  { pad: '/uploads/rapporten/2026-09-22-VanPraet-Sander.pdf', datum: '2026-09-22', titel: 'Dagrapport 22-09-2026' },
  { pad: '/uploads/rapporten/2026-09-29-VanPraet-Sander.pdf', datum: '2026-09-29', titel: 'Dagrapport 29-09-2026' },
];
for (const r of RAPORTEN) {
  if (!DB.prepare('SELECT 1 x FROM Rapporten WHERE Pad = ?').get(r.pad)) {
    DB.prepare(`INSERT INTO Rapporten (StageId, PovLocatieId, Pad, Titel, Datum, IsGepubliceerd, Volgorde)
      VALUES (NULL, ?, ?, ?, ?, 1, 0)`).run(erperheide.Id, r.pad, r.titel, r.datum);
    console.log('  ✓ rapport toegevoegd:', r.titel);
  }
}

// ---------- 5. profiel-over-mij tekst sync ----------
const overPag = RAW.find(p => p.pad === '/over-mij');
if (overPag) {
  const frags = contentFrags(overPag);
  const teksten = frags.filter(f => f.length > 60);
  // eerste lange gedeelte is de echte alinea; "Locatie school:" valt onder de 60
  if (teksten.length > 0) {
    const al = teksten[0].trim();
    DB.prepare(`UPDATE Profielen SET OverMijTekst = ?, KorteIntroductie = ?, BijgewerktOp = ?
      WHERE Id = (SELECT MIN(Id) FROM Profielen)`).run(al, korteSam(al), NU);
    console.log('  ✓ Profiel overmij-tekst geüpdatet (' + al.length + ' tekens)');
  }
}

// ---------- 6. sluit ----------
const totaal = DB.prepare('SELECT COUNT(*) c FROM Projecten').get().c;
console.log('TOTAAL projecten in DB:', totaal);
DB.close();
