// volledige-extractor.mjs — netto extractor voor de gemiste Google-Sites-kindpagina's.
// 1. Haal elke (eventueel ontbrekende) pagina op met fetch.
// 2. Parse de HTML: blokken in documentvolgorde (koppen, alinea's, afbeeldingen, documentlinks).
// 3. Download elke afbeelding naar wwwroot/uploads/projecten/<bestand>.jpg via fetch (werkt: 200).
// 4. Verrijk gs-data.json met de pagina's die er nog niet in stonden.
import fs from 'fs';
import path from 'path';

const ROOT = path.resolve(import.meta.dirname);
const UPDIR = path.join(ROOT, 'wwwroot', 'uploads', 'projecten');
fs.mkdirSync(UPDIR, { recursive: true });
const GSPAD = path.join(ROOT, 'gs-data.json');
const gsData = JSON.parse(fs.readFileSync(GSPAD, 'utf8'));

const BASIS = 'https://sites.google.com';

// De 10 kindpagina's die de indexpagina's openen maar die in gs-data ontbraken.
const ONTBREKEND = [
  '/view/sander-van-praet/3de-jaar/elektriciteit/draadplooioefening',
  '/view/sander-van-praet/3de-jaar/elektriciteit/buisplooioefening',
  '/view/sander-van-praet/3de-jaar/elektriciteit/basis-schakeling-1',
  '/view/sander-van-praet/3de-jaar/elektriciteit/basisschakeling-2-2lampen-serie',
  '/view/sander-van-praet/3de-jaar/elektriciteit/2-lampen-parralel',
  '/view/sander-van-praet/3de-jaar/elektriciteit/dubbele-contactdoos',
  '/view/sander-van-praet/3de-jaar/elektriciteit/inbouw-enkelpolig-controle',
  '/view/sander-van-praet/3de-jaar/elektriciteit/inbouw-enkelpolig-verlicht',
  '/view/sander-van-praet/3de-jaar/elektriciteit/inbouw-dubbele-aansteking',
  '/view/sander-van-praet/3de-jaar/elektriciteit/inbouw-wissel',
];

function strip(htmlTekst) {
  return htmlTekst
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<\/(p|div|h\d)>/gi, '\n')
    .replace(/<[^>]+>/g, '')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&#39;|&apos;/g, "'")
    .replace(/&quot;/g, '"')
    .replace(/\r/g, '')
    .split('\n')
    .map(l => l.trim())
    .filter(Boolean)
    .join('\n')
    .trim();
}

// --- parser: blokken in documentvolgorde ---
function parseer(raw) {
  const inhoudStart = raw.indexOf('<h1');
  const inhoudEinde = raw.lastIndexOf('</body>');
  if (inhoudStart < 0) return null;
  const content = raw.slice(inhoudStart, inhoudEinde);

  const blokken = [];
  const re = /<(h\d)\b[^>]*>([\s\S]*?)<\/\1>|<p\b[^>]*class="[^"]*zfr3Q[^"]*"[^>]*>([\s\S]*?)<\/p>|<img[^>]*src="([^"]+)"[^>]*class="CENy8b"/g;
  let m;
  while ((m = re.exec(content)) !== null) {
    if (m[1]) {
      const t = strip(m[2]);
      if (t) blokken.push({ k: 'h', lvl: Number(m[1].slice(1)), text: t });
    } else if (m[3]) {
      const t = strip(m[3]);
      if (t) blokken.push({ k: 'p', lvl: 0, text: t });
    } else if (m[4]) {
      blokken.push({ k: 'img', src: m[4] });
    }
  }
  // Interne paginalinks (miniatuur-omlijning) in documentvolgorde teruggeven voor koppelingen:
  const links = [...content.matchAll(/href="(\/view\/sander-van-praet\/[^"]+)"/g)].map(m => m[1])
    .filter((v, i, a) => a.indexOf(v) === i);
  return { blokken, links };
}

// --- afbeelding downloaden ---
async function laad(src, bestand) {
  const doelpad = path.join(UPDIR, bestand);
  if (fs.existsSync(doelpad) && fs.statSync(doelpad).size > 5000) return bestand;
  try {
    const r = await fetch(src, {
      headers: { Referer: 'https://sites.google.com/view/sander-van-praet/', 'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)' },
    });
    if (!r.ok) { console.log('  ! afbeelding', r.status, bestand); return null; }
    const buf = Buffer.from(await r.arrayBuffer());
    fs.writeFileSync(doelpad, buf);
    return bestand;
  } catch (e) { console.log('  ! fout', bestand, e.message); return null; }
}

// Lazily geladen foto-viewer: de afbeeldings-URL's staan als JSON-blob in de HTML zelf.
function viewerUrls(raw) {
  const urls = [...raw.matchAll(/https:\/\/sites\.google\.com\/sitesv-images-rt\/[A-Za-z0-9_\-]+=w\d+/g)].map(m => m[0]);
  // Uniek maken, volgorde van voorkomen behouden:
  return [...new Set(urls)];
}

async function main() {
  let nieuwe = 0;
  for (const pad of ONTBREKEND) {
    if (gsData[pad]) { console.log('== bestaat al:', pad); continue; }
    const url = BASIS + pad;
    const r = await fetch(url, { headers: { 'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)' } });
    if (!r.ok) { console.log('! pagina', r.status, pad); continue; }
    const raw = await r.text();
    const parsed = parseer(raw);
    // Pagina's met alleen een lazy foto-viewer hebben geen volledig parser-resultaat;
    // titel komt dan uit het eerste h1-element.
    if (!parsed) { console.log('! leeg:', pad); continue; }

    // titel: eerste h1 op de pagina
    const h1 = parsed.blokken.find(b => b.k === 'h') ;
    const titel = h1 ? h1.text : path.basename(pad);

    // afbeeldingen: in volgorde genummerd op basis van de paginaslug
    const afbeeldingen = [];
    let nr = 1;
    for (const b of parsed.blokken) {
      if (b.k !== 'img') continue;
      const w = 1280;
      const bestand = path.basename(pad) + '-' + (nr++) + '.jpg';
      const ok = await laad(b.src, bestand);
      if (ok) {
        afbeeldingen.push({ src: b.src, bestand, w, h: null });
        b.bestand = bestand; // koppeling blok → bestand
      }
    }

    // Fallback: foto's uit de JS-blobs (lazy viewer) als er geen statische <img> was.
    if (afbeeldingen.length === 0) {
      let vn = 1;
      for (const u of viewerUrls(raw)) {
        const bestand = path.basename(pad) + '-' + (vn++) + '.jpg';
        const ok = await laad(u, bestand);
        if (ok) {
          afbeeldingen.push({ src: u, bestand, w: 1280, h: null });
        }
      }
    }

    gsData[pad] = {
      pad,
      titel,
      inhoud: parsed.blokken.filter(b => b.k !== 'img'),
      blokken: parsed.blokken,   // met img-blokken op de juiste plek in de volgorde
      afbeeldingen,
      links: parsed.links,
      extern: [],
    };
    nieuwe++;
    console.log('== opgehaald:', pad, '| blokken:', parsed.blokken.length, '| imgs:', afbeeldingen.length);
  }
  fs.writeFileSync(GSPAD, JSON.stringify(gsData, null, 1));
  console.log('TOTAAL nieuw:', nieuwe, '| pagina\'s in gs-data:', Object.keys(gsData).length);
}
main();
