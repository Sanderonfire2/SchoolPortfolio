// Importeert alle projecten van de Google Site in de portfolio-database — v4
// 1) Content opsassen (alleen echte alinea's)
// 2) Foto's downloaden naar wwwroot/uploads
// 3) Projecten + GallerijAfbeeldingen in app.db zetten (directe SQLite-SQL)
// 4) Profiel-info bijwerken vanuit de Google Site
import fs from 'fs';
import path from 'path';

console.log('IMPORT v4');

const ROOT = process.cwd();
const DB = path.join(ROOT, 'app.db');
const UPLOADS = path.join(ROOT, 'wwwroot', 'uploads');
fs.mkdirSync(UPLOADS, { recursive: true });

const RAAW = JSON.parse(fs.readFileSync('import-raw.json', 'utf8'));

// ---------- 1. Content opruimen ----------
// Nav-ruis herkennen: de content-alinea's zijn de LAATSTE fragmenten,
// vóór de afsluitende chroomfragmenten. Nav-items komen 2× voor (menu 2×).
function contentUitTekst(tekst, titel) {
  // alles met titel/herhaling is nav
  const navSet = new Set();
  // Korte nav-items:komen vaak voor
  const telling = {};
  for (const t of tekst) telling[t] = (telling[t] || 0) + 1;
  for (const t in telling) if (telling[t] >= 2) navSet.add(t);
  // Laatste tot de chroom: 'Google' / cookies voet以后的 alles weg
  const chromePat = /^(Google Sites|Report abuse|This site uses cookies|Learn more|Aantal bezoeken|Bekijk nu|Deze site gebruikt)/;
  // Content = laatste opeenvolgende blok van opruimbare tekst
  // We knippen: alles t/m het LAATSTE voorkomen van een item dat ≥2× voorkomt
  let laatsteNav = -1;
  for (let i = 0; i < tekst.length; i++) {
    if (navSet.has(tekst[i])) laatsteNav = i;
  }
  // Na laatste nav-item: echte content (incl. eventueel paginatitel-herhaling 1×)
  let stijlStukken = tekst.slice(laatsteNav + 1);
  // paginatitel-herhaling (broodkruimel) vóór de content weghalen
  while (stijlStukken.length && stijlStukken[0].toLowerCase().includes(titel.toLowerCase())) stijlStukken.shift();
  // chrome-tail weg
  while (stijlStukken.length && chromePat.test(stijlStukken[stijlStukken.length - 1])) stijlStukken.pop();
  // Duplicaten weg (opvolgende gelijke)
  stijlStukken = stijlStukken.filter((t, i) => i === 0 || t !== stijlStukken[i - 1]);
  return stijlStukken;
}

// ---------- 2. Foto's ----------
const downloadtCache = new Map();
async function download(url, doelpad) {
  if (downloadtCache.has(url)) return downloadtCache.get(url);
  try {
    const res = await fetch(url);
    if (!res.ok) { console.log('  IMG FAIL', res.status, url.slice(0, 60)); return null; }
    const buf = Buffer.from(await res.arrayBuffer());
    // Determine bestandsextensie — content-sniffen
    let ext = '.jpg';
    if (buf[0] === 0x89 && buf[1] === 0x50) ext = '.png';
    else if (buf.slice(0, 3).toString('ascii') === 'GIF') ext = '.gif';
    else if (buf.slice(8, 12).toString('ascii') === 'WEBP') ext = '.webp';
    const naam = 'import-' + Date.now() + '-' + Math.random().toString(36).slice(2, 8) + ext;
    const doelpadVol = path.join(UPLOADS, naam);
    fs.writeFileSync(doelpadVol, buf);
    const pub = '/uploads/' + naam;
    downloadtCache.set(url, pub);
    return pub;
  } catch (e) {
    console.log('  IMG ERR', String(e).slice(0, 60));
    return null;
  }
}

// ----------趣 3. Probeer de sqlite-bestanden direct via EF te omschrijven; dat vereist dotnet;
// we schrijven een JSON-middelbestand dat het C#-importcommando leest. ----------
const projectenVoorImport = [];

// Jaartab / rubriek bepalen
function categorieUitPad(pad) {
  const deel = pad.split('/').filter(Boolean);
  // /4de-jaar/elektriciteit/... → '4de jaar · Elektriciteit'
  if (deel.length >= 2) {
    const jaar = deel[0].replace(/-jaar$/, 'e jaar').replace(/^3de-e jaar$/, '3de jaar');
    const rubriek = deel[1][0].toUpperCase() + deel[1].slice(1).replace(/-/g, ' ');
    if (deel[1] !== 'over-mij') return `${jaar} · ${rubriek}`;
  }
  return null;
}

const rPackene = RAAW.filter(d => {
  const deel = d.pad.split('/').filter(Boolean);
  // Alleen echte projectdetailpagina's (dieper dan 2 niveaus)
  return deel.length >= 3 && !d.pad.startsWith('/home') && !d.pad.startsWith('/over-mij');
});

console.log(rPackene.length + ' projectpagina\'s gevonden');

for (const p of rPackene) {
  const alineas = contentUitTekst(p.tekst, p.titel);
  // Titel: laatste nav-item-blad is de eigenlijke titel; of de paginatitlel
  const zijnPad = p.pad.split('/').filter(Boolean);
  let origineel = p.titel;
  // Titel uit de教学内容 eerste fragment als die met de paginatitel-overeenkomt
  const netteTitel = p.titel.charAt(0).toUpperCase() + p.titel.slice(1);
  if (alineas.length === 0) {
    console.log('OVERSLAAN (geen content):', p.pad);
    continue;
  }
  const omschrijving = alineas.join('\n\n');
  const samenvat = alineas[0].slice(0, 180);

  projectenVoorImport.push({
    titel: netteTitel,
    slug: p.pad.split('/').filter(Boolean).slice(-2).join('-'), // bijv. werking-boormachine
    categorie: categorieUitPad(p.pad),
    periode: zijnPad[0].replace(/-jaar$/, 'de jaar'),
    omschrijving,
    korteSamenvatting: samenvat,
    gallerijUrls: p.imgs,
    bronPad: p.pad
  });
  console.log('-', netteTitel, ' alineas:', alineas.length, ' imgs:', p.imgs.length);
}

// ---------- 4. Over mij info ----------
const overMij = RAAW.find(x => x.pad === '/over-mij');
let overMijInhoud = '';
if (overMij) {
  const alineas = contentUitTekst(overMij.tekst, 'over mij');
  overMijInhoud = alineas.join('\n\n');
  console.log('Over-mij inhoud (' + overMijInhoud.length + ' kar):', overMijInhoud.slice(0, 200));
}

// ---------- 5. Home intro ----------
const home = RAAW.find(x => x.pad === '/home');
let homeIntro = '';
if (home) {
  homeIntro = contentUitTekst(home.tekst, 'home').join('\n\n');
  console.log('Home-inhoud (' + homeIntro.length + ' kar):', homeIntro.slice(0, 200));
}

fs.writeFileSync('import-ready.json', JSON.stringify({
  projecten: projectenVoorImport,
  overMij: overMijInhoud,
  homeIntro
}, null, 2));
console.log('import-ready.json weggeschreven. Nu de C#-importer draaien.');
