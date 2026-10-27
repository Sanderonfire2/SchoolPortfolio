// index-associatie.mjs — koppelt op de twee 3de-jaar-indexpagina's elke afbeelding in documentvolgorde
// aan het juiste mini-item: ofwel aan de gelinkte kindpagina (miniatuur), ofwel aan de kop erboven.
// Resultaat: 'associaties' in gs-data.json per indexpagina.
import fs from 'fs';
import path from 'path';

const ROOT = path.resolve(import.meta.dirname);
const GSPAD = path.join(ROOT, 'gs-data.json');
const gsData = JSON.parse(fs.readFileSync(GSPAD, 'utf8'));

function strip(s) {
  return s
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<[^>]+>/g, '')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&#39;|&apos;/g, "'")
    .replace(/&quot;/g, '"')
    .trim();
}

// Walk door de content in documentvolgorde: kop / thumb-in-link / losse afbeelding.
function loop(pad, htmlBestand) {
  const raw = fs.readFileSync(htmlBestand, 'utf8');
  const i = raw.indexOf('<h1');
  const content = raw.slice(i, raw.lastIndexOf('</body>'));
  const afd = gsData[pad].afbeeldingen; // volgt zelfde DOM-volgorde

  // Eerst de thumb-blokken (link + daarna eerste img) markeren als bezet, met posities.
  // Positie = volgorde van het img-element binnen alle <img> van de pagina.
  // Simpel: loop regex over alles; teller imgPos verhoogt bij elke <img.
  const re = /<a href="(\/view\/sander-van-praet\/[^"]+)"[^>]*class="fqo2vd"[^>]*>[\s\S]{0,400}?<img[^>]*src="([^"]+)"|<(h\d)\b[^>]*>([\s\S]*?)<\/\3>|<img[^>]*src="([^"]+)"/g;
  let m;
  let imgPos = 0;
  const kopHist = [];
  const assoc = {
    kikan: [],                 // kindpagina-entries: { kind(netloc siste deel), afbeelding bestand }
    delen: [],                 // losse delen: { kop, tekst, imgs[] }
    huidig: null,
  };
  let huidigKopObj = null;
  while ((m = re.exec(content)) !== null) {
    if (m[1]) {
      // thumb-blok: gekoppeld aan de kindpagina; positie is de volgende img-teller.
      imgPos++;
      const bestand = afd[imgPos - 1]?.bestand ?? null;
      assoc.kikan.push({ kind: m[1], thumb: bestand });
      huidigKopObj = null; // na een kindthumbbucket is de afbeelding weg van de kop
    } else if (m[3]) {
      const t = strip(m[4]);
      if (!t) continue;
      huidigKopObj = { kop: t, tekst: '', imgs: [] };
      assoc.delen.push(huidigKopObj);
    } else if (m[5]) {
      imgPos++;
      const bestand = afd[imgPos - 1]?.bestand ?? null;
      if (huidigKopObj) huidigKopObj.imgs.push(bestand);
    }
  }
  return assoc;
}

for (const [pad, bestand] of [
  ['/view/sander-van-praet/3de-jaar/elektriciteit', 'idx-elek.html'],
  ['/view/sander-van-praet/3de-jaar/constructie', 'idx-constr.html'],
]) {
  const assoc = loop(pad, path.join(ROOT, bestand));
  gsData[pad].associaties = assoc;
  console.log('==', pad);
  console.log('kinderlinks:', assoc.kikan.length, assoc.kikan.map(k => k.kind.split('/').pop() + ' → ' + k.thumb).join(', '));
  console.log('delen:', assoc.delen.filter(d => d.kop && !/^(Elektriciteit 3de jaar\.|Constructie technieken|Projecten|Opdrachten 3EL)$/.test(d.kop)).length);
  for (const d of assoc.delen) console.log('   ·', d.kop, '→', d.imgs.length, 'imgs', d.imgs.join(','), d.tekst ? '' : '(geen tekst)');
}
fs.writeFileSync(GSPAD, JSON.stringify(gsData, null, 1));
console.log('gs-data.json bijgewerkt met associaties');
