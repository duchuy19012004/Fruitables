#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import zlib from 'node:zlib';

const args = process.argv.slice(2);
const source = args.find((arg) => !arg.startsWith('--'));
const wantPng = args.includes('--png');

if (!source) {
  console.error('Usage: node .agents/scripts/plantuml-render.mjs <file.puml> [--png]');
  process.exit(2);
}

const sourcePath = path.resolve(source);
if (!fs.existsSync(sourcePath)) {
  console.error(`Không thấy file: ${sourcePath}`);
  process.exit(2);
}
if (!/\.puml$/i.test(sourcePath)) {
  console.error(`File PlantUML phải có đuôi .puml: ${sourcePath}`);
  process.exit(2);
}

const alphabet = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_';
const encode6bit = (value) => alphabet[value & 0x3f];

function append3bytes(b1, b2, b3) {
  const c1 = b1 >> 2;
  const c2 = ((b1 & 0x3) << 4) | (b2 >> 4);
  const c3 = ((b2 & 0xf) << 2) | (b3 >> 6);
  const c4 = b3 & 0x3f;
  return encode6bit(c1) + encode6bit(c2) + encode6bit(c3) + encode6bit(c4);
}

function encodePlantUml(text) {
  const compressed = zlib.deflateRawSync(Buffer.from(text, 'utf8'), { level: 9 });
  let encoded = '';
  for (let i = 0; i < compressed.length; i += 3) {
    encoded += append3bytes(compressed[i], compressed[i + 1] ?? 0, compressed[i + 2] ?? 0);
  }
  return encoded;
}

async function download(format, encoded) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 20_000);
  try {
    const response = await fetch(`https://www.plantuml.com/plantuml/${format}/${encoded}`, {
      signal: controller.signal,
      headers: { 'User-Agent': 'Fruitables-Codex-Diagram-Skills' },
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return Buffer.from(await response.arrayBuffer());
  } finally {
    clearTimeout(timeout);
  }
}

const encoded = encodePlantUml(fs.readFileSync(sourcePath, 'utf8'));
const svgPath = sourcePath.replace(/\.puml$/i, '.svg');

try {
  const svg = await download('svg', encoded);
  const svgText = svg.toString('utf8');
  if (svg.length < 200 || !/<svg\b/i.test(svgText) || /Syntax Error|An error has occured|cannot find message/i.test(svgText)) {
    throw new Error('PlantUML returned an SVG containing a syntax error.');
  }
  fs.writeFileSync(svgPath, svg);
  console.log(`✅ SVG: ${svgPath}`);

  if (wantPng) {
    const pngPath = sourcePath.replace(/\.puml$/i, '.png');
    try {
      const png = await download('png', encoded);
      const pngSignature = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
      if (png.length < 500 || !png.subarray(0, 8).equals(pngSignature)) {
        throw new Error('PlantUML did not return a valid PNG.');
      }
      fs.writeFileSync(pngPath, png);
      console.log(`✅ PNG: ${pngPath}`);
    } catch (error) {
      if (fs.existsSync(pngPath)) fs.rmSync(pngPath, { force: true });
      console.warn(`⚠️ Không tạo được PNG; SVG vẫn hợp lệ. ${error.message}`);
    }
  }
} catch (error) {
  if (fs.existsSync(svgPath)) fs.rmSync(svgPath, { force: true });
  console.error(`❌ Render PlantUML thất bại: ${error.message}`);
  process.exit(1);
}
