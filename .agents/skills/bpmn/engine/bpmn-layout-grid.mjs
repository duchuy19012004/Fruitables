// bpmn-layout-grid.mjs — Engine layout SWIMLANE DỌC tự viết (kiểm soát 100%).
// IR { process, lanes[], nodes[], flows[] } → BPMN 2.0 XML (semantic + BPMNDiagram).
//
// Vì sao tự viết: bpmn-auto-layout (bpmn-io) route đẹp nhưng KHÔNG có khái niệm swimlane
// (chỉ set size Lane 400×100, không partition node). ELK partition được nhưng loop route
// vọt lên đỉnh + không nén node cùng lane → xấu. Grid tự viết: lane = CỘT (x cố định),
// bước = HÀNG (y tăng theo rank longest-path), node canh giữa cột lane, forward edge đi
// thẳng/bẻ-L, back-edge (loop) đi ra MÉP PHẢI vòng — không cắt ngang giữa sơ đồ.
//
// Bố cục:
//   ┌─ pool (isHorizontal=false) ────────────────┐
//   │  Lane A   │  Lane B   │  Lane C   │  header trên đỉnh
//   │  ○        │           │           │  rank 0
//   │  ↓        │           │           │
//   │  [task] ──┼─→ ◇       │           │  rank 1
//   │           │  ↓ ↓      │           │
//   └───────────┴───────────┴───────────┘   flow chảy XUỐNG

const SIZE = { start: [36, 36], end: [36, 36], gateway: [50, 50], task: [130, 70] };
const dim = k => SIZE[k] || SIZE.task;

const ROW_H = 120;          // cao mỗi rank (band dọc)
const COL_W = 190;          // rộng mỗi cột con
const LANE_PAD_X = 30;      // đệm trái/phải trong lane
const HEADER_H = 30;        // dải header lane trên đỉnh
const POOL_PAD_TOP = 20;    // đệm trên node đầu (dưới header)
const POOL_PAD_BOT = 30;
const BACK_MARGIN = 34;     // khoảng đẩy back-edge ra mép phải lane

// ── 1. Rank theo longest-path (bỏ back-edge để không phá thứ tự dọc) ──
// Back-edge = cạnh trỏ ngược lên node có rank ≤ nguồn (loop). Phát hiện bằng DFS.
function computeRanks(ir) {
  const adj = {}, indeg = {};
  ir.nodes.forEach(n => { adj[n.id] = []; indeg[n.id] = 0; });
  ir.flows.forEach(f => { if (adj[f.src] && adj[f.tgt] != null) { adj[f.src].push(f.tgt); } });

  // DFS phát hiện back-edge (cạnh tới node đang trong stack đệ quy)
  const color = {};            // 0=chưa, 1=đang thăm, 2=xong
  const backEdges = new Set();
  ir.nodes.forEach(n => { color[n.id] = 0; });
  const dfs = u => {
    color[u] = 1;
    for (const v of adj[u]) {
      if (color[v] === 1) backEdges.add(u + '\0' + v);   // back-edge
      else if (color[v] === 0) dfs(v);
    }
    color[u] = 2;
  };
  const starts = ir.nodes.filter(n => n.kind === 'start').map(n => n.id);
  (starts.length ? starts : [ir.nodes[0]?.id]).forEach(s => { if (s && color[s] === 0) dfs(s); });
  ir.nodes.forEach(n => { if (color[n.id] === 0) dfs(n.id); });   // node rời

  // longest-path rank trên DAG (bỏ back-edge)
  const fwd = {};
  ir.nodes.forEach(n => { fwd[n.id] = []; });
  ir.flows.forEach(f => {
    if (!fwd[f.src] || fwd[f.tgt] == null) return;
    if (backEdges.has(f.src + '\0' + f.tgt)) return;
    fwd[f.src].push(f.tgt);
    indeg[f.tgt] = (indeg[f.tgt] || 0) + 1;
  });
  const rank = {};
  const q = ir.nodes.filter(n => (indeg[n.id] || 0) === 0).map(n => n.id);
  q.forEach(id => { rank[id] = 0; });
  const deg = { ...indeg };
  while (q.length) {
    const u = q.shift();
    for (const v of fwd[u]) {
      rank[v] = Math.max(rank[v] ?? 0, (rank[u] ?? 0) + 1);
      if (--deg[v] === 0) q.push(v);
    }
  }
  ir.nodes.forEach(n => { if (rank[n.id] == null) rank[n.id] = 0; });
  return { rank, backEdges };
}

// ── 2. Gán cột: node vào dải X của lane nó; cùng lane+rank → tách cột con ──
function assignColumns(ir, rank) {
  const laneOrder = ir.lanes.map(l => l.id);
  const laneIndex = {}; laneOrder.forEach((id, i) => { laneIndex[id] = i; });

  // đếm cột con tối đa mỗi lane (nhiều node cùng lane cùng rank)
  const perLaneRank = {};   // laneId -> rank -> [nodeId]
  for (const n of ir.nodes) {
    const l = n.lane ?? laneOrder[0];
    ((perLaneRank[l] ||= {})[rank[n.id]] ||= []).push(n.id);
  }
  const laneCols = {};      // laneId -> số cột con
  for (const l of laneOrder) {
    let mx = 1;
    for (const r in (perLaneRank[l] || {})) mx = Math.max(mx, perLaneRank[l][r].length);
    laneCols[l] = mx;
  }
  // x gốc mỗi lane
  const laneX0 = {}; let acc = 0;
  for (const l of laneOrder) {
    laneX0[l] = acc;
    acc += LANE_PAD_X * 2 + laneCols[l] * COL_W;
  }
  const laneW = {}; for (const l of laneOrder) laneW[l] = LANE_PAD_X * 2 + laneCols[l] * COL_W;

  // gán col-index trong lane cho từng node (theo thứ tự xuất hiện trong rank)
  const colOf = {};
  for (const l of laneOrder) for (const r in (perLaneRank[l] || {})) {
    perLaneRank[l][r].forEach((id, i) => { colOf[id] = i; });
  }
  return { laneOrder, laneX0, laneW, laneCols, colOf, laneIndexOf: laneIndex };
}

// ── 3. Toạ độ tâm mỗi node ──
function placeNodes(ir, rank, cols) {
  const { laneX0, laneCols, colOf, laneOrder } = cols;
  const pos = {};
  const maxRank = Math.max(0, ...ir.nodes.map(n => rank[n.id]));
  for (const n of ir.nodes) {
    const l = n.lane ?? laneOrder[0];
    const [w, h] = dim(n.kind);
    const nCols = laneCols[l];
    // canh giữa: nếu lane 1 cột → giữa lane; nhiều cột → theo colOf
    const ci = colOf[n.id] || 0;
    const laneInnerW = nCols * COL_W;
    const colCenter = laneX0[l] + LANE_PAD_X + (ci + 0.5) * COL_W
      + (nCols === 1 ? 0 : 0);
    // nếu lane chỉ có node lẻ ở rank → vẫn canh giữa toàn lane cho đẹp
    const cx = nCols === 1 ? laneX0[l] + LANE_PAD_X + laneInnerW / 2 : colCenter;
    const cy = HEADER_H + POOL_PAD_TOP + rank[n.id] * ROW_H + ROW_H / 2;
    pos[n.id] = { x: Math.round(cx - w / 2), y: Math.round(cy - h / 2), w, h, cx: Math.round(cx), cy: Math.round(cy) };
  }
  return { pos, maxRank };
}

// ── 4. Routing ──
// Forward: từ đáy source xuống đỉnh target; khác cột → bẻ L tại giữa 2 rank.
// Back-edge (loop): ra cạnh PHẢI source → chạy dọc mép phải lane → vào cạnh phải target.
const esc = s => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

function anchor(p, side) {
  if (side === 'bottom') return [p.cx, p.y + p.h];
  if (side === 'top') return [p.cx, p.y];
  if (side === 'right') return [p.x + p.w, p.cy];
  if (side === 'left') return [p.x, p.cy];
}

// Đẩy toạ độ x của 1 đoạn DỌC [yTop..yBot] ra tới khi không cắt node nào (trừ src/tgt).
// dir=+1 đẩy sang phải, -1 sang trái. Trả x an toàn.
function clearVX(x, yTop, yBot, dir, boxes, skip) {
  let safe = x;
  for (let iter = 0; iter < 40; iter++) {
    let hit = null;
    for (const bx of boxes) {
      if (skip.has(bx.id)) continue;
      const yOv = Math.max(0, Math.min(yBot, bx.y + bx.h) - Math.max(yTop, bx.y));
      if (yOv > 2 && safe > bx.x - 8 && safe < bx.x + bx.w + 8) { hit = bx; break; }
    }
    if (!hit) return safe;
    safe = dir >= 0 ? hit.x + hit.w + 20 : hit.x - 20;
  }
  return safe;
}

// Forward edge. srcSide = cạnh ra khỏi source ('bottom' mặc định; gateway fan-out có thể
// ra 'left'/'right' để 2 nhánh không chồng đáy). Vào target luôn từ 'top'. boxes = node
// bounds để đoạn dọc ra cạnh né node nằm giữa (không cắt thân task).
function routeForward(sp, tp, srcSide = 'bottom', tgtDx = 0, boxes = [], skip = new Set()) {
  const b0 = anchor(tp, 'top');
  const b = [b0[0] + tgtDx, b0[1]];   // fan-in: lệch điểm cắm top để nhiều edge vào không chồng
  if (srcSide === 'bottom') {
    const a = anchor(sp, 'bottom');
    if (Math.abs(a[0] - b[0]) < 2) return [a, b];         // thẳng cột
    const midY = Math.round((a[1] + b[1]) / 2);           // bẻ L giữa 2 rank
    return dedupe([a, [a[0], midY], [b[0], midY], b]);
  }
  // ra cạnh trái/phải gateway → đi ngang rồi xuống cột target rồi vào top; đoạn dọc né node
  const a = anchor(sp, srcSide);
  const dir = srcSide === 'right' ? 1 : -1;
  let outX = a[0] + dir * 20;
  outX = clearVX(outX, Math.min(a[1], b[1] - 20), Math.max(a[1], b[1] - 20), dir, boxes, skip);
  return dedupe([a, [outX, a[1]], [outX, b[1] - 20], [b[0], b[1] - 20], b]);
}

// Back-edge (loop): ra cạnh PHẢI source → dọc lên hành lang ngang phía TRÊN target →
// cắm vào cạnh TRÊN target. xOut chỉ lấn ra ngay bên phải source (KHÔNG ra mép pool):
// nằm giữa mép phải source và mép trái target nếu target ở bên phải; nếu target ở bên
// TRÁI source (loop về lane trước) thì xOut = ngay sát phải source, đường đi thẳng lên
// rồi ngang sang trái tới trên target — ngắn nhất có thể. Mỗi làn lệch 1 bậc.
function routeBack(sp, tp, corridorY, lane, tgtDx = 0) {
  const a = anchor(sp, 'right');
  const top0 = anchor(tp, 'top');
  const top = [top0[0] + tgtDx, top0[1]];   // fan-in lệch điểm cắm top
  const gapRight = sp.x + sp.w + BACK_MARGIN + lane * 18;   // ngay bên phải source
  // nếu target ở bên phải source, có thể dùng khoảng giữa 2 node; nếu bên trái, sát source
  const between = tp.x > sp.x + sp.w ? Math.round((sp.x + sp.w + tp.x) / 2) : gapRight;
  const xOut = tp.x > sp.x + sp.w ? Math.min(between, gapRight) : gapRight;
  const y1 = corridorY - lane * 16;                         // hành lang ngang riêng mỗi làn
  return dedupe([a, [xOut, a[1]], [xOut, y1], [top[0], y1], top]);
}
const dedupe = pts => pts.filter((p, i, a) => i === 0 || p[0] !== a[i - 1][0] || p[1] !== a[i - 1][1]);

// ── 5. Đặt nhãn nhánh gateway: gần ĐẦU đường (nơi rẽ), lệch sang bên, né node + nhãn khác.
// Không đặt midpoint (dễ đè gateway/node kế). Ưu tiên đoạn đầu tiên đủ dài; điểm neo cách
// source ~30px để tránh đè tên gateway hiển thị ngay dưới shape.
function edgeLabel(pts, name, placed, nodeBoxes, isBack) {
  const w = Math.min(Math.max(String(name).length * 6.2, 24), 160), h = 14;
  let anchorPt;
  if (isBack) {
    // back-edge: đặt nhãn trên đoạn NGANG dài nhất (hành lang trên cùng) — luôn trong pool,
    // không trôi ra mép phải như khi đặt gần source.
    let bestLen = -1, mid = pts[Math.floor(pts.length / 2)] || pts[0];
    for (let k = 0; k < pts.length - 1; k++) {
      const horiz = Math.abs(pts[k][1] - pts[k + 1][1]) < 2;
      const len = Math.abs(pts[k][0] - pts[k + 1][0]);
      if (horiz && len > bestLen) { bestLen = len; mid = [(pts[k][0] + pts[k + 1][0]) / 2, pts[k][1]]; }
    }
    anchorPt = mid;
  } else {
    // forward: gần ĐẦU đường (nơi rẽ), cách source ~30px (nhảy qua vùng tên gateway/node)
    const a0 = pts[0];
    anchorPt = pts[1] || pts[0];
    const seg0Len = Math.hypot((pts[1] || a0)[0] - a0[0], (pts[1] || a0)[1] - a0[1]);
    if (seg0Len > 40) {
      const t = 30 / seg0Len;
      anchorPt = [a0[0] + (pts[1][0] - a0[0]) * t, a0[1] + (pts[1][1] - a0[1]) * t];
    }
  }
  const [mx, my] = anchorPt;
  // candidate: bên phải, bên trái, trên, dưới — xa dần
  const cands = [[mx + 8, my - h / 2], [mx - w - 8, my - h / 2],
                 [mx + 8, my - h - 6], [mx - w - 8, my - h - 6],
                 [mx + 8, my + 6], [mx - w - 8, my + 6],
                 [mx - w / 2, my - h - 8], [mx - w / 2, my + 8],
                 [mx + 16, my - h / 2], [mx - w - 16, my - h / 2]];
  const hit = (x, y, q) => x < q.x + q.w && x + w > q.x && y < q.y + q.h && y + h > q.y;
  const bad = (x, y) => placed.some(q => hit(x, y, q)) || nodeBoxes.some(q => hit(x, y, q));
  for (const [x, y] of cands) if (!bad(x, y)) { placed.push({ x, y, w, h }); return { x, y, w, h }; }
  // không có chỗ trống → chọn cái đè ít nhất
  let best = cands[0], bestA = Infinity;
  for (const [x, y] of cands) {
    let a = 0;
    for (const q of [...placed, ...nodeBoxes]) {
      const ox = Math.max(0, Math.min(x + w, q.x + q.w) - Math.max(x, q.x));
      const oy = Math.max(0, Math.min(y + h, q.y + q.h) - Math.max(y, q.y));
      a += ox * oy;
    }
    if (a < bestA) { bestA = a; best = [x, y]; }
  }
  placed.push({ x: best[0], y: best[1], w, h }); return { x: best[0], y: best[1], w, h };
}

const bpmnTag = k => ({ start: 'startEvent', end: 'endEvent', gateway: 'exclusiveGateway', task: 'task' }[k] || 'task');

// ── Post-routing: khử overlap segment giữa các edge (port từ bpmn-layout-elk.mjs) ──
// Gom segment cùng phương/cùng trục thành group → cấp track (dịch song song). Segment giữa
// dịch nguyên đoạn; đầu/cuối dùng dogleg giữ anchor cắm đúng biên node. Xử 2 nhánh cùng
// chạy 1 hành lang (fan-in cross-lane) + back-edge song song.
const OVERLAP_TRACK = 14, OVERLAP_MIN = 16, ENDPOINT_STUB = 10, GEPS = 0.5;
const gOvLen = (a1, a2, b1, b2) => Math.max(0, Math.min(Math.max(a1, a2), Math.max(b1, b2)) - Math.max(Math.min(a1, a2), Math.min(b1, b2)));
const gTrackOffset = slot => { if (slot === 0) return 0; const step = Math.ceil(slot / 2); return (slot % 2 ? -1 : 1) * step * OVERLAP_TRACK; };
const gSegKey = s => `${s.edgeId}:${s.i}`;

function gCollectSegments(edgeWps) {
  const refs = [];
  for (const [edgeId, pts] of Object.entries(edgeWps)) {
    for (let i = 0; i < pts.length - 1; i++) {
      const a = pts[i], b = pts[i + 1];
      const horiz = Math.abs(a[1] - b[1]) < GEPS, vert = Math.abs(a[0] - b[0]) < GEPS;
      if (!horiz && !vert) continue;
      if (Math.hypot(a[0] - b[0], a[1] - b[1]) < GEPS) continue;
      const p1 = horiz ? a[0] : a[1], p2 = horiz ? b[0] : b[1];
      refs.push({ edgeId, i, orient: horiz ? 'h' : 'v', axisKey: Math.round(horiz ? a[1] : a[0]),
        from: Math.min(p1, p2), to: Math.max(p1, p2),
        endpoint: i === 0 ? 'start' : (i === pts.length - 2 ? 'end' : 'mid') });
    }
  }
  return refs;
}
function gOverlapGroups(edgeWps) {
  const byLine = new Map();
  for (const s of gCollectSegments(edgeWps)) { const k = `${s.orient}:${s.axisKey}`; (byLine.get(k) || byLine.set(k, []).get(k)).push(s); }
  const groups = [];
  for (const line of byLine.values()) {
    const n = line.length; if (n < 2) continue;
    const parent = Array.from({ length: n }, (_, i) => i);
    const find = i => parent[i] === i ? i : (parent[i] = find(parent[i]));
    for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) {
      if (line[i].edgeId === line[j].edgeId) continue;
      if (gOvLen(line[i].from, line[i].to, line[j].from, line[j].to) > OVERLAP_MIN) { const ra = find(i), rb = find(j); if (ra !== rb) parent[rb] = ra; }
    }
    const buckets = new Map();
    for (let i = 0; i < n; i++) { const r = find(i); (buckets.get(r) || buckets.set(r, []).get(r)).push(line[i]); }
    for (const g of buckets.values()) if (new Set(g.map(s => s.edgeId)).size > 1) groups.push(g);
  }
  return groups;
}
function gAssignTrackOffsets(groups) {
  const offsets = new Map();
  for (const group of groups) {
    [...group].sort((a, b) => a.from - b.from || a.to - b.to || a.edgeId.localeCompare(b.edgeId) || a.i - b.i)
      .forEach((s, slot) => { const off = gTrackOffset(slot); if (!off) return; const key = gSegKey(s); const prev = offsets.get(key); if (prev == null || Math.abs(off) > Math.abs(prev.offset)) offsets.set(key, { ...s, offset: off }); });
  }
  return [...offsets.values()];
}
function gShiftMid(pts, i, orient, offset) { if (orient === 'h') { pts[i][1] += offset; pts[i + 1][1] += offset; } else { pts[i][0] += offset; pts[i + 1][0] += offset; } }
function gDogleg(pts, i, orient, offset) {
  if (!offset) return;
  const a = pts[i], b = pts[i + 1];
  if (i === 0) {
    if (orient === 'h') { const dir = Math.sign(b[0] - a[0]) || 1; const sl = Math.min(ENDPOINT_STUB, Math.max(4, Math.abs(b[0] - a[0]) / 2)); const stub = [a[0] + dir * sl, a[1]], jog = [stub[0], a[1] + offset]; b[1] += offset; pts.splice(1, 0, stub, jog); }
    else { const dir = Math.sign(b[1] - a[1]) || 1; const sl = Math.min(ENDPOINT_STUB, Math.max(4, Math.abs(b[1] - a[1]) / 2)); const stub = [a[0], a[1] + dir * sl], jog = [a[0] + offset, stub[1]]; b[0] += offset; pts.splice(1, 0, stub, jog); }
  } else {
    if (orient === 'h') { const dir = Math.sign(a[0] - b[0]) || -1; const sl = Math.min(ENDPOINT_STUB, Math.max(4, Math.abs(b[0] - a[0]) / 2)); const stub = [b[0] + dir * sl, b[1]], jog = [stub[0], b[1] + offset]; a[1] += offset; pts.splice(i + 1, 0, jog, stub); }
    else { const dir = Math.sign(a[1] - b[1]) || -1; const sl = Math.min(ENDPOINT_STUB, Math.max(4, Math.abs(b[1] - a[1]) / 2)); const stub = [b[0], b[1] + dir * sl], jog = [b[0] + offset, stub[1]]; a[0] += offset; pts.splice(i + 1, 0, jog, stub); }
  }
}
function gApplyTrack(edgeWps, s) { const pts = edgeWps[s.edgeId]; if (!pts || s.i >= pts.length - 1) return; if (s.endpoint === 'mid') gShiftMid(pts, s.i, s.orient, s.offset); else gDogleg(pts, s.i, s.orient, s.offset); }
function gNormalize(pts) {
  for (let i = pts.length - 2; i >= 0; i--) if (Math.abs(pts[i][0] - pts[i + 1][0]) < GEPS && Math.abs(pts[i][1] - pts[i + 1][1]) < GEPS) pts.splice(i + 1, 1);
  for (let i = 1; i < pts.length - 1; i++) { const a = pts[i - 1], b = pts[i], c = pts[i + 1]; if ((Math.abs(a[1] - b[1]) < GEPS && Math.abs(b[1] - c[1]) < GEPS) || (Math.abs(a[0] - b[0]) < GEPS && Math.abs(b[0] - c[0]) < GEPS)) { pts.splice(i, 1); i--; } }
}
function deOverlapEdges(edgeWps) {
  for (let pass = 0; pass < 6; pass++) {
    const moves = gAssignTrackOffsets(gOverlapGroups(edgeWps));
    if (!moves.length) break;
    moves.sort((a, b) => a.edgeId.localeCompare(b.edgeId) || b.i - a.i).forEach(m => gApplyTrack(edgeWps, m));
    Object.values(edgeWps).forEach(gNormalize);
  }
}

// ── API chính ──
export async function layoutToBpmn(ir, opts = {}) {
  const lanes = ir.lanes && ir.lanes.length ? ir.lanes : [{ id: '_L', name: ir.process.title || '' }];
  // đảm bảo mọi node có lane hợp lệ
  const laneIds = new Set(lanes.map(l => l.id));
  ir.nodes.forEach(n => { if (!laneIds.has(n.lane)) n.lane = lanes[0].id; });

  const irL = { ...ir, lanes };
  const { rank, backEdges } = computeRanks(irL);
  const cols = assignColumns(irL, rank);
  const { pos, maxRank } = placeNodes(irL, rank, cols);
  const allNodeBoxes = irL.nodes.map(n => ({ id: n.id, x: pos[n.id].x, y: pos[n.id].y, w: pos[n.id].w, h: pos[n.id].h }));

  // pool bounds
  const poolLeft = 0;
  const poolRight = Object.values(cols.laneW).reduce((a, b) => a + b, 0);
  const poolTop = 0;
  const poolBot = HEADER_H + POOL_PAD_TOP + (maxRank + 1) * ROW_H + POOL_PAD_BOT;

  // waypoint mỗi flow
  const kindById = Object.fromEntries(irL.nodes.map(n => [n.id, n.kind]));
  const nodeIds = new Set(irL.nodes.map(n => n.id));
  const edgeWps = {};
  const backLaneByTgt = {};
  const backFlowIds = new Set();

  // Fan-out gateway: chọn 1 nhánh forward đi THẲNG cột (bottom), nhánh khác ra trái/phải
  // theo hướng target → không chồng stub đáy (tránh verify báo đè).
  const fwdBySrc = {};
  for (const f of irL.flows) {
    if (!nodeIds.has(f.src) || !nodeIds.has(f.tgt)) continue;
    const isBk = backEdges.has(f.src + ' ' + f.tgt) || rank[f.tgt] <= rank[f.src];
    if (!isBk) (fwdBySrc[f.src] ||= []).push(f);
  }
  const srcSideOf = {};
  for (const [srcId, fs] of Object.entries(fwdBySrc)) {
    const sp = pos[srcId];
    if (fs.length < 2) { fs.forEach(f => { srcSideOf[f.id] = 'bottom'; }); continue; }
    const withDx = fs.map(f => ({ f, dx: pos[f.tgt].cx - sp.cx }));
    withDx.sort((a, b) => Math.abs(a.dx) - Math.abs(b.dx));
    srcSideOf[withDx[0].f.id] = 'bottom';
    for (let i = 1; i < withDx.length; i++) srcSideOf[withDx[i].f.id] = withDx[i].dx >= 0 ? 'right' : 'left';
  }

  // Fan-in: nhiều edge vào cùng target → cấp mỗi edge offset x quanh tâm cạnh top (không cắm chồng).
  const inByTgt = {};
  for (const f of irL.flows) if (nodeIds.has(f.src) && nodeIds.has(f.tgt)) (inByTgt[f.tgt] ||= []).push(f.id);
  const tgtDxOf = {};
  for (const [tgt, ids] of Object.entries(inByTgt)) {
    const n = ids.length;
    if (n < 2) { tgtDxOf[ids[0]] = 0; continue; }
    const span = Math.min((pos[tgt].w - 12), (n - 1) * 16);
    ids.forEach((id, i) => { tgtDxOf[id] = Math.round(-span / 2 + (span / (n - 1)) * i); });
  }

  for (const f of irL.flows) {
    if (!nodeIds.has(f.src) || !nodeIds.has(f.tgt)) continue;
    const sp = pos[f.src], tp = pos[f.tgt];
    const isBack = backEdges.has(f.src + '\0' + f.tgt) || rank[f.tgt] <= rank[f.src];
    if (isBack) {
      backFlowIds.add(f.id);
      const lane = (backLaneByTgt[f.tgt] = (backLaneByTgt[f.tgt] || 0) + 1) - 1;
      edgeWps[f.id] = routeBack(sp, tp, tp.y - 24, lane, tgtDxOf[f.id] || 0);
    } else {
      const side = srcSideOf[f.id] || 'bottom';
      const boxes = side === 'bottom' ? [] : allNodeBoxes;
      edgeWps[f.id] = routeForward(sp, tp, side, tgtDxOf[f.id] || 0, boxes, new Set([f.src, f.tgt]));
    }
  }

  // ── khử overlap segment (fan-in cross-lane + đường song song chồng nhau) ──
  deOverlapEdges(edgeWps);

  // ── build XML ──
  let flowEls = '';
  for (const n of irL.nodes) {
    const inc = irL.flows.filter(f => f.tgt === n.id).map(f => `<bpmn:incoming>${esc(f.id)}</bpmn:incoming>`).join('');
    const out = irL.flows.filter(f => f.src === n.id).map(f => `<bpmn:outgoing>${esc(f.id)}</bpmn:outgoing>`).join('');
    flowEls += `    <bpmn:${bpmnTag(n.kind)} id="${esc(n.id)}" name="${esc(n.name)}">${inc}${out}</bpmn:${bpmnTag(n.kind)}>\n`;
  }
  const validFlows = irL.flows.filter(f => nodeIds.has(f.src) && nodeIds.has(f.tgt));
  for (const f of validFlows) {
    flowEls += `    <bpmn:sequenceFlow id="${esc(f.id)}" name="${esc(f.name)}" sourceRef="${esc(f.src)}" targetRef="${esc(f.tgt)}"/>\n`;
  }

  // laneSet
  const laneList = lanes.map(L => {
    const refs = irL.nodes.filter(n => n.lane === L.id).map(n => `<bpmn:flowNodeRef>${esc(n.id)}</bpmn:flowNodeRef>`).join('');
    return `      <bpmn:lane id="${esc(L.id)}" name="${esc(L.name)}">${refs}</bpmn:lane>`;
  }).join('\n');
  const laneSet = `    <bpmn:laneSet id="LaneSet_1">\n${laneList}\n    </bpmn:laneSet>\n`;

  // DI shapes
  let shapes = '', poolDI = '';
  poolDI += `      <bpmndi:BPMNShape bpmnElement="Participant_1" isHorizontal="false"><dc:Bounds x="${poolLeft}" y="${poolTop}" width="${poolRight}" height="${poolBot}"/></bpmndi:BPMNShape>\n`;
  for (const L of lanes) {
    const lx = cols.laneX0[L.id], lw = cols.laneW[L.id];
    poolDI += `      <bpmndi:BPMNShape bpmnElement="${esc(L.id)}" isHorizontal="false"><dc:Bounds x="${lx}" y="${poolTop + HEADER_H}" width="${lw}" height="${poolBot - HEADER_H}"/></bpmndi:BPMNShape>\n`;
  }
  for (const n of irL.nodes) {
    const p = pos[n.id];
    shapes += `      <bpmndi:BPMNShape bpmnElement="${esc(n.id)}"><dc:Bounds x="${p.x}" y="${p.y}" width="${p.w}" height="${p.h}"/></bpmndi:BPMNShape>\n`;
  }

  // DI edges + label
  const nodeBoxes = irL.nodes.map(n => { const p = pos[n.id]; return { x: p.x, y: p.y, w: p.w, h: p.h }; });
  const placed = [];
  let diEdges = '';
  for (const f of validFlows) {
    const pts = edgeWps[f.id];
    const w = pts.map(([x, y]) => `<di:waypoint x="${Math.round(x)}" y="${Math.round(y)}"/>`).join('');
    let labelXml = '';
    if (f.name) {
      const lb = edgeLabel(pts, f.name, placed, nodeBoxes, backFlowIds.has(f.id));
      labelXml = `<bpmndi:BPMNLabel><dc:Bounds x="${Math.round(lb.x)}" y="${Math.round(lb.y)}" width="${Math.round(lb.w)}" height="${lb.h}"/></bpmndi:BPMNLabel>`;
    }
    diEdges += `      <bpmndi:BPMNEdge bpmnElement="${esc(f.id)}">${w}${labelXml}</bpmndi:BPMNEdge>\n`;
  }

  return `<?xml version="1.0" encoding="UTF-8"?>
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI" xmlns:dc="http://www.omg.org/spec/DD/20100524/DC" xmlns:di="http://www.omg.org/spec/DD/20100524/DI" id="Def_1" targetNamespace="http://bpmn.io/schema/bpmn">
  <bpmn:collaboration id="Collaboration_1">
    <bpmn:participant id="Participant_1" name="${esc(ir.process.title)}" processRef="${esc(ir.process.id)}"/>
  </bpmn:collaboration>
  <bpmn:process id="${esc(ir.process.id)}" isExecutable="false">
${laneSet}${flowEls}  </bpmn:process>
  <bpmndi:BPMNDiagram id="Diagram_1">
    <bpmndi:BPMNPlane id="Plane_1" bpmnElement="Collaboration_1">
${poolDI}${shapes}${diEdges}    </bpmndi:BPMNPlane>
  </bpmndi:BPMNDiagram>
</bpmn:definitions>`;
}
