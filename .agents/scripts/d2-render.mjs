#!/usr/bin/env node

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { spawnSync } from 'node:child_process';
import { findChrome } from './runtime-paths.mjs';

const args = process.argv.slice(2);
const source = args.find((arg) => !arg.startsWith('--'));
const wantPng = args.includes('--png');

if (!source) {
  console.error('Usage: node .agents/scripts/d2-render.mjs <file.d2> [--png]');
  process.exit(2);
}

const sourcePath = path.resolve(source);
if (!fs.existsSync(sourcePath)) {
  console.error(`Không thấy file: ${sourcePath}`);
  process.exit(2);
}
if (!/\.d2$/i.test(sourcePath)) {
  console.error(`File D2 phải có đuôi .d2: ${sourcePath}`);
  process.exit(2);
}

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '..', '..');
const localD2 = path.join(repoRoot, '.agents', 'tools', 'd2', process.platform === 'win32' ? 'd2.exe' : 'd2');
const d2 = process.env.D2_BIN || (fs.existsSync(localD2) ? localD2 : 'd2');
const svgPath = sourcePath.replace(/\.d2$/i, '.svg');
const result = spawnSync(d2, ['--layout', 'elk', '--theme', '1', '--pad', '40', sourcePath, svgPath], {
  encoding: 'utf8',
});

if (result.status !== 0 || !fs.existsSync(svgPath)) {
  console.error(`❌ D2 render thất bại.\n${result.stderr || result.error?.message || ''}`);
  process.exit(1);
}
console.log(`✅ SVG: ${svgPath}`);

if (!wantPng) process.exit(0);

const chrome = findChrome();
if (!chrome) {
  console.warn('⚠️ Không tìm thấy Chrome; SVG đã tạo nhưng bỏ qua PNG.');
  process.exit(0);
}

const svgText = fs.readFileSync(svgPath, 'utf8');
const viewBox = svgText.match(/viewBox="[\d.\-]+\s+[\d.\-]+\s+([\d.]+)\s+([\d.]+)"/i);
const width = Math.max(1, Math.ceil(Number(viewBox?.[1] || 1600)));
const height = Math.max(1, Math.ceil(Number(viewBox?.[2] || 2200)));
const pngPath = sourcePath.replace(/\.d2$/i, '.png');
const chromeResult = spawnSync(chrome, [
  '--headless=new',
  '--disable-gpu',
  `--screenshot=${pngPath}`,
  `--window-size=${width},${height}`,
  '--default-background-color=FFFFFFFF',
  pathToFileURL(svgPath).href,
], { encoding: 'utf8' });

if (chromeResult.status !== 0 || !fs.existsSync(pngPath)) {
  console.warn(`⚠️ Không tạo được PNG; SVG vẫn hợp lệ. ${chromeResult.stderr || ''}`);
  process.exit(0);
}
console.log(`✅ PNG: ${pngPath} (${width}x${height})`);
