/**
 * Post-process PlantUML swimlane SVG → khung StarUML (header ô đóng + viền ngoài).
 * Đẩy nội dung diagram xuống để header không đè initial/activity.
 *
 * Usage:
 *   node docs/chatbox/srs/frame-svg.mjs path/to/diagram.svg
 *   node docs/chatbox/srs/frame-svg.mjs path.svg --lanes "Admin,Hệ thống"
 */
import fs from 'node:fs';
import path from 'node:path';

const args = process.argv.slice(2);
const svgArg = args.find((a) => !a.startsWith('--') && a.endsWith('.svg'));
const lanesFlag = args.find((a) => a.startsWith('--lanes='));
const lanesFlagIdx = args.indexOf('--lanes');
const lanesFromFlag = lanesFlag
  ? lanesFlag.slice('--lanes='.length)
  : lanesFlagIdx >= 0
    ? args[lanesFlagIdx + 1]
    : null;

const svgPath = path.resolve(
  svgArg || 'docs/chatbox/srs/chatbox-user-flow-swimlane.svg'
);

let svg = fs.readFileSync(svgPath, 'utf8');

// Remove previous frame + previous shift wrapper (re-render PlantUML first for clean SVG)
svg = svg.replace(/\n?<!-- StarUML-like closed frame[\s\S]*?<\/g>\n?/g, '');
svg = svg.replace(/<g id="plantuml-shifted"[^>]*>\s*/g, '');

const vbMatch = svg.match(/viewBox="([^"]+)"/i);
if (!vbMatch) {
  console.error('No viewBox');
  process.exit(1);
}
const [vx, vy, Vw, Vh] = vbMatch[1].split(/\s+/).map(Number);

const DEFAULT_LANES = [
  'Người dùng',
  'Giao diện Chat',
  'API Chat',
  'RAG / AI',
  'Admin',
  'Hệ thống',
];

// --- Parse texts ---
const texts = [];
const textRe = /<text\b([^>]*)>([^<]*)<\/text>/gi;
let m;
while ((m = textRe.exec(svg))) {
  const attrs = m[1];
  const content = m[2]
    .replace(/&amp;/g, '&')
    .replace(/&#(\d+);/g, (_, n) => String.fromCharCode(+n));
  const xm = attrs.match(/\bx="([\d.]+)"/);
  const ym = attrs.match(/\by="([\d.]+)"/);
  if (!xm || !ym) continue;
  texts.push({ x: +xm[1], y: +ym[1], content, full: m[0] });
}

// Diagram title: first large title-like text near top
const titleText =
  texts.find((t) => t.y < 55 && t.content.length > 8) ||
  texts.find((t) => /^(A\.|B\.|C\.|D\.|Luồng)/i.test(t.content));

// Resolve expected lane title list
let expectedTitles = lanesFromFlag
  ? lanesFromFlag.split(',').map((s) => s.trim()).filter(Boolean)
  : null;

// Lane titles near top
let lanes = [];
if (expectedTitles?.length) {
  for (const title of expectedTitles) {
    const hit = texts.find(
      (t) =>
        t.y < 160 &&
        (t.content === title ||
          t.content.includes(title) ||
          (title.length > 4 && t.content.startsWith(title.slice(0, 6))))
    );
    if (hit) {
      lanes.push({ name: title, x: hit.x, y: hit.y, full: hit.full });
    } else {
      // Title not drawn (single-lane or missing) — synthesize from vertical layout later
      lanes.push({ name: title, x: null, y: 76, full: null, synthetic: true });
    }
  }
} else {
  // Auto: known defaults first
  for (const title of DEFAULT_LANES) {
    const hit = texts.find(
      (t) =>
        t.y < 160 &&
        (t.content === title ||
          t.content.includes(title) ||
          (title.startsWith('Giao diện') && /Giao diện/i.test(t.content)))
    );
    if (hit && !lanes.some((l) => l.full === hit.full)) {
      lanes.push({ name: hit.content, x: hit.x, y: hit.y, full: hit.full });
    }
  }
  // Fallback: cluster short texts at same y band as swimlane headers (~70-90)
  if (lanes.length < 1) {
    const band = texts.filter(
      (t) =>
        t.y >= 60 &&
        t.y <= 100 &&
        t.content.length <= 24 &&
        !/^(A\.|B\.|C\.|D\.)/.test(t.content)
    );
    band.sort((a, b) => a.x - b.x);
    for (const t of band) {
      if (!lanes.some((l) => Math.abs(l.x - t.x) < 20)) {
        lanes.push({ name: t.content, x: t.x, y: t.y, full: t.full });
      }
    }
  }
}

lanes = lanes.filter((l) => l.x != null || l.synthetic);
lanes.sort((a, b) => (a.x ?? 0) - (b.x ?? 0));

// Assign x for synthetic lanes from edges later
if (lanes.length < 1) {
  console.error('Lane titles not found near top. Top texts:');
  console.error(
    texts
      .filter((t) => t.y < 160)
      .map((t) => `${t.y.toFixed(0)} "${t.content}"`)
      .join('\n')
  );
  process.exit(1);
}

// Long vertical lines → column edges
const vBuckets = new Map();
const lineRe =
  /<line\b[^>]*\bx1="([\d.]+)"[^>]*\by1="([\d.]+)"[^>]*\bx2="([\d.]+)"[^>]*\by2="([\d.]+)"[^>]*\/?>/gi;
while ((m = lineRe.exec(svg))) {
  const x1 = +m[1];
  const y1 = +m[2];
  const x2 = +m[3];
  const y2 = +m[4];
  if (Math.abs(x1 - x2) >= 0.8) continue;
  const len = Math.abs(y2 - y1);
  if (len < Vh * 0.45) continue;
  const x = Math.round(x1 * 2) / 2;
  let key = x;
  for (const k of vBuckets.keys()) {
    if (Math.abs(k - x) < 4) {
      key = k;
      break;
    }
  }
  const arr = vBuckets.get(key) || [];
  arr.push({ x, y1: Math.min(y1, y2), y2: Math.max(y1, y2), len });
  vBuckets.set(key, arr);
}

let edges = [...vBuckets.entries()]
  .map(([x, arr]) => ({
    x,
    maxLen: Math.max(...arr.map((a) => a.len)),
    maxY: Math.max(...arr.map((a) => a.y2)),
  }))
  .filter((b) => b.maxLen > Vh * 0.5)
  .sort((a, b) => a.x - b.x)
  .map((b) => b.x);

const laneX = (i) =>
  lanes[i].x ?? (edges[i] != null && edges[i + 1] != null
    ? (edges[i] + edges[i + 1]) / 2
    : vx + (Vw * (i + 0.5)) / Math.max(lanes.length, 1));

if (edges.length > lanes.length + 1) {
  const outerL = edges[0];
  const outerR = edges[edges.length - 1];
  const picked = [outerL];
  for (let i = 0; i < lanes.length - 1; i++) {
    const mid = (laneX(i) + laneX(i + 1)) / 2;
    let best = edges[1] ?? outerR;
    let bestD = Infinity;
    for (const e of edges) {
      if (e === outerL || e === outerR) continue;
      const d = Math.abs(e - mid);
      if (d < bestD) {
        bestD = d;
        best = e;
      }
    }
    if (!picked.includes(best)) picked.push(best);
  }
  if (!picked.includes(outerR)) picked.push(outerR);
  edges = picked.sort((a, b) => a - b);
}

while (edges.length > lanes.length + 1) {
  let drop = 1;
  let score = -1;
  for (let i = 1; i < edges.length - 1; i++) {
    let minD = Infinity;
    for (let j = 0; j < lanes.length - 1; j++) {
      minD = Math.min(minD, Math.abs(edges[i] - (laneX(j) + laneX(j + 1)) / 2));
    }
    if (minD > score) {
      score = minD;
      drop = i;
    }
  }
  edges.splice(drop, 1);
}

if (edges.length < 2) {
  edges = [vx + 12, vx + Vw - 12];
}

if (edges.length < lanes.length + 1) {
  const left = edges[0];
  const right = edges[edges.length - 1];
  edges = [left];
  for (let i = 0; i < lanes.length - 1; i++) {
    const xa = lanes[i].x;
    const xb = lanes[i + 1].x;
    edges.push(
      xa != null && xb != null
        ? (xa + xb) / 2
        : left + ((right - left) * (i + 1)) / lanes.length
    );
  }
  edges.push(right);
}

// Fill synthetic lane x from column midpoints
for (let i = 0; i < lanes.length; i++) {
  if (lanes[i].x == null && edges[i] != null && edges[i + 1] != null) {
    lanes[i].x = (edges[i] + edges[i + 1]) / 2;
  }
}

// originalHeaderY needs a number
const headerBandY = Math.min(
  ...lanes.map((l) => l.y).filter((y) => y != null),
  76
);

// --- Layout: title stays; header band; then content shifted down ---
const HEADER_H = 42;
const GAP_BELOW_HEADER = 14; // clear space so initial is not under header
const titleBottom = titleText ? titleText.y + 10 : vy + 28;

// Header sits under diagram title
const headerTop = titleBottom + 6;
const headerBottom = headerTop + HEADER_H;

// Original plantuml lane titles sit ~ y 70-90; start node right under them.
// We hide titles and push ALL plantuml content down so start is below header.
const originalHeaderY = headerBandY;
// Shift so that original content that was at originalHeaderY now sits at headerBottom + GAP
const shiftY = headerBottom + GAP_BELOW_HEADER - (originalHeaderY - 18);

const frameLeft = edges[0];
const frameRight = edges[edges.length - 1];
const contentBottom =
  Math.max(
    ...[...vBuckets.values()].flat().map((a) => a.y2),
    vy + Vh - 8
  ) + shiftY;
const frameBottom = contentBottom + 8;

// Hide original lane title texts
for (const lane of lanes) {
  if (lane.full) svg = svg.replace(lane.full, '');
}

// Expand viewBox / size for shift + frame
const newH = Math.ceil(Math.max(Vh + shiftY + 20, frameBottom - vy + 16));
svg = svg.replace(
  /viewBox="[^"]+"/i,
  `viewBox="${vx} ${vy} ${Vw} ${newH}"`
);
svg = svg.replace(
  /(\s)height="[^"]+"/i,
  `$1height="${newH}px"`
);
svg = svg.replace(
  /style="width:([^;]+);height:[^"]+"/i,
  `style="width:$1;height:${newH}px;background:#FFFFFF;"`
);

// Build frame (absolute coords — not shifted)
const cells = [];
for (let i = 0; i < edges.length - 1; i++) {
  cells.push({
    x1: edges[i],
    x2: edges[i + 1],
    title: lanes[i]?.name || LANE_TITLES[i] || `Lane ${i + 1}`,
  });
}

const frameParts = [];
frameParts.push('<!-- StarUML-like closed frame (post-process) -->');
frameParts.push(
  '<g id="staruml-frame" font-family="Arial, Helvetica, sans-serif">'
);
frameParts.push(
  `<rect x="${frameLeft}" y="${headerTop}" width="${frameRight - frameLeft}" height="${frameBottom - headerTop}" fill="none" stroke="#000000" stroke-width="2.25"/>`
);
for (const c of cells) {
  frameParts.push(
    `<rect x="${c.x1}" y="${headerTop}" width="${c.x2 - c.x1}" height="${HEADER_H}" fill="#FFFFFF" stroke="#000000" stroke-width="1.75"/>`
  );
  const cx = (c.x1 + c.x2) / 2;
  const ty = headerTop + HEADER_H / 2 + 5;
  frameParts.push(
    `<text x="${cx}" y="${ty}" text-anchor="middle" fill="#000000" font-size="14" font-weight="700">${c.title.replace(/&/g, '&amp;')}</text>`
  );
}
frameParts.push(
  `<line x1="${frameLeft}" y1="${headerBottom}" x2="${frameRight}" y2="${headerBottom}" stroke="#000000" stroke-width="2"/>`
);
for (let i = 1; i < edges.length - 1; i++) {
  frameParts.push(
    `<line x1="${edges[i]}" y1="${headerBottom}" x2="${edges[i]}" y2="${frameBottom}" stroke="#000000" stroke-width="1.5"/>`
  );
}
frameParts.push('</g>');

// Wrap plantuml content in translate so header never covers initial/nodes
const wrapOpen = `<g id="plantuml-shifted" transform="translate(0,${shiftY.toFixed(2)})">`;
if (/<defs\s*\/>/i.test(svg)) {
  svg = svg.replace(/<defs\s*\/>/i, `<defs/>${wrapOpen}`);
} else if (/<\/defs>/i.test(svg)) {
  svg = svg.replace(/<\/defs>/i, `</defs>${wrapOpen}`);
} else {
  svg = svg.replace(/(<svg[^>]*>)/i, `$1${wrapOpen}`);
}

// Close shift group + append frame before </svg>
svg = svg.replace(
  /<\/svg>\s*$/i,
  `</g>\n${frameParts.join('\n')}\n</svg>`
);

// Final (stop) nodes: match chatbox style — outer ring + filled dark center
// (PlantUML often renders stop inner as fill="#FFF"; chatbox uses fill="#222")
svg = recolorStopNodesLikeChatbox(svg);
// Decision (amber) vs merge (blue-gray) diamonds
svg = recolorDecisionAndMergeDiamonds(svg);

fs.writeFileSync(svgPath, svg);

console.log('✅ Framed (no overlap):', svgPath);
console.log('   cells:', cells.map((c) => c.title).join(' | '));
console.log(
  '   shiftY:',
  shiftY.toFixed(1),
  'header:',
  headerTop.toFixed(1),
  '→',
  headerBottom.toFixed(1)
);
console.log('   new height:', newH);

/**
 * UML activity final = concentric ellipses. Chatbox SVG:
 *   outer: fill="none" stroke="#222"
 *   inner: fill="#222" stroke="#222"
 * Initial stays solid fill="#000".
 */
function recolorStopNodesLikeChatbox(svgText) {
  const ellipses = [];
  const re =
    /<ellipse\b([^>]*)\/>|<ellipse\b([^>]*)><\/ellipse>/gi;
  let em;
  while ((em = re.exec(svgText))) {
    const attrs = em[1] || em[2] || '';
    const cx = +(attrs.match(/\bcx="([\d.]+)"/) || [])[1];
    const cy = +(attrs.match(/\bcy="([\d.]+)"/) || [])[1];
    const rx = +(attrs.match(/\brx="([\d.]+)"/) || [])[1];
    if (Number.isNaN(cx) || Number.isNaN(cy) || Number.isNaN(rx)) continue;
    ellipses.push({ full: em[0], attrs, cx, cy, rx, index: em.index });
  }

  // Group by center (rounded)
  const groups = new Map();
  for (const e of ellipses) {
    const key = `${Math.round(e.cx * 10) / 10},${Math.round(e.cy * 10) / 10}`;
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(e);
  }

  let out = svgText;
  for (const group of groups.values()) {
    if (group.length < 2) continue; // start node is single filled circle
    group.sort((a, b) => b.rx - a.rx);
    const outer = group[0];
    const inner = group[group.length - 1];
    if (outer.rx <= inner.rx) continue;

    const outerNew = outer.full
      .replace(/\bfill="[^"]*"/i, 'fill="none"')
      .replace(/stroke:#[0-9a-fA-F]+/g, 'stroke:#222')
      .replace(/\bstroke="[^"]*"/i, 'stroke="#222"');
    // ensure fill none
    let o = outerNew;
    if (!/\bfill=/i.test(o)) {
      o = o.replace('<ellipse', '<ellipse fill="none"');
    }

    let i = inner.full
      .replace(/\bfill="[^"]*"/i, 'fill="#222"')
      .replace(/stroke:#[0-9a-fA-F]+/g, 'stroke:#222')
      .replace(/\bstroke="[^"]*"/i, 'stroke="#222"');
    if (!/\bfill=/i.test(i)) {
      i = i.replace('<ellipse', '<ellipse fill="#222"');
    }
    // style="...fill:#FFF..." variants
    i = i.replace(/fill:#(?:FFF|fff|FFFFFF|ffffff)/g, 'fill:#222');
    o = o.replace(/fill:#(?:FFF|fff|FFFFFF|ffffff)/g, 'fill:none');

    out = out.replace(outer.full, o);
    out = out.replace(inner.full, i);
  }

  // Also normalize start node stroke like chatbox
  out = out.replace(
    /(<ellipse\b[^>]*\bfill="#000"[^>]*style="stroke:)#000/gi,
    '$1#222'
  );
  out = out.replace(
    /(<ellipse\b[^>]*\bfill="#000"[^>]*)\bstroke="#000"/gi,
    '$1stroke="#222"'
  );

  return out;
}

/**
 * PlantUML activity diamonds vs arrow-head polygons.
 * - Decision: 6+ vertices, wide hexagon (question node)
 * - Merge: small ~square diamond, 4 vertices, size ~20–36
 * - Arrow heads: tiny 4-pt chevrons (max side < 16) — leave alone
 */
function recolorDecisionAndMergeDiamonds(svgText) {
  const DECISION_FILL = '#FFF3E0';
  const DECISION_STROKE = '#EF6C00';
  const MERGE_FILL = '#E3F2FD';
  const MERGE_STROKE = '#1565C0';

  return svgText.replace(/<polygon\b([^>]*?)(\s*\/>|>)/gi, (full, attrs, end) => {
    const pm = attrs.match(/\bpoints="([^"]+)"/i);
    if (!pm) return full;

    const nums = pm[1]
      .trim()
      .split(/[\s,]+/)
      .map(Number)
      .filter((n) => !Number.isNaN(n));
    const nPts = Math.floor(nums.length / 2);
    if (nPts < 4) return full;

    let minX = Infinity;
    let maxX = -Infinity;
    let minY = Infinity;
    let maxY = -Infinity;
    for (let i = 0; i < nPts * 2; i += 2) {
      minX = Math.min(minX, nums[i]);
      maxX = Math.max(maxX, nums[i]);
      minY = Math.min(minY, nums[i + 1]);
      maxY = Math.max(maxY, nums[i + 1]);
    }
    const w = maxX - minX;
    const h = maxY - minY;
    if (w < 4 || h < 4) return full;

    // Arrow tip markers (PlantUML uses tiny polygons)
    if (Math.max(w, h) < 16) return full;

    // Decision: wide multi-point diamond with question text (often 6–7 pts closed)
    const isDecision =
      (nPts >= 6 && w >= 40 && h >= 14 && h <= 50) ||
      (nPts >= 6 && w >= 60 && h <= 60);

    // Merge: small diamond; PlantUML may list 4 pts or 5 (closed loop)
    const isMerge =
      (nPts === 4 || nPts === 5) &&
      w >= 16 &&
      h >= 16 &&
      w <= 40 &&
      h <= 40 &&
      Math.abs(w - h) <= 14;

    if (isDecision) {
      return paintPolygon(attrs, end, DECISION_FILL, DECISION_STROKE);
    }
    if (isMerge) {
      return paintPolygon(attrs, end, MERGE_FILL, MERGE_STROKE);
    }
    return full;
  });
}

function paintPolygon(attrs, end, fill, stroke) {
  let a = ' ' + attrs.trim();
  if (/\bfill="/i.test(a)) {
    a = a.replace(/\bfill="[^"]*"/i, `fill="${fill}"`);
  } else {
    a += ` fill="${fill}"`;
  }
  if (/\bstroke="/i.test(a)) {
    a = a.replace(/\bstroke="[^"]*"/i, `stroke="${stroke}"`);
  } else {
    a += ` stroke="${stroke}"`;
  }
  a = a.replace(/stroke:#[0-9a-fA-F]{3,8}/gi, `stroke:${stroke}`);
  a = a.replace(/fill:#[0-9a-fA-F]{3,8}/gi, `fill:${fill}`);
  const selfClose = /\/>/.test(end);
  return selfClose ? `<polygon${a}/>` : `<polygon${a}>`;
}
