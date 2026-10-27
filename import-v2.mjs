// import-v2.mjs — de échte Google-Sites-data in app.db zetten.
// · Schoolcarrière-hiërarchie: Schooljaar + Vak-kolommen (geen categorienamen als projecten)
// · afbeeldingen staan al lokaal in wwwroot/uploads/projecten
// · geen indexpagina's als project, geen dubbele projecten, eerste afbeelding = cover
import fs from 'fs';
import path from 'path';
import { DatabaseSync } from 'node:sqlite';

const ROOT = path.resolve(import.meta.dirname);
const gsData = JSON.parse(fs.readFileSync(path.join(ROOT, 'gs-data.json'), 'utf8'));
const DB = new DatabaseSync(path.join(ROOT, 'app.db'));
const NU = new Date().toISOString().replace('T', ' ').slice(0, 19);

function slug(c) {
  return String(c).toLowerCase()
    .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');
}
function korteSam(oms) {
  const eerste = (oms.split('\n').find(l => l.trim()) ?? '');
  const zin = eerste.split(/(?<=[.!?])\s/)[0] ?? eerste;
  return zin.length > 220 ? zin.slice(0, 217).trimEnd() + '…' : zin;
}
function jaarUit(pad) { return pad.includes('/3de-jaar/') ? '3de jaar' : '4de jaar'; }
function vakUit(pad) {
  if (pad.includes('/elektriciteit')) return 'Elektriciteit';
  if (pad.includes('/constructietechnieken')) return 'Constructietechnieken';
  if (pad.includes('/constructie')) return 'Constructie';
  return null;
}

// ---------- 1. schoon vegen ----------
DB.prepare('DELETE FROM GallerijAfbeeldingen').run();
DB.prepare('DELETE FROM Projecten').run();
console.log('Projecten en galerij leeggemaakt.');

// ---------- 2. projecttitels uit de indexkoppen (originele schrijfwijze) ----------
const titelHulp = {
  'draadplooioefening': 'Opdracht 1:Draadplooioefening',
  'buisplooioefening': 'Opdracht 2: Buisplooi oefening',
  'basis-schakeling-1': 'Basisschakeling 1',
  'basisschakeling-2-2lampen-serie': 'Bassisschakeling 2',
  '2-lampen-parralel': 'Basisschakeling 3',
  'dubbele-contactdoos': 'Contactdoos',
  'inbouw-enkelpolig-controle': 'Inbouw enkelpolig controle',
  'inbouw-enkelpolig-verlicht': 'Inbouw enkelpolig verlicht',
  'inbouw-dubbele-aansteking': 'Dubbele aansteking',
  'inbouw-wissel': 'Inbouw wissel',
};

// Omschrijvingen uit de elektriciteits-index (voor kindpagina's zonder eigen tekst):
const omsUitIndex = {};
{
  const ixInhoud = gsData['/view/sander-van-praet/3de-jaar/elektriciteit'].inhoud;
  let laatsteKop = null;
  for (const b of ixInhoud) {
    if (b.k === 'h' && b.lvl === 1 && !/^(Elektriciteit 3de jaar\.|Opdrachten 3EL)$/.test(b.text)) {
      laatsteKop = b.text;
      if (!omsUitIndex[laatsteKop]) omsUitIndex[laatsteKop] = [];
    } else if (b.k === 'p' && laatsteKop) {
      omsUitIndex[laatsteKop].push(b.text);
    }
  }
}

// ---------- 3. project invoegen ----------
function voegIn({ titel, slugNaam, schooljaar, vak, omschrijving, bestanden, soorten }) {
  if (DB.prepare('SELECT Id FROM Projecten WHERE Slug = ?').get(slugNaam)) {
    console.log('  ~~ dubbele slug overgeslagen:', slugNaam);
    return null;
  }
  const cat = vak ? `${schooljaar} · ${vak}` : schooljaar;
  DB.prepare(`INSERT INTO Projecten (Titel, Slug, Categorie, Schooljaar, Vak, KorteSamenvatting, Omschrijving, IsUitgelicht, IsGepubliceerd, Volgorde, BijgewerktOp)
    VALUES (?, ?, ?, ?, ?, ?, ?, 0, 1, (SELECT COALESCE(MAX(Volgorde),0)+1 FROM Projecten), ?)`)
    .run(titel, slugNaam, cat, schooljaar, vak, korteSam(omschrijving), omschrijving, NU);
  const proj = DB.prepare('SELECT Id FROM Projecten WHERE Slug = ?').get(slugNaam);

  let r = 1;
  const paden = [];
  const gezien = new Set();
  for (const [i, bestand] of bestanden.entries()) {
    if (!bestand || gezien.has(bestand)) continue;
    gezien.add(bestand);
    const pad = '/uploads/projecten/' + bestand;
    const soort = soorten && soorten[i] ? soorten[i] : 'afbeelding';
    // Alleen echt lokaal aanwezige bestanden koppelen:
    if (!fs.existsSync(path.join(ROOT, 'wwwroot', pad.slice(1)))) continue;
    DB.prepare(`INSERT INTO GallerijAfbeeldingen (ProjectId, Pad, Soort, Onderschrift, IsHoofdCover, Volgorde)
      VALUES (?, ?, ?, NULL, ?, ?)`)
      .run(proj.Id, pad, soort, soort === 'afbeelding' && paden.length === 0 ? 1 : 0, r++);
    paden.push(pad);
  }
  if (paden.length > 0) {
    DB.prepare('UPDATE Projecten SET CoverPad = ? WHERE Id = ?').run(paden[0], proj.Id);
  }
  invoeg++;
  console.log(`  ✓ ${titel} (${schooljaar} · ${vak}) — ${paden.length} afbeelding(en)`);
  return proj.Id;
}
let invoeg = 0;

// ---------- 4. elk onderliggend paginapad importeren ----------
const INDEXPADEN = new Set([
  '/view/sander-van-praet/3de-jaar/elektriciteit',
  '/view/sander-van-praet/3de-jaar/constructie',
]);
// Mini-items zonder eigen pagina (beschrijving + afbeelding uit de indexpagina):
const miniUitIndex = {
  'Wisselschakelaar': { pad: '/view/sander-van-praet/3de-jaar/elektriciteit', imgsFromKop: 'Wisselschakelaar' },
  'Buitenlamp': { pad: '/view/sander-van-praet/3de-jaar/elektriciteit', imgsFromKop: 'Buitenlamp' },
  'Enkel stopcontact inbouw': { pad: '/view/sander-van-praet/3de-jaar/elektriciteit', imgsFromKop: 'Enkel stopcontact inbouw' },
  'Meervoudige stopcontacten': { pad: '/view/sander-van-praet/3de-jaar/elektriciteit', imgsFromKop: 'Meervoudige stopcontacten' },
  'Aanhanger stekker aansluiten': { pad: '/view/sander-van-praet/3de-jaar/constructie', imgsFromKop: 'Aanhanger stekker aansluiten' },
  'Werking van de popnagel tang': { pad: '/view/sander-van-praet/3de-jaar/constructie', imgsFromKop: 'Werking van de popnagel tang' },
  'koolstofborstels van boormachine vervangen': { pad: '/view/sander-van-praet/3de-jaar/constructie', imgsFromKop: 'koolstofborstels van boormachine vervangen' },
  'Een balk monteren in een muur van steen': { pad: '/view/sander-van-praet/3de-jaar/constructie', imgsFromKop: 'Een balk monteren in een muur van steen' },
};

for (const p of Object.values(gsData)) {
  if (INDEXPADEN.has(p.pad)) continue;
  if (p.pad.endsWith('/home') || p.pad.endsWith('/over-mij')) continue;
  const isSchool = p.pad.includes('/3de-jaar/') || p.pad.includes('/4de-jaar/');
  if (!isSchool) continue;

  const padDeel = p.pad.replace(/^\/view\/sander-van-praet\//, '');
  const laatsteSegment = padDeel.split('/').pop();
  const titel = titelHulp[laatsteSegment] ?? p.titel;
  const schooljaar = jaarUit(p.pad);
  const vak = vakUit(p.pad);
  const slugNaam = laatsteSegment; // zelfde pad-naam als op Google Sites

  // Omschrijving: eigen alinea's, anders die van de indexpagina.
  let alineas = p.inhoud.filter(b => b.k === 'p').map(b => b.text);
  if (alineas.length === 0) {
    alineas = (omsUitIndex[titelHulp[laatsteSegment]] ?? [])
      .filter(t => !/klik op de afbeelding/i.test(t));
  }
  const omschrijving = alineas.filter(Boolean).join('\n\n');

  // Mini's zonder eigen pagina halen hun afbeeldingen van de indexpagina:
  let bestanden = (p.afbeeldingen ?? []).map(a => a.bestand);
  if (miniUitIndex[titel] && bestanden.length === 0) {
    const assoc = gsData[miniUitIndex[titel].pad].associaties;
    const deel = assoc.delen.find(d => d.kop === miniUitIndex[titel].imgsFromKop);
    bestanden = deel ? deel.imgs.filter(Boolean) : [];
  }
  voegIn({ titel, slugNaam, schooljaar, vak, omschrijving, bestanden });
}

// ---------- 5. extra item: "Extra verlichte letters" uit de elektriciteitsindex ----------
{
  const ix = gsData['/view/sander-van-praet/3de-jaar/elektriciteit'];
  const teksten = ix.inhoud.filter(b => b.k === 'p').map(b => b.text);
  const i = teksten.findIndex(t => /Extra verlichte letters/.test(t));
  if (i >= 0) {
    voegIn({
      titel: 'Extra verlichte letters',
      slugNaam: 'extra-verlichte-letters',
      schooljaar: '3de jaar',
      vak: 'Elektriciteit',
      omschrijving: teksten.slice(i + 1).join('\n\n'),
      bestanden: [],
    });
  }
}

// ---------- 6. PovLocaties + Rapporten ----------
DB.prepare(`INSERT OR IGNORE INTO PovLocaties (Naam, Slug, Bedrijf, Adres, Periode, Omschrijving, IsGepubliceerd, Volgorde)
  VALUES ('POV Sint-Oda', 'pov-sint-oda', 'Sint-Oda', 'Overpelt', '2026 — heden', ?, 1, 2)`)
  .run('Praktijkervaring hier begonnen; de dagrapporten verschijnen zodra de periode een aanvang neemt.');
const erp = DB.prepare("SELECT Id FROM PovLocaties WHERE Slug = 'pov-erperheide'").get();
const rapporten = [
  { pad: '/uploads/rapporten/2026-09-08-VanPraet-Sander.pdf', datum: '2026-09-08', titel: 'Dagrapport 08-09-2026' },
  { pad: '/uploads/rapporten/2026-09-15-VanPraet-Sander.pdf', datum: '2026-09-15', titel: 'Dagrapport 15-09-2026' },
  { pad: '/uploads/rapporten/2026-09-22-VanPraet-Sander.pdf', datum: '2026-09-22', titel: 'Dagrapport 22-09-2026' },
  { pad: '/uploads/rapporten/2026-09-29-VanPraet-Sander.pdf', datum: '2026-09-29', titel: 'Dagrapport 29-09-2026' },
];
if (erp) {
  for (const r of rapporten) {
    if (!DB.prepare('SELECT 1 x FROM Rapporten WHERE Pad = ?').get(r.pad)) {
      DB.prepare(`INSERT INTO Rapporten (StageId, PovLocatieId, Pad, Titel, Datum, IsGepubliceerd, Volgorde)
        VALUES (NULL, ?, ?, ?, ?, 1, 0)`).run(erp.Id, r.pad, r.titel, r.datum);
      console.log('  ✓ rapport:', r.titel);
    }
  }
}

// ---------- 7. profiel synchroon ----------
{
  const overPag = gsData['/view/sander-van-praet/over-mij'];
  if (overPag) {
    const lange = overPag.inhoud.filter(b => b.k === 'p' && b.text.length > 60).map(b => b.text);
    if (lange.length > 0) {
      const al = lange.join('\n\n');
      DB.prepare(`UPDATE Profielen SET OverMijTekst = ?, BijgewerktOp = ? WHERE Id = (SELECT MIN(Id) FROM Profielen)`)
        .run(al, NU);
      console.log('  ✓ Profiel over-mij-tekst geüpdatet (' + al.length + ' tekens)');
    }
  }
  // startpagina-intro uit de home-page
  const home = gsData['/view/sander-van-praet/home'];
  if (home) {
    const homeTexten = home.inhoud.filter(b => b.k === 'p').map(b => b.text).filter(t => t.length > 30);
    if (homeTexten.length > 0) {
      console.log('  home-intro beschikbaar (' + homeTexten[0].slice(0, 60) + '…)');
    }
  }
}

// ---------- 8. rapport ----------
console.log('TOTAAL nieuwe projecten:', invoeg, '— in DB:',
  DB.prepare('SELECT COUNT(*) c FROM Projecten').get().c);
console.log(DB.prepare('SELECT Schooljaar, Vak, COUNT(*) c FROM Projecten GROUP BY Schooljaar, Vak ORDER BY Schooljaar, Vak').all());
DB.close();
