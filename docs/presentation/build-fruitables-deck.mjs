import fs from "node:fs/promises";
import path from "node:path";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const ROOT = "C:/Users/juven/Desktop/Fruitables";
const TMP = "C:/Users/juven/AppData/Local/Temp/codex-presentations/manual-fruitables-b2c/fruitables-deck/tmp";
const OUT = `${ROOT}/outputs/fruitables-b2c-presentation.pptx`;
const ASSET = `${ROOT}/docs/presentation/assets`;

const C = {
  green: "#81C408",
  greenDark: "#5A8A00",
  greenSoft: "#F1F8E8",
  ink: "#1F241B",
  muted: "#5F6B55",
  light: "#8A9680",
  white: "#FFFFFF",
  warm: "#FDFCF8",
  panel: "#F4F6F1",
  border: "#E8EBE3",
  orange: "#F5A623",
  red: "#D9381E",
};

const FONT = "Arial";

async function bytes(file) {
  const b = await fs.readFile(file);
  return b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength);
}

async function writeBlob(file, blob) {
  await fs.mkdir(path.dirname(file), { recursive: true });
  await fs.writeFile(file, new Uint8Array(await blob.arrayBuffer()));
}

function box(slide, x, y, w, h, fill = C.panel, line = C.border, radius = 0) {
  return slide.shapes.add({
    geometry: "roundRect",
    position: { left: x, top: y, width: w, height: h },
    fill,
    line: { style: "solid", fill: line, width: 1 },
    borderRadius: radius,
  });
}

function text(slide, value, x, y, w, h, opts = {}) {
  const s = slide.shapes.add({
    geometry: "textbox",
    position: { left: x, top: y, width: w, height: h },
    fill: "none",
    line: { style: "solid", fill: "none", width: 0 },
  });
  s.text = value;
  s.text.style = {
    fontSize: opts.size ?? 22,
    bold: opts.bold ?? false,
    color: opts.color ?? C.ink,
    alignment: opts.align ?? "left",
    verticalAlignment: opts.valign ?? "top",
    typeface: FONT,
    autoFit: opts.autoFit ?? "shrinkText",
  };
  return s;
}

function line(slide, x, y, w, color = C.border, weight = 1) {
  return slide.shapes.add({
    geometry: "line",
    position: { left: x, top: y, width: w, height: 0 },
    fill: "none",
    line: { style: "solid", fill: color, width: weight },
  });
}

let pageCounter = 0;

function nextPageNumber() {
  pageCounter += 1;
  return String(pageCounter).padStart(2, "0");
}

function base(slide, _number, section = "FRUITABLES / B2C") {
  slide.background.fill = C.white;
  text(slide, section, 48, 22, 315, 24, { size: 11, bold: true, color: C.greenDark });
  line(slide, 390, 34, 160, C.green, 2);
  text(slide, nextPageNumber(), 575, 12, 130, 40, { size: 26, bold: true, color: C.green, align: "center" });
  line(slide, 730, 34, 190, C.green, 2);
  line(slide, 48, 58, 1184, C.border, 1);
  text(slide, "FRUITABLES", 1110, 676, 120, 18, { size: 11, bold: true, color: C.greenDark, align: "right" });
  line(slide, 48, 660, 1184, C.border, 1);
}

function title(slide, value, opts = {}) {
  text(slide, value, 48, opts.y ?? 64, opts.w ?? 1135, opts.h ?? 80, {
    size: opts.size ?? 43,
    bold: true,
    color: opts.color ?? C.ink,
  });
}

function bullets(items) {
  return items.map((v) => `• ${v}`).join("\n");
}

function note(slide, value) {
  slide.speakerNotes.textFrame.setText(value);
  slide.speakerNotes.setVisible(true);
}

async function image(slide, file, x, y, w, h, opts = {}) {
  if (opts.frame !== false) box(slide, x - 8, y - 8, w + 16, h + 16, C.white, opts.border ?? C.green, 0);
  return slide.images.add({
    blob: await bytes(file),
    contentType: "image/png",
    alt: opts.alt ?? path.basename(file),
    fit: opts.fit ?? "contain",
    geometry: "rect",
    borderRadius: 0,
    position: { left: x, top: y, width: w, height: h },
  });
}

function addStep(slide, n, label, x, y, w) {
  const dot = slide.shapes.add({
    geometry: "ellipse",
    position: { left: x, top: y, width: 48, height: 48 },
    fill: C.green,
    line: { style: "solid", fill: C.green, width: 0 },
  });
  dot.text = String(n);
  dot.text.style = { fontSize: 20, bold: true, color: C.white, alignment: "center", verticalAlignment: "middle", typeface: FONT };
  text(slide, label, x - 18, y + 64, w, 72, { size: 20, bold: true, align: "center" });
}

function segment(slide, x1, y1, x2, y2, color = C.light, weight = 2) {
  return slide.shapes.add({
    geometry: "line",
    position: {
      left: Math.min(x1, x2),
      top: Math.min(y1, y2),
      width: Math.abs(x2 - x1),
      height: Math.abs(y2 - y1),
    },
    fill: "none",
    line: { style: "solid", fill: color, width: weight },
  });
}

function relation(slide, points, label, labelX, labelY, labelW = 82) {
  for (let i = 0; i < points.length - 1; i += 1) {
    segment(slide, points[i][0], points[i][1], points[i + 1][0], points[i + 1][1], C.greenDark, 2);
  }
  box(slide, labelX, labelY, labelW, 26, C.greenSoft, C.greenSoft, 4);
  text(slide, label, labelX, labelY + 3, labelW, 20, {
    size: 16,
    bold: true,
    color: C.greenDark,
    align: "center",
    valign: "middle",
  });
}

function erdTable(slide, x, y, w, titleText, rows, accent = C.greenDark) {
  const headerH = 38;
  const rowH = 30;
  const h = headerH + rows.length * rowH;
  box(slide, x, y, w, h, C.white, accent, 0);
  box(slide, x, y, w, headerH, accent, accent, 0);
  text(slide, titleText, x + 12, y + 8, w - 24, 24, {
    size: 18,
    bold: true,
    color: C.white,
    valign: "middle",
  });
  rows.forEach((row, i) => {
    const rowY = y + headerH + i * rowH;
    if (i > 0) line(slide, x, rowY, w, C.border, 1);
    text(slide, row[0], x + 12, rowY + 6, w * 0.54, 20, {
      size: 16,
      bold: row[2] === "PK" || row[2] === "FK",
      color: C.ink,
    });
    text(slide, row[1], x + w * 0.56, rowY + 6, w * 0.28, 20, {
      size: 16,
      color: C.muted,
    });
    if (row[2]) {
      text(slide, row[2], x + w - 48, rowY + 6, 36, 20, {
        size: 16,
        bold: true,
        color: row[2] === "PK" ? C.orange : C.greenDark,
        align: "right",
      });
    }
  });
  return h;
}

const deck = Presentation.create({ slideSize: { width: 1280, height: 720 } });

async function sectionTransition(part, heading, description, visualFile, activeIndex, photo = false) {
  const s = deck.slides.add();
  s.background.fill = C.greenSoft;
  box(s, 0, 0, 590, 720, C.white, C.white, 0);
  await image(s, visualFile, 0, 0, 590, 720, { fit: "cover", frame: false, alt: heading });
  box(s, 0, 0, 18, 720, C.greenDark, C.greenDark, 0);
  if (!photo) {
    box(s, 32, 642, 236, 40, C.greenDark, C.greenDark, 0);
    text(s, "GIAO DIỆN CHỨC NĂNG", 48, 653, 204, 20, { size: 13, bold: true, color: C.white });
  }

  box(s, 590, 0, 690, 720, C.greenSoft, C.greenSoft, 0);
  box(s, 1250, 76, 30, 126, C.green, C.green, 0);
  text(s, nextPageNumber(), 650, 44, 80, 38, { size: 25, bold: true, color: C.green });
  line(s, 748, 66, 170, C.green, 2);
  text(s, part, 650, 164, 300, 30, { size: 17, bold: true, color: C.greenDark });
  text(s, heading, 650, 214, 540, 126, { size: 48, bold: true, color: C.ink });
  line(s, 650, 352, 96, C.green, 5);
  text(s, description, 650, 382, 520, 100, { size: 22, color: C.muted });

  const sections = ["Tổng quan", "Use case", "Chatbox", "Giá", "Combo", "Kết luận"];
  sections.forEach((label, i) => {
    const x = 650 + i * 92;
    line(s, x, 572, 76, i === activeIndex ? C.green : C.border, i === activeIndex ? 5 : 2);
    text(s, label, x, 590, 82, 28, {
      size: 11,
      bold: i === activeIndex,
      color: i === activeIndex ? C.greenDark : C.light,
      align: "center",
    });
  });
  text(s, "FRUITABLES · B2C", 650, 660, 240, 20, { size: 12, bold: true, color: C.greenDark });
  note(s, "Slide chuyển tiếp sang " + heading + ". Dùng thanh tiến trình phía dưới để nhắc người nghe vị trí hiện tại trong bài trình bày.");
}

async function activityDetailSlide({
  section,
  heading,
  visualFile,
  lanes,
  points,
  noteText,
  layout = "wide",
  diagramAspect,
  laneBoundaries,
}) {
  const s = deck.slides.add();
  base(s, 0, section);
  text(s, heading, 48, 74, 1160, 72, { size: 39, bold: true, color: C.ink });

  if (layout === "tall") {
    const diagramX = 48;
    const imageMaxW = 684;
    const imageH = 404;
    const imageW = Math.min(imageMaxW, imageH * diagramAspect);
    const imageX = diagramX + 8 + (imageMaxW - imageW) / 2;
    const laneBounds = laneBoundaries || lanes.map((_, i) => i / lanes.length).concat(1);
    lanes.forEach((laneLabel, i) => {
      const laneX = imageX + imageW * laneBounds[i];
      const laneW = imageW * (laneBounds[i + 1] - laneBounds[i]);
      box(s, laneX, 154, laneW, 36, C.greenSoft, C.green, 0);
      text(s, laneLabel, laneX, 163, laneW, 20, {
        size: 13,
        bold: true,
        color: C.greenDark,
        align: "center",
      });
    });
    box(s, imageX - 8, 190, imageW + 16, 420, C.white, C.green, 0);
    await image(s, visualFile, imageX, 198, imageW, imageH, {
      fit: "contain",
      frame: false,
      alt: heading,
    });
    box(s, 790, 154, 442, 456, C.panel, C.border, 0);
    points.forEach((point, i) => {
      const y = 192 + i * 132;
      text(s, "0" + (i + 1) + ".", 820, y, 62, 34, {
        size: 26,
        bold: true,
        color: C.green,
      });
      text(s, point, 894, y + 2, 300, 88, {
        size: 20,
        bold: true,
        color: C.ink,
      });
      if (i < points.length - 1) line(s, 820, y + 104, 374, C.green, 2);
    });
  } else {
    const diagramX = 48;
    const imageMaxW = 1118;
    const imageH = 318;
    const imageW = Math.min(imageMaxW, imageH * diagramAspect);
    const imageX = diagramX + 8 + (imageMaxW - imageW) / 2;
    const laneBounds = laneBoundaries || lanes.map((_, i) => i / lanes.length).concat(1);
    lanes.forEach((laneLabel, i) => {
      const laneX = imageX + imageW * laneBounds[i];
      const laneW = imageW * (laneBounds[i + 1] - laneBounds[i]);
      box(s, laneX, 154, laneW, 34, C.greenSoft, C.green, 0);
      text(s, laneLabel, laneX, 162, laneW, 20, {
        size: 12,
        bold: true,
        color: C.greenDark,
        align: "center",
      });
    });
    box(s, imageX - 8, 188, imageW + 16, 334, C.white, C.green, 0);
    await image(s, visualFile, imageX, 196, imageW, imageH, {
      fit: "contain",
      frame: false,
      alt: heading,
    });
    points.forEach((point, i) => {
      const x = 48 + i * 396;
      if (i > 0) box(s, x - 18, 544, 2, 80, C.green, C.green, 0);
      text(s, "0" + (i + 1), x, 544, 54, 30, {
        size: 24,
        bold: true,
        color: C.green,
      });
      text(s, point, x + 62, 544, 316, 82, {
        size: 17,
        bold: true,
        color: C.ink,
      });
    });
  }

  note(s, noteText);
}

// Cover
{
  const s = deck.slides.add();
  s.background.fill = C.white;
  await image(s, `${ASSET}/cover-fruitables.png`, 620, 0, 660, 720, { fit: "cover", frame: false, alt: "Rau củ quả tươi cho bìa Fruitables" });
  box(s, 0, 0, 640, 720, C.white, C.white, 0);
  box(s, 620, 84, 26, 134, C.green, C.green, 0);

  text(s, "FRUITABLES / B2C", 64, 52, 260, 28, { size: 16, bold: true, color: C.greenDark });
  line(s, 334, 66, 170, C.green, 2);
  text(s, "ĐỒ ÁN HỆ THỐNG THƯƠNG MẠI ĐIỆN TỬ", 64, 122, 500, 26, { size: 15, bold: true, color: C.greenDark });
  text(s, "Xây dựng hệ thống\nthương mại điện tử\nrau củ quả B2C", 64, 168, 500, 224, { size: 50, bold: true, color: C.ink });
  line(s, 64, 424, 118, C.green, 6);
  text(s, "Kết nối trải nghiệm mua hàng với hoạt động vận hành cửa hàng.", 64, 458, 480, 68, { size: 21, color: C.muted });

  text(s, "THÀNH VIÊN NHÓM 2", 64, 548, 180, 22, { size: 13, bold: true, color: C.greenDark });
  text(s, "Nguyễn Đức Huy\nNguyễn Quang Huy\nTrương Duy Hữu Phúc", 250, 542, 300, 82, { size: 17, bold: true, color: C.ink });
  text(s, "GIẢNG VIÊN HƯỚNG DẪN", 64, 650, 210, 22, { size: 13, bold: true, color: C.greenDark });
  line(s, 280, 666, 242, C.border, 1);
  text(s, "2026", 64, 690, 100, 18, { size: 12, bold: true, color: C.light });
  note(s, "Giới thiệu tên đề tài, mô hình B2C và tên hệ thống Fruitables. Không đi vào chi tiết kỹ thuật ở slide bìa.");
}

// 1
{
  const s = deck.slides.add(); base(s, 1, "01 / GIỚI THIỆU");
  title(s, "Fruitables đưa hoạt động bán rau củ quả lên một hệ thống thống nhất", { w: 600, h: 110 });
  text(s, bullets([
    "Website bán trực tiếp từ cửa hàng đến người tiêu dùng.",
    "Khách tìm sản phẩm, đặt hàng và theo dõi đơn trên cùng một nền tảng.",
    "Chủ và nhân viên vận hành cửa hàng từ khu vực quản trị."
  ]), 56, 238, 515, 240, { size: 23, color: C.muted });
  box(s, 56, 520, 500, 82, C.greenSoft, C.greenSoft, 8);
  text(s, "Mô hình B2C: cửa hàng bán trực tiếp cho người mua cuối", 78, 540, 458, 42, { size: 21, bold: true, color: C.greenDark });
  await image(s, `${ASSET}/customer-shopping.png`, 650, 86, 560, 520, { fit: "cover", alt: "Khách hàng mua rau củ trực tuyến" });
  note(s, "Fruitables là hệ thống thương mại điện tử chuyên cho rau củ quả. Hệ thống kết nối trực tiếp cửa hàng với người mua cuối nên nhóm chọn mô hình B2C. Phạm vi gồm cả trải nghiệm mua hàng và phần quản trị vận hành.");
}

// 2
{
  const s = deck.slides.add(); base(s, 2, "01 / BÀI TOÁN");
  title(s, "Bán hàng rời rạc khiến cả khách mua lẫn cửa hàng mất thời gian");
  const rows = [
    ["01", "Khách khó tìm đúng sản phẩm, mức giá và phân loại phù hợp."],
    ["02", "Thông tin giá, tồn kho và trạng thái sản phẩm dễ thiếu đồng bộ."],
    ["03", "Đơn hàng, đánh giá và tài khoản phải xử lý ở nhiều nơi."],
    ["04", "Khách cần được hỗ trợ nhanh trước khi quyết định mua."]
  ];
  rows.forEach((r, i) => {
    const y = 182 + i * 104;
    text(s, r[0], 64, y + 10, 70, 44, { size: 30, bold: true, color: C.green });
    line(s, 145, y + 34, 90, C.green, 2);
    text(s, r[1], 260, y, 900, 64, { size: 25, color: C.ink, valign: "middle" });
  });
  note(s, "Thông tin bán hàng thường nằm rải rác ở tin nhắn, bảng tính hoặc nhiều kênh. Khách phải hỏi lại về giá và tình trạng hàng, còn cửa hàng dễ sai khi cập nhật dữ liệu thủ công. Đây là nhóm vấn đề Fruitables tập trung giải quyết.");
}

// 3
{
  const s = deck.slides.add(); base(s, 3, "01 / GIẢI PHÁP");
  title(s, "Giải pháp bao phủ trọn quy trình mua hàng B2C");
  line(s, 112, 326, 1030, C.border, 4);
  const steps = [
    [1, "Khám phá\nsản phẩm"], [2, "Giỏ hàng"], [3, "Đặt hàng và\nthanh toán"], [4, "Giao nhận"], [5, "Chăm sóc\nsau mua"]
  ];
  steps.forEach((a, i) => addStep(s, a[0], a[1], 88 + i * 245, 302, 120));
  box(s, 176, 524, 928, 78, C.greenSoft, C.greenSoft, 8);
  text(s, "Ba phần tập trung: Chatbox  •  Quản lý giá  •  Combo sản phẩm", 210, 545, 860, 34, { size: 24, bold: true, color: C.greenDark, align: "center" });
  note(s, "Fruitables bao phủ hành trình từ lúc khách bắt đầu tìm sản phẩm đến khi nhận hàng và đánh giá. Dữ liệu bán hàng được dùng chung giữa giao diện khách và khu vực quản trị. Ba phần được trình bày sâu hơn là Chatbox, quản lý giá và combo.");
}

await sectionTransition(
  "PHẦN 02",
  "Use case hệ thống",
  "Nhận diện các nhóm người dùng và phạm vi chức năng của từng vai trò.",
  ASSET + "/customer-shopping.png",
  1,
  true
);

// 4
{
  const s = deck.slides.add(); base(s, 4, "02 / USE CASE TỔNG QUÁT");
  title(s, "Bốn vai trò được gom thành hai nhóm sử dụng chính");
  box(s, 48, 178, 558, 420, C.greenSoft, C.greenSoft, 8);
  box(s, 630, 178, 602, 420, C.panel, C.border, 8);
  text(s, "NHÓM MUA HÀNG", 80, 210, 250, 28, { size: 17, bold: true, color: C.greenDark });
  text(s, "Khách vãng lai", 80, 278, 240, 42, { size: 30, bold: true });
  text(s, "Tìm kiếm, xem sản phẩm, đánh giá và chuẩn bị giỏ hàng.", 80, 330, 430, 70, { size: 20, color: C.muted });
  line(s, 80, 418, 430, C.border, 1);
  text(s, "Khách hàng", 80, 446, 240, 42, { size: 30, bold: true });
  text(s, "Đăng nhập để đặt hàng, thanh toán, theo dõi đơn và đánh giá.", 80, 498, 430, 74, { size: 20, color: C.muted });
  text(s, "NHÓM VẬN HÀNH", 666, 210, 250, 28, { size: 17, bold: true, color: C.greenDark });
  text(s, "Nhân viên", 666, 278, 240, 42, { size: 30, bold: true });
  text(s, "Xử lý sản phẩm, đơn hàng, đánh giá và hỗ trợ khách.", 666, 330, 430, 70, { size: 20, color: C.muted });
  line(s, 666, 418, 470, C.border, 1);
  text(s, "Chủ cửa hàng", 666, 446, 280, 42, { size: 30, bold: true });
  text(s, "Kiểm soát giá, combo, voucher, giao nhận, SEO và phân quyền.", 666, 498, 470, 74, { size: 20, color: C.muted });
  note(s, "Các actor được gom thành hai nhóm để dễ theo dõi. Khách hàng kế thừa các thao tác cơ bản của khách vãng lai. Chủ cửa hàng có phạm vi quản trị rộng hơn nhân viên, đây cũng là nền tảng của phân quyền.");
}

// 5
{
  const s = deck.slides.add(); base(s, 5, "02 / USE CASE KHÁCH HÀNG");
  title(s, "Khách có thể bắt đầu mua sắm ngay cả khi chưa đăng nhập", { w: 600, h: 100 });
  text(s, bullets([
    "Khách vãng lai tìm kiếm, lọc và xem sản phẩm.",
    "Có thể xem đánh giá và thêm hàng vào giỏ.",
    "Sau khi đăng nhập, khách đặt hàng và theo dõi trạng thái.",
    "Chatbox hỗ trợ ở cả hai trạng thái."
  ]), 56, 218, 500, 270, { size: 22, color: C.muted });
  box(s, 56, 524, 500, 70, C.greenSoft, C.greenSoft, 8);
  text(s, "Đăng nhập ở thời điểm cần hoàn tất giao dịch", 78, 543, 456, 34, { size: 20, bold: true, color: C.greenDark });
  await image(s, `${ASSET}/usecase-customer-guest-overview.png`, 650, 108, 500, 510, { fit: "contain", alt: "Use case tổng quát khách hàng và khách vãng lai" });
  note(s, "Sơ đồ cho thấy người dùng không bị buộc đăng nhập ngay từ đầu. Họ vẫn có thể khám phá sản phẩm và chuẩn bị giỏ hàng. Tài khoản được dùng khi cần đặt hàng, quản lý hồ sơ hoặc theo dõi đơn.");
}

// 6
{
  const s = deck.slides.add(); base(s, 6, "02 / USE CASE CHI TIẾT");
  title(s, "Khách vãng lai tập trung vào giai đoạn khám phá sản phẩm");
  await image(s, `${ASSET}/usecase-guest-detail.png`, 48, 170, 735, 450, { fit: "contain", alt: "Use case chi tiết khách vãng lai" });
  text(s, "4 thao tác chính", 840, 186, 300, 34, { size: 19, bold: true, color: C.greenDark });
  text(s, "Tìm kiếm và lọc\n\nXem danh sách\n\nXem đánh giá\n\nThêm vào giỏ", 840, 248, 330, 300, { size: 28, bold: true, color: C.ink });
  line(s, 840, 574, 286, C.green, 5);
  text(s, "Mục tiêu: giảm rào cản trước khi mua", 840, 594, 330, 34, { size: 18, color: C.muted });
  note(s, "Khách vãng lai thực hiện các thao tác trước khi mua. Hệ thống cho phép họ tìm đúng sản phẩm, xem trải nghiệm của người mua trước và chuẩn bị giỏ hàng trước khi cần cung cấp thông tin tài khoản.");
}

// 7
{
  const s = deck.slides.add(); base(s, 7, "02 / USE CASE CHI TIẾT");
  title(s, "Tài khoản khách hàng hoàn thiện hành trình từ đặt hàng đến sau mua", { w: 650, h: 110 });
  text(s, bullets([
    "Quản lý hồ sơ và thông tin nhận hàng.",
    "Đặt hàng, thanh toán và theo dõi trạng thái.",
    "Hủy đơn khi trạng thái còn cho phép.",
    "Đánh giá sản phẩm sau khi mua."
  ]), 56, 238, 510, 260, { size: 23, color: C.muted });
  box(s, 56, 524, 510, 68, C.greenSoft, C.greenSoft, 8);
  text(s, "Khách hàng kế thừa quyền của khách vãng lai", 78, 542, 470, 34, { size: 20, bold: true, color: C.greenDark });
  await image(s, `${ASSET}/usecase-customer-detail.png`, 640, 132, 560, 486, { fit: "contain", alt: "Use case chi tiết khách hàng" });
  note(s, "Sau khi đăng nhập, người dùng có thể hoàn tất đơn hàng và theo dõi xử lý. Quyền hủy đơn phụ thuộc trạng thái hiện tại. Khi giao dịch hoàn tất, khách có thể gửi đánh giá cho sản phẩm.");
}

// 8
{
  const s = deck.slides.add(); base(s, 8, "02 / USE CASE QUẢN TRỊ");
  title(s, "Nhân viên vận hành hằng ngày, chủ cửa hàng kiểm soát toàn hệ thống");
  text(s, "NHÂN VIÊN", 72, 192, 310, 42, { size: 20, bold: true, color: C.greenDark });
  text(s, "Sản phẩm và danh mục\nĐơn hàng và đánh giá\nTài khoản khách hàng\nThống kê doanh thu", 72, 252, 430, 230, { size: 29, bold: true });
  text(s, "Xử lý công việc phát sinh mỗi ngày", 72, 520, 430, 38, { size: 19, color: C.muted });
  line(s, 610, 178, 0, C.green, 5);
  const divider = slide => slide.shapes.add({ geometry: "line", position: { left: 616, top: 180, width: 0, height: 400 }, fill: "none", line: { style: "solid", fill: C.green, width: 5 } });
  divider(s);
  text(s, "CHỦ CỬA HÀNG", 690, 192, 330, 42, { size: 20, bold: true, color: C.greenDark });
  text(s, "Toàn bộ quyền nhân viên\nGiá và combo sản phẩm\nVoucher, giao nhận, SEO\nPhân quyền nội bộ", 690, 252, 470, 230, { size: 29, bold: true });
  text(s, "Điều chỉnh chính sách bán hàng và quyền truy cập", 690, 520, 470, 46, { size: 19, color: C.muted });
  note(s, "Nhân viên xử lý các nghiệp vụ diễn ra thường xuyên. Chủ cửa hàng có thêm các quyền cấu hình và kiểm soát. Việc tách vai trò giúp giảm thao tác nhầm và giới hạn đúng phạm vi dữ liệu.");
}

// 9
{
  const s = deck.slides.add(); base(s, 9, "02 / USE CASE NHÂN VIÊN");
  title(s, "Nhân viên xử lý vận hành và chăm sóc khách hàng");
  text(s, "SẢN PHẨM VÀ ĐƠN HÀNG", 80, 154, 470, 28, { size: 17, bold: true, color: C.greenDark });
  text(s, "KHÁCH HÀNG VÀ BÁO CÁO", 696, 154, 470, 28, { size: 17, bold: true, color: C.greenDark });
  await image(s, `${ASSET}/usecase-employee-top.png`, 48, 192, 568, 420, { fit: "contain", alt: "Nhân viên quản lý sản phẩm và đơn hàng" });
  await image(s, `${ASSET}/usecase-employee-bottom.png`, 664, 192, 568, 420, { fit: "contain", alt: "Nhân viên quản lý khách hàng và báo cáo" });
  note(s, "Use case nhân viên được phân thành hai vùng để chữ dễ đọc. Vùng đầu là sản phẩm, danh mục và đơn hàng. Vùng sau là đánh giá, tài khoản khách hàng, báo cáo và phân quyền theo phạm vi được cấp.");
}

// 10
{
  const s = deck.slides.add(); base(s, 10, "02 / USE CASE CHỦ CỬA HÀNG");
  title(s, "Chủ cửa hàng điều chỉnh chính sách bán hàng và quyền truy cập");
  text(s, "VẬN HÀNH CHUNG", 80, 154, 470, 28, { size: 17, bold: true, color: C.greenDark });
  text(s, "KINH DOANH VÀ HỆ THỐNG", 696, 154, 470, 28, { size: 17, bold: true, color: C.greenDark });
  await image(s, `${ASSET}/usecase-owner-top.png`, 48, 192, 568, 420, { fit: "contain", alt: "Use case phần trên của chủ cửa hàng" });
  await image(s, `${ASSET}/usecase-owner-bottom.png`, 664, 192, 568, 420, { fit: "contain", alt: "Use case phần dưới của chủ cửa hàng" });
  note(s, "Chủ cửa hàng có toàn bộ quyền vận hành và thêm các chức năng cấu hình. Quản lý giá và combo tác động trực tiếp đến cách sản phẩm được bán. Phân quyền giúp chủ kiểm soát nhân viên nào được sử dụng từng nhóm chức năng.");
}

await sectionTransition(
  "PHẦN 03",
  "Chatbox hỗ trợ khách hàng",
  "Bắt đầu từ cấu trúc dữ liệu, sau đó theo dõi cách hệ thống tạo câu trả lời.",
  ASSET + "/chat-feature-ui.png",
  2,
  true
);

// Previous combined feature slides retained for reference only.
if (false) {
// 11
{
  const s = deck.slides.add(); base(s, 11, "03 / CHATBOX");
  title(s, "Chatbox trả lời dựa trên dữ liệu thật của cửa hàng", { w: 560, h: 100 });
  text(s, "1", 62, 208, 42, 42, { size: 30, bold: true, color: C.green });
  text(s, "Tiếp nhận câu hỏi", 118, 212, 390, 36, { size: 25, bold: true });
  text(s, "2", 62, 312, 42, 42, { size: 30, bold: true, color: C.green });
  text(s, "Tìm đoạn kiến thức phù hợp", 118, 316, 430, 36, { size: 25, bold: true });
  text(s, "3", 62, 416, 42, 42, { size: 30, bold: true, color: C.green });
  text(s, "Tạo và lưu câu trả lời", 118, 420, 390, 36, { size: 25, bold: true });
  box(s, 56, 520, 500, 72, C.greenSoft, C.greenSoft, 8);
  text(s, "Hỗ trợ khách vãng lai và khách đã đăng nhập", 78, 540, 458, 34, { size: 19, bold: true, color: C.greenDark });
  await image(s, `${ASSET}/chatbox-flow.png`, 620, 112, 570, 510, { fit: "contain", alt: "Luồng Chatbox Fruitables" });
  note(s, "Khi khách gửi câu hỏi, hệ thống tìm trong kho kiến thức tạo từ FAQ, sản phẩm và cấu hình cửa hàng. Phần nội dung phù hợp được dùng để tạo câu trả lời. Phiên chat và tin nhắn được lưu để giữ mạch hội thoại.");
}

// 12
{
  const s = deck.slides.add(); base(s, 12, "03 / CHATBOX DATABASE");
  title(s, "Dữ liệu Chatbox tách riêng hội thoại và kho kiến thức");
  box(s, 48, 146, 352, 92, C.greenSoft, C.greenSoft, 8);
  text(s, "Hội thoại", 70, 164, 120, 30, { size: 20, bold: true, color: C.greenDark });
  text(s, "Users → ChatSessions → ChatMessages", 70, 198, 306, 28, { size: 17, color: C.muted });
  box(s, 424, 146, 464, 92, C.panel, C.border, 8);
  text(s, "Kho kiến thức", 446, 164, 170, 30, { size: 20, bold: true, color: C.greenDark });
  text(s, "Faqs / Products → KnowledgeChunks", 446, 198, 412, 28, { size: 17, color: C.muted });
  box(s, 912, 146, 320, 92, C.warm, C.border, 8);
  text(s, "Ý nghĩa", 934, 164, 110, 30, { size: 20, bold: true, color: C.greenDark });
  text(s, "Cập nhật tri thức không làm mất lịch sử chat", 934, 198, 276, 28, { size: 16, color: C.muted });
  await image(s, `${ASSET}/chatbox-erd.png`, 82, 270, 1110, 348, { fit: "contain", alt: "ERD của chức năng Chatbox" });
  note(s, "Cơ sở dữ liệu Chatbox có hai nhóm. Nhóm đầu lưu lịch sử giao tiếp qua phiên chat và tin nhắn. Nhóm sau lưu tri thức để tìm nội dung liên quan. Việc tách nhóm giúp lịch sử hội thoại không phụ thuộc cách kho kiến thức được cập nhật.");
}

// 13
{
  const s = deck.slides.add(); base(s, 13, "04 / QUẢN LÝ GIÁ");
  title(s, "Quản lý giá cho phép thay đổi có lịch và có kiểm soát");
  text(s, "LUỒNG XỬ LÝ", 74, 148, 300, 28, { size: 17, bold: true, color: C.greenDark });
  text(s, "CƠ SỞ DỮ LIỆU", 696, 148, 300, 28, { size: 17, bold: true, color: C.greenDark });
  await image(s, `${ASSET}/price-flow.png`, 48, 184, 568, 384, { fit: "contain", alt: "Luồng tạo và áp dụng lịch giá" });
  await image(s, `${ASSET}/price-schedule-erd.png`, 664, 184, 568, 384, { fit: "contain", alt: "ERD quản lý giá" });
  box(s, 48, 586, 1184, 54, C.greenSoft, C.greenSoft, 8);
  text(s, "Products / ProductVariants  •  PriceSchedules  •  ProductLogs  •  Users", 80, 600, 1120, 28, { size: 20, bold: true, color: C.greenDark, align: "center" });
  note(s, "Tính năng xử lý giá gốc và lịch giảm theo thời gian. Hệ thống kiểm tra khoảng thời gian, mức giảm và lịch bị trùng trước khi lưu. Sau khi giá đổi, giá hiển thị cho khách và giá tính trong combo cũng được cập nhật.");
}

// 14
{
  const s = deck.slides.add(); base(s, 14, "05 / COMBO SẢN PHẨM");
  title(s, "Combo gom nhiều sản phẩm nhưng vẫn dùng giá và tồn kho hiện tại");
  text(s, "LUỒNG TẠO COMBO", 74, 148, 300, 28, { size: 17, bold: true, color: C.greenDark });
  text(s, "CƠ SỞ DỮ LIỆU", 696, 148, 300, 28, { size: 17, bold: true, color: C.greenDark });
  await image(s, `${ASSET}/combo-flow.png`, 48, 184, 568, 384, { fit: "contain", alt: "Luồng quản trị tạo combo" });
  await image(s, `${ASSET}/combo-erd.png`, 664, 184, 568, 384, { fit: "contain", alt: "ERD combo sản phẩm" });
  box(s, 48, 586, 1184, 54, C.greenSoft, C.greenSoft, 8);
  text(s, "Combos → ComboItems → Products / ProductVariants", 80, 600, 1120, 28, { size: 21, bold: true, color: C.greenDark, align: "center" });
  note(s, "Combo nhóm nhiều sản phẩm thành một gợi ý mua chung. Tổng giá được tính lại từ giá đang có hiệu lực của từng sản phẩm. Khi thêm vào giỏ, hệ thống kiểm tra trạng thái và tồn kho từng mục.");
}

// 15
{
  const s = deck.slides.add(); base(s, 15, "06 / KẾT LUẬN");
  title(s, "Fruitables nối trải nghiệm mua hàng với công việc vận hành phía sau");
  const cols = [
    ["CHATBOX", "Tìm thông tin và hỗ trợ khách nhanh hơn"],
    ["QUẢN LÝ GIÁ", "Kiểm soát giá gốc và lịch giảm theo thời gian"],
    ["COMBO", "Bán nhiều sản phẩm theo nhóm với giá hiện tại"]
  ];
  cols.forEach((c, i) => {
    const x = 48 + i * 405;
    box(s, x, 220, 374, 280, i === 0 ? C.greenSoft : C.panel, i === 0 ? C.greenSoft : C.border, 8);
    text(s, `0${i + 1}`, x + 28, 250, 80, 52, { size: 38, bold: true, color: C.green });
    text(s, c[0], x + 28, 330, 300, 38, { size: 24, bold: true, color: C.ink });
    text(s, c[1], x + 28, 388, 310, 78, { size: 20, color: C.muted });
  });
  text(s, "Hành trình demo đề xuất", 48, 548, 280, 30, { size: 18, bold: true, color: C.greenDark });
  text(s, "Chatbox  →  Quản lý giá  →  Tạo combo sản phẩm", 48, 586, 900, 38, { size: 28, bold: true, color: C.ink });
  note(s, "Fruitables giải quyết bài toán ở cả hai phía B2C. Khách có quy trình mua hàng liền mạch, còn cửa hàng có dữ liệu tập trung để vận hành. Kết thúc phần trình bày bằng ba tính năng sẽ được demo lần lượt.");
}
}

// 11 - Layout 03: dominant database visual with editorial explanation
{
  const s = deck.slides.add(); base(s, 11, "03 / CHATBOX · DATABASE");
  title(s, "Database Chatbox tách hội thoại khỏi kho kiến thức", { h: 72 });

  text(s, "NHÓM HỘI THOẠI", 60, 148, 260, 24, { size: 16, bold: true, color: C.greenDark });
  text(s, "KHO KIẾN THỨC", 60, 386, 260, 24, { size: 16, bold: true, color: C.greenDark });

  relation(s, [[310, 249], [400, 249]], "1 — N", 319, 237, 72);
  relation(s, [[700, 249], [800, 249]], "1 — N", 714, 237, 72);
  relation(s, [[185, 430], [185, 406], [720, 406], [720, 480], [760, 480]], "NẠP TRI THỨC", 434, 394, 126);
  relation(s, [[485, 430], [485, 416], [710, 416], [710, 540], [760, 540]], "NẠP TRI THỨC", 560, 404, 126);

  erdTable(s, 60, 185, 250, "Users", [
    ["Id", "integer", "PK"],
    ["FullName", "string", ""],
    ["Role", "string", ""],
  ]);
  erdTable(s, 400, 185, 300, "ChatSessions", [
    ["Id", "GUID", "PK"],
    ["UserId", "integer", "FK"],
    ["Source", "string", ""],
    ["LastMessageAt", "datetime", ""],
  ]);
  erdTable(s, 800, 185, 350, "ChatMessages", [
    ["Id", "long", "PK"],
    ["ChatSessionId", "GUID", "FK"],
    ["Role", "string", ""],
    ["Content", "text", ""],
  ]);
  erdTable(s, 60, 430, 250, "Products", [
    ["Id", "integer", "PK"],
    ["Name", "string", ""],
    ["IsActive", "boolean", ""],
  ]);
  erdTable(s, 360, 430, 250, "Faqs", [
    ["Id", "integer", "PK"],
    ["Title", "string", ""],
    ["Content", "text", ""],
  ]);
  erdTable(s, 760, 420, 390, "KnowledgeChunks", [
    ["Id", "long", "PK"],
    ["SourceType", "string", ""],
    ["SourceId", "string", "FK"],
    ["Content", "text", ""],
    ["VectorEmbedding", "vector", ""],
  ]);
  note(s, "Trình bày database trước để người nghe biết dữ liệu nào tham gia vào Chatbox. Nhóm hội thoại lưu phiên và tin nhắn. Nhóm kho kiến thức lưu tài liệu và các đoạn nội dung dùng khi truy xuất câu trả lời.");
}

// 12 - Layout 05: image plus numbered list bars
if (false) {
  const s = deck.slides.add(); base(s, 12, "03 / CHATBOX · ACTIVITY");
  title(s, "Luồng Chatbox biến câu hỏi thành câu trả lời có ngữ cảnh", { h: 72 });
  await image(s, ASSET + "/chatbox-flow.png", 48, 174, 570, 420, { fit: "contain", alt: "Luồng hoạt động Chatbox Fruitables" });
  box(s, 656, 164, 576, 446, C.greenSoft, C.greenSoft, 0);
  const items = [
    ["Tiếp nhận câu hỏi", "Gắn câu hỏi với người dùng hoặc phiên vãng lai"],
    ["Truy xuất tri thức", "Tìm các đoạn nội dung liên quan trong kho kiến thức"],
    ["Phản hồi và lưu", "Tạo câu trả lời rồi ghi lại lịch sử hội thoại"]
  ];
  items.forEach((item, i) => {
    const y = 202 + i * 124;
    box(s, 684, y + 8, 500, 92, C.greenDark, C.greenDark, 0);
    box(s, 672, y, 500, 92, C.green, C.green, 0);
    text(s, item[0], 698, y + 16, 330, 30, { size: 22, bold: true, color: C.white });
    text(s, item[1], 698, y + 50, 380, 34, { size: 15, color: C.white });
    text(s, "0" + (i + 1) + ".", 1082, y + 24, 70, 38, { size: 28, bold: true, color: C.white, align: "right" });
  });
  note(s, "Sau phần database, trình bày activity theo ba bước. Hệ thống tiếp nhận câu hỏi, truy xuất nội dung phù hợp, tạo phản hồi và lưu lại hội thoại để duy trì ngữ cảnh.");
}

await activityDetailSlide({
  section: "03 / CHATBOX · ACTIVITY 1/3",
  heading: "Chatbox 1/3: Mở chat và khôi phục phiên làm việc",
  visualFile: ASSET + "/chat-activity-1.png",
  diagramAspect: 2.578,
  laneBoundaries: [0.005, 0.177, 0.386, 0.606, 0.976],
  lanes: ["Người dùng", "Giao diện Chat", "API Chat", "RAG / AI"],
  points: [
    "Người dùng mở khung chat và giao diện kiểm tra phiên gần nhất.",
    "API tải lại phiên đã lưu; nếu chưa có thì tạo một phiên mới.",
    "Nếu không thể khôi phục, giao diện hiển thị lỗi và người dùng có thể đóng chat."
  ],
  noteText: "Chặng đầu giải quyết việc khởi tạo ngữ cảnh hội thoại. Hệ thống ưu tiên dùng lại phiên cũ để giữ lịch sử; nếu không có sẽ tạo phiên mới. Lỗi khôi phục được hiển thị rõ, không ép người dùng tiếp tục."
});

await activityDetailSlide({
  section: "03 / CHATBOX · ACTIVITY 2/3",
  heading: "Chatbox 2/3: Gửi câu hỏi và kiểm tra yêu cầu",
  visualFile: ASSET + "/chat-activity-2.png",
  diagramAspect: 2.218,
  laneBoundaries: [0.005, 0.177, 0.386, 0.606, 0.976],
  lanes: ["Người dùng", "Giao diện Chat", "API Chat", "RAG / AI"],
  points: [
    "Người dùng nhập câu hỏi hoặc chọn gợi ý; giao diện báo đang soạn trả lời.",
    "API kiểm tra độ dài nội dung và giới hạn gửi trước khi xử lý.",
    "Yêu cầu sai trả lỗi ngay; yêu cầu hợp lệ được lưu và chuyển sang RAG / AI."
  ],
  noteText: "Chặng hai tập trung vào kiểm tra đầu vào. Giao diện phản hồi ngay trạng thái gửi, API loại bỏ yêu cầu không hợp lệ, còn câu hỏi hợp lệ được lưu trước khi chuyển sang tầng truy xuất tri thức."
});

await activityDetailSlide({
  section: "03 / CHATBOX · ACTIVITY 3/3",
  heading: "Chatbox 3/3: Truy xuất tri thức và trả phản hồi",
  visualFile: ASSET + "/chat-activity-3.png",
  diagramAspect: 2.446,
  laneBoundaries: [0.005, 0.177, 0.386, 0.606, 0.976],
  lanes: ["Người dùng", "Giao diện Chat", "API Chat", "RAG / AI"],
  points: [
    "RAG mã hóa câu hỏi, tìm và xếp hạng các mẫu kiến thức liên quan.",
    "AI tạo câu trả lời từ dữ liệu tìm được hoặc báo chưa đủ thông tin.",
    "Phản hồi được lưu, cập nhật lên màn hình; người dùng chọn hỏi tiếp hoặc đóng chat."
  ],
  noteText: "Chặng cuối cho thấy vai trò của RAG và AI. Câu trả lời chỉ dựa trên nội dung truy xuất được. Sau khi lưu và hiển thị phản hồi, quyền quyết định tiếp tục hay đóng chat thuộc về người dùng."
});

await sectionTransition(
  "PHẦN 04",
  "Quản lý giá",
  "Kiểm soát giá hiện tại, lịch áp dụng và quá trình kiểm tra trước khi công bố.",
  ASSET + "/price-feature-ui.png",
  3,
  true
);

// 13 - Layout 03: database first
{
  const s = deck.slides.add(); base(s, 13, "04 / QUẢN LÝ GIÁ · DATABASE");
  title(s, "Database quản lý giá lưu giá hiện tại, lịch áp dụng và lịch sử thay đổi", { h: 76 });

  relation(s, [[680, 249], [740, 249]], "1 — N", 674, 237, 72);
  relation(s, [[195, 343], [195, 390]], "1 — N", 159, 350, 72);
  relation(s, [[360, 282], [342, 282], [342, 468], [360, 468]], "1 — N", 306, 344, 72);
  relation(s, [[195, 185], [195, 164], [1150, 164], [1150, 410]], "TẠO LỊCH", 1046, 152, 90);
  relation(s, [[520, 343], [520, 374], [830, 374], [830, 410]], "1 — N", 646, 362, 72);
  relation(s, [[905, 343], [905, 410]], "ÁP DỤNG", 866, 360, 80);

  erdTable(s, 60, 185, 270, "Users", [
    ["Id", "integer", "PK"],
    ["FullName", "string", ""],
    ["Role", "string", ""],
    ["IsActive", "boolean", ""],
  ]);
  erdTable(s, 360, 185, 320, "Products", [
    ["Id", "integer", "PK"],
    ["CategoryId", "integer", "FK"],
    ["Name", "string", ""],
    ["BasePrice", "decimal", ""],
  ]);
  erdTable(s, 740, 185, 330, "ProductVariants", [
    ["Id", "integer", "PK"],
    ["ProductId", "integer", "FK"],
    ["Sku", "string", ""],
    ["Price", "decimal", ""],
  ]);
  erdTable(s, 60, 390, 300, "ProductLogs", [
    ["Id", "integer", "PK"],
    ["ProductId", "integer", "FK"],
    ["AdminId", "integer", "FK"],
    ["Action", "string", ""],
    ["CreatedAt", "datetime", ""],
  ]);
  erdTable(s, 740, 410, 430, "PriceSchedules", [
    ["Id", "integer", "PK"],
    ["ProductId", "integer", "FK"],
    ["ProductVariantId", "integer", "FK"],
    ["DiscountValue", "decimal", ""],
    ["StartsAt / EndsAt", "datetime", ""],
    ["CreatedBy", "integer", "FK"],
  ]);
  box(s, 400, 442, 280, 104, C.greenSoft, C.greenSoft, 4);
  text(s, "GIÁ CÓ HIỆU LỰC", 422, 460, 236, 24, { size: 16, bold: true, color: C.greenDark, align: "center" });
  text(s, "Giá gốc + lịch đang hoạt động", 422, 498, 236, 28, { size: 19, bold: true, color: C.ink, align: "center" });
  note(s, "Database quản lý giá gồm dữ liệu sản phẩm, lịch giá và nhật ký. Việc lưu lịch giúp hệ thống biết giá nào có hiệu lực theo thời điểm, còn nhật ký giúp truy vết người thực hiện.");
}

// 14 - Layout 05: activity after database
if (false) {
  const s = deck.slides.add(); base(s, 14, "04 / QUẢN LÝ GIÁ · ACTIVITY");
  title(s, "Luồng quản lý giá kiểm tra hợp lệ trước khi công bố", { h: 72 });
  await image(s, ASSET + "/price-flow.png", 48, 174, 570, 420, { fit: "contain", alt: "Luồng hoạt động quản lý giá" });
  box(s, 656, 164, 576, 446, C.panel, C.border, 0);
  const items = [
    ["Nhập giá hoặc lịch giá", "Chủ cửa hàng chọn sản phẩm, mức giá và thời gian"],
    ["Kiểm tra điều kiện", "Phát hiện giá sai, thời gian sai hoặc lịch bị trùng"],
    ["Lưu và áp dụng", "Cập nhật giá hiển thị, combo và ghi nhật ký"]
  ];
  items.forEach((item, i) => {
    const y = 198 + i * 126;
    text(s, "0" + (i + 1) + ".", 690, y, 74, 40, { size: 30, bold: true, color: C.green });
    text(s, item[0], 784, y + 2, 386, 34, { size: 24, bold: true });
    text(s, item[1], 784, y + 42, 386, 52, { size: 17, color: C.muted });
    if (i < 2) line(s, 690, y + 104, 480, C.green, 2);
  });
  note(s, "Sau khi đã biết cấu trúc dữ liệu, người nghe theo dõi luồng xử lý. Thông tin được nhập, hệ thống kiểm tra lỗi và xung đột, sau đó mới lưu, áp dụng và ghi lịch sử.");
}

await activityDetailSlide({
  section: "04 / QUẢN LÝ GIÁ · ACTIVITY 1/3",
  heading: "Quản lý giá 1/3: Chọn sản phẩm và tạo lịch giảm",
  visualFile: ASSET + "/price-activity-1.png",
  diagramAspect: 1.23,
  laneBoundaries: [0.019, 0.484, 0.969],
  lanes: ["Admin", "Hệ thống"],
  points: [
    "Admin mở trang quản lý giá và chọn sản phẩm hoặc phân loại cần áp dụng.",
    "Hệ thống hiển thị giá hiện tại cùng các lịch giảm giá đã có.",
    "Admin nhập mức giảm, thời gian bắt đầu, kết thúc rồi xác nhận tạo lịch."
  ],
  noteText: "Chặng đầu mô tả thao tác chủ động của admin. Hệ thống cung cấp bối cảnh giá hiện tại và các lịch đã tồn tại trước khi admin nhập lịch giảm mới.",
  layout: "tall"
});

await activityDetailSlide({
  section: "04 / QUẢN LÝ GIÁ · ACTIVITY 2/3",
  heading: "Quản lý giá 2/3: Kiểm tra lịch và chờ áp dụng",
  visualFile: ASSET + "/price-activity-2.png",
  diagramAspect: 1.29,
  laneBoundaries: [0.019, 0.484, 0.969],
  lanes: ["Admin", "Hệ thống"],
  points: [
    "Hệ thống kiểm tra mức giảm, khoảng thời gian và xung đột với lịch khác.",
    "Thông tin sai được báo lỗi để admin tự chọn chỉnh sửa hoặc dừng lại.",
    "Lịch hợp lệ được lưu ở trạng thái chờ; admin có thể tạo tiếp hoặc chờ áp dụng."
  ],
  noteText: "Chặng hai nhấn mạnh nhánh kiểm tra. Khi có lỗi, hệ thống chỉ hiển thị thông báo và giữ quyền quyết định cho admin. Lịch hợp lệ được lưu nhưng chưa làm thay đổi giá ngay.",
  layout: "tall"
});

await activityDetailSlide({
  section: "04 / QUẢN LÝ GIÁ · ACTIVITY 3/3",
  heading: "Quản lý giá 3/3: Tự động áp dụng và kết thúc lịch",
  visualFile: ASSET + "/price-activity-3.png",
  diagramAspect: 1.556,
  laneBoundaries: [0.019, 0.484, 0.969],
  lanes: ["Admin", "Hệ thống"],
  points: [
    "Hệ thống định kỳ kiểm tra các lịch đang chờ và thời điểm bắt đầu.",
    "Khi tới hạn, giá giảm được áp dụng và cập nhật ngay trên cửa hàng.",
    "Khi lịch hết hạn, hệ thống khôi phục giá gốc và đánh dấu lịch đã kết thúc."
  ],
  noteText: "Chặng cuối được hệ thống tự động thực hiện. Giá gốc không bị thay đổi, chỉ giá bán hiệu lực được cập nhật trong thời gian lịch chạy và được khôi phục khi hết hạn.",
  layout: "tall"
});

await sectionTransition(
  "PHẦN 05",
  "Combo sản phẩm",
  "Liên kết nhiều sản phẩm thành một gói bán và xử lý đầy đủ các nhánh thành công, lỗi và tạo tiếp.",
  ASSET + "/combo-feature-ui.png",
  4,
  true
);

// 15 - Layout 03: database first
{
  const s = deck.slides.add(); base(s, 15, "05 / COMBO SẢN PHẨM · DATABASE");
  title(s, "Database combo liên kết một gói bán với nhiều sản phẩm cụ thể", { h: 76 });

  relation(s, [[380, 345], [860, 345]], "1 — N", 594, 333, 72);
  relation(s, [[380, 484], [410, 484], [410, 614], [820, 614], [820, 388], [860, 388]], "1 — N", 590, 602, 72);
  relation(s, [[760, 514], [810, 514], [810, 448], [860, 448]], "0..1", 772, 500, 72);

  erdTable(s, 60, 185, 320, "Combos", [
    ["Id", "integer", "PK"],
    ["Name", "string", ""],
    ["Slug", "string", ""],
    ["Description", "text", ""],
    ["IsActive", "boolean", ""],
  ]);
  erdTable(s, 60, 420, 320, "Products", [
    ["Id", "integer", "PK"],
    ["Name", "string", ""],
    ["BasePrice", "decimal", ""],
    ["Stock", "integer", ""],
  ]);
  erdTable(s, 450, 420, 310, "ProductVariants", [
    ["Id", "integer", "PK"],
    ["ProductId", "integer", "FK"],
    ["Sku", "string", ""],
    ["Price", "decimal", ""],
  ]);
  erdTable(s, 860, 220, 340, "ComboItems", [
    ["Id", "integer", "PK"],
    ["ComboId", "integer", "FK"],
    ["ProductId", "integer", "FK"],
    ["ProductVariantId", "integer", "FK"],
    ["Quantity", "integer", ""],
    ["SortOrder", "integer", ""],
  ]);
  box(s, 450, 185, 310, 126, C.greenSoft, C.greenSoft, 4);
  text(s, "NGUYÊN TẮC TÍNH GIÁ", 474, 204, 262, 24, { size: 16, bold: true, color: C.greenDark, align: "center" });
  text(s, "Tổng giá = giá có hiệu lực × số lượng", 474, 246, 262, 42, { size: 20, bold: true, color: C.ink, align: "center" });
  note(s, "Database combo có bảng Combos và bảng chi tiết ComboItems. ComboItems liên kết tới sản phẩm hoặc biến thể và lưu số lượng. Giá bán được tính theo giá hiện hành thay vì sao chép một mức giá cố định.");
}

// 16 - Layout 05: activity after database
if (false) {
  const s = deck.slides.add(); base(s, 16, "05 / COMBO SẢN PHẨM · ACTIVITY");
  title(s, "Luồng tạo combo cho phép sửa lỗi hoặc tạo tiếp sau khi hoàn tất", { h: 76 });
  await image(s, ASSET + "/combo-flow.png", 48, 184, 610, 400, { fit: "contain", alt: "Luồng hoạt động tạo combo sản phẩm" });
  box(s, 696, 174, 536, 420, C.greenSoft, C.greenSoft, 0);
  text(s, "01.", 728, 208, 72, 38, { size: 30, bold: true, color: C.greenDark });
  text(s, "Nhập thông tin combo", 820, 210, 360, 34, { size: 24, bold: true });
  text(s, "Chọn sản phẩm, biến thể và số lượng.", 820, 250, 350, 42, { size: 17, color: C.muted });
  text(s, "02.", 728, 322, 72, 38, { size: 30, bold: true, color: C.greenDark });
  text(s, "Kiểm tra và hiển thị kết quả", 820, 324, 360, 34, { size: 24, bold: true });
  text(s, "Lỗi hiển thị lại trên form; dữ liệu hợp lệ được lưu.", 820, 364, 350, 48, { size: 17, color: C.muted });
  text(s, "03.", 728, 444, 72, 38, { size: 30, bold: true, color: C.greenDark });
  text(s, "Xem combo hoặc tạo tiếp", 820, 446, 360, 34, { size: 24, bold: true });
  text(s, "Người dùng tự chọn quay lại danh sách hoặc mở form mới.", 820, 486, 350, 54, { size: 17, color: C.muted });
  note(s, "Luồng activity nhấn mạnh các nhánh người dùng đã góp ý. Nếu dữ liệu sai, giao diện hiển thị lỗi và giữ lại form để người dùng tự quyết định sửa. Sau khi tạo thành công, người dùng có thể xem combo vừa tạo hoặc bắt đầu tạo combo khác.");
}

await activityDetailSlide({
  section: "05 / COMBO SẢN PHẨM · ACTIVITY 1/3",
  heading: "Combo 1/3: Mở quản lý và chuẩn bị form tạo mới",
  visualFile: ASSET + "/combo-activity-1.png",
  diagramAspect: 3.154,
  laneBoundaries: [0.006, 0.251, 0.594, 0.971],
  lanes: ["Admin", "Giao diện quản trị", "Hệ thống"],
  points: [
    "Admin mở chức năng combo; giao diện hiển thị màn hình quản lý.",
    "Hệ thống lấy danh sách combo và tính tổng giá hiện tại của từng combo.",
    "Khi bấm Thêm, giao diện mở form và yêu cầu danh sách sản phẩm, biến thể đang bán."
  ],
  noteText: "Chặng đầu bắt đầu từ thao tác chọn chức năng. Giao diện luôn hiển thị màn hình quản lý và dữ liệu hiện tại trước khi admin bắt đầu tạo combo mới."
});

await activityDetailSlide({
  section: "05 / COMBO SẢN PHẨM · ACTIVITY 2/3",
  heading: "Combo 2/3: Nhập thông tin và gửi dữ liệu kiểm tra",
  visualFile: ASSET + "/combo-activity-2.png",
  diagramAspect: 2.934,
  laneBoundaries: [0.006, 0.251, 0.594, 0.971],
  lanes: ["Admin", "Giao diện quản trị", "Hệ thống"],
  points: [
    "Form hiển thị ô nhập, sản phẩm, biến thể và số lượng có thể chọn.",
    "Admin nhập tên, mô tả, ảnh, trạng thái cùng các sản phẩm trong combo.",
    "Khi bấm Lưu, giao diện gửi dữ liệu để hệ thống kiểm tra tính hợp lệ."
  ],
  noteText: "Chặng hai là quá trình nhập liệu. Dữ liệu chưa được lưu ngay khi admin nhập mà chỉ được gửi sang hệ thống sau khi bấm Lưu combo."
});

await activityDetailSlide({
  section: "05 / COMBO SẢN PHẨM · ACTIVITY 3/3",
  heading: "Combo 3/3: Xử lý lỗi, lưu thành công và tạo tiếp",
  visualFile: ASSET + "/combo-activity-3.png",
  diagramAspect: 3.507,
  laneBoundaries: [0.006, 0.251, 0.594, 0.971],
  lanes: ["Admin", "Giao diện quản trị", "Hệ thống"],
  points: [
    "Dữ liệu sai được báo ngay trên form và giữ nguyên thông tin đã nhập.",
    "Admin tự chọn chỉnh sửa rồi lưu lại hoặc dừng; hệ thống không ép buộc.",
    "Khi thành công, admin xem combo vừa tạo rồi chọn tạo tiếp hoặc kết thúc."
  ],
  noteText: "Chặng cuối làm rõ hai nhánh. Với lỗi, form được hiển thị lại và giữ dữ liệu để admin tự quyết định. Với dữ liệu hợp lệ, combo được lưu, bảng cập nhật và admin có thể xem kết quả trước khi chọn tạo tiếp."
});

await sectionTransition(
  "PHẦN 06",
  "Kết luận",
  "Tổng hợp giá trị của hệ thống đối với khách mua và hoạt động vận hành cửa hàng.",
  ASSET + "/cover-fruitables.png",
  5,
  true
);

// 17 - Layout 06: angled geometric closing analysis
{
  const s = deck.slides.add(); base(s, 17, "06 / KẾT LUẬN");
  s.shapes.add({
    geometry: "parallelogram",
    position: { left: 0, top: 124, width: 410, height: 500 },
    fill: C.green,
    line: { style: "solid", fill: C.green, width: 0 },
  });
  text(s, "FRUITABLES", 100, 230, 260, 48, { size: 32, bold: true, color: C.white });
  text(s, "B2C", 100, 292, 240, 82, { size: 68, bold: true, color: C.white });
  text(s, "Một nền tảng chung cho mua hàng và vận hành.", 100, 404, 250, 96, { size: 20, bold: true, color: C.white });
  text(s, "Ba tính năng nối dữ liệu với trải nghiệm sử dụng", 450, 82, 760, 82, { size: 40, bold: true });
  text(s, "PHÍA KHÁCH HÀNG", 470, 218, 300, 28, { size: 17, bold: true, color: C.greenDark });
  text(s, "Chatbox hỗ trợ tìm thông tin nhanh hơn.\nGiá hiển thị đúng theo thời điểm.\nCombo giúp mua nhiều sản phẩm thuận tiện.", 470, 266, 330, 210, { size: 24, bold: true });
  box(s, 836, 218, 2, 300, C.green, C.green, 0);
  text(s, "PHÍA CỬA HÀNG", 878, 218, 300, 28, { size: 17, bold: true, color: C.greenDark });
  text(s, "Database tập trung và có thể truy vết.\nLuồng kiểm tra rõ ràng trước khi lưu.\nChủ động xử lý lỗi và tiếp tục tác vụ.", 878, 266, 320, 210, { size: 24, bold: true });
  note(s, "Kết luận bằng hai góc nhìn. Khách mua có trải nghiệm rõ ràng và nhất quán. Cửa hàng có dữ liệu tập trung, luồng kiểm tra và khả năng truy vết. Ba tính năng minh họa cách hệ thống kết nối hai phía.");
}

// 18 - Split-screen section closer
{
  const s = deck.slides.add();
  s.background.fill = C.greenSoft;
  await image(s, ASSET + "/cover-fruitables.png", 0, 0, 570, 720, { fit: "cover", frame: false, alt: "Rau củ quả tươi Fruitables" });
  box(s, 570, 0, 710, 720, C.greenSoft, C.greenSoft, 0);
  box(s, 1250, 86, 30, 120, C.green, C.green, 0);
  text(s, nextPageNumber(), 622, 56, 90, 36, { size: 24, bold: true, color: C.green });
  line(s, 728, 74, 160, C.green, 2);
  text(s, "CẢM ƠN", 622, 224, 560, 92, { size: 66, bold: true, color: C.ink });
  text(s, "Cảm ơn cô và các bạn đã lắng nghe.", 622, 342, 520, 50, { size: 25, color: C.muted });
  text(s, "Q&A", 622, 456, 220, 62, { size: 44, bold: true, color: C.greenDark });
  text(s, "FRUITABLES · THƯƠNG MẠI ĐIỆN TỬ RAU CỦ QUẢ B2C", 622, 642, 580, 24, { size: 13, bold: true, color: C.greenDark });
  note(s, "Cảm ơn người nghe và chuyển sang phần câu hỏi.");
}

await fs.mkdir(`${TMP}/slides`, { recursive: true });
await fs.mkdir(`${TMP}/layout`, { recursive: true });
for (const [i, slide] of deck.slides.items.entries()) {
  const stem = `slide-${String(i + 1).padStart(2, "0")}`;
  await writeBlob(`${TMP}/slides/${stem}.png`, await deck.export({ slide, format: "png", scale: 1 }));
  const layout = await slide.export({ format: "layout" });
  await fs.writeFile(`${TMP}/layout/${stem}.layout.json`, await layout.text());
}

await writeBlob(`${TMP}/preview/deck-montage.webp`, await deck.export({ format: "webp", montage: true, scale: 1 }));
const pptx = await PresentationFile.exportPptx(deck);
await pptx.save(OUT);
console.log(`Saved ${OUT}`);
console.log(`Slides ${deck.slides.items.length}`);
