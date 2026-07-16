import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

export function findChrome() {
  const candidates = [
    process.env.PUPPETEER_EXECUTABLE_PATH,
    process.platform === 'win32' && process.env.PROGRAMFILES
      ? path.join(process.env.PROGRAMFILES, 'Google', 'Chrome', 'Application', 'chrome.exe')
      : null,
    process.platform === 'win32' && process.env['PROGRAMFILES(X86)']
      ? path.join(process.env['PROGRAMFILES(X86)'], 'Google', 'Chrome', 'Application', 'chrome.exe')
      : null,
    process.platform === 'win32' && process.env.LOCALAPPDATA
      ? path.join(process.env.LOCALAPPDATA, 'Google', 'Chrome', 'Application', 'chrome.exe')
      : null,
    '/Applications/Google Chrome.app/Contents/MacOS/Google Chrome',
    '/usr/bin/google-chrome',
    '/usr/bin/chromium',
  ].filter(Boolean);
  const direct = candidates.find((candidate) => fs.existsSync(candidate));
  if (direct) return direct;

  const cacheRoots = [
    path.join(os.homedir(), '.puppeteer-cache', 'chrome'),
    path.join(os.homedir(), '.cache', 'puppeteer', 'chrome'),
    path.join(os.homedir(), '.cache', 'puppeteer', 'chrome-headless-shell'),
  ];
  for (const root of cacheRoots) {
    if (!fs.existsSync(root)) continue;
    const queue = [root];
    while (queue.length) {
      const current = queue.shift();
      for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
        const candidate = path.join(current, entry.name);
        if (entry.isDirectory()) queue.push(candidate);
        if (entry.isFile() && /^(chrome|chrome-headless-shell|chrome\.exe)$/i.test(entry.name)) return candidate;
      }
    }
  }
  return null;
}
