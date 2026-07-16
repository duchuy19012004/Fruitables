#!/usr/bin/env node

import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '..', '..');
const skillRoot = path.join(repoRoot, '.agents', 'skills');
const skills = [
  'activity', 'activity-swimlane', 'bpmn', 'd2-activity', 'd2-architect',
  'd2-erd', 'dbdiagram', 'erd', 'sequence', 'state', 'usecase-diagram',
];
const failures = [];

for (const skill of skills) {
  const file = path.join(skillRoot, skill, 'SKILL.md');
  if (!fs.existsSync(file)) {
    failures.push(`missing ${file}`);
    continue;
  }
  const content = fs.readFileSync(file, 'utf8');
  const frontmatter = content.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!frontmatter || !/^name:\s*.+$/m.test(frontmatter[1]) || !/^description:\s*.+$/m.test(frontmatter[1])) {
    failures.push(`invalid frontmatter in ${file}`);
  }
  if (content.includes('.claude/')) failures.push(`legacy .claude path in ${file}`);
}

const reviewer = path.join(repoRoot, '.codex', 'agents', 'diagram-reviewer.toml');
if (!fs.existsSync(reviewer)) failures.push(`missing ${reviewer}`);

const commands = [
  ['node', ['--version'], 'Node'],
  [path.join(repoRoot, '.agents', 'tools', 'd2', process.platform === 'win32' ? 'd2.exe' : 'd2'), ['version'], 'D2'],
];
for (const [command, args, label] of commands) {
  const result = spawnSync(command, args, { encoding: 'utf8' });
  if (result.status !== 0) failures.push(`${label} unavailable: ${result.error?.message || result.stderr}`);
  else console.log(`✅ ${label}: ${(result.stdout || result.stderr).trim()}`);
}

const tempRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'fruitables-diagram-verify-'));
try {
  const mermaidFile = path.join(tempRoot, 'sample.md');
  fs.writeFileSync(mermaidFile, '## Sample\n\n```mermaid\nflowchart LR\n  A[Start] --> B[Done]\n```\n');
  const mermaid = spawnSync(process.execPath, [path.join(scriptDir, 'mermaid-verify.mjs'), '--file', mermaidFile], { encoding: 'utf8' });
  if (mermaid.status !== 0) failures.push(`Mermaid verification failed: ${mermaid.stderr || mermaid.stdout}`);
  else console.log('✅ Mermaid compile test');

  const pumlFile = path.join(tempRoot, 'sample.puml');
  fs.writeFileSync(pumlFile, '@startuml\nAlice -> Bob: hello\n@enduml\n');
  const plantuml = spawnSync(process.execPath, [path.join(scriptDir, 'plantuml-render.mjs'), pumlFile], { encoding: 'utf8' });
  if (plantuml.status !== 0) failures.push(`PlantUML verification failed: ${plantuml.stderr || plantuml.stdout}`);
  else console.log('✅ PlantUML render test');

  const d2File = path.join(tempRoot, 'sample.d2');
  fs.writeFileSync(d2File, 'start -> done\n');
  const d2 = spawnSync(process.execPath, [path.join(scriptDir, 'd2-render.mjs'), d2File], { encoding: 'utf8' });
  if (d2.status !== 0) failures.push(`D2 verification failed: ${d2.stderr || d2.stdout}`);
  else console.log('✅ D2 render test');

  const wrongExtension = path.join(tempRoot, 'wrong-extension.txt');
  fs.writeFileSync(wrongExtension, 'must not be overwritten');
  const wrongPlantUml = spawnSync(process.execPath, [path.join(scriptDir, 'plantuml-render.mjs'), wrongExtension], { encoding: 'utf8' });
  const wrongD2 = spawnSync(process.execPath, [path.join(scriptDir, 'd2-render.mjs'), wrongExtension], { encoding: 'utf8' });
  if (wrongPlantUml.status === 0 || wrongD2.status === 0 || fs.readFileSync(wrongExtension, 'utf8') !== 'must not be overwritten') {
    failures.push('Renderer extension guard failed.');
  } else console.log('✅ Renderer extension guards');

  const dbmlFile = path.join(tempRoot, 'sample.dbml');
  const sqlFile = path.join(tempRoot, 'sample.sql');
  fs.writeFileSync(dbmlFile, 'Table users {\n  id integer [pk]\n}\n');
  const dbmlCli = path.join(repoRoot, '.agents', 'node_modules', '.bin', process.platform === 'win32' ? 'dbml2sql.cmd' : 'dbml2sql');
  const dbml = spawnSync(dbmlCli, [dbmlFile, '--postgres', '-o', sqlFile], { encoding: 'utf8', shell: process.platform === 'win32' });
  if (dbml.status !== 0 || !fs.existsSync(sqlFile)) failures.push(`DBML verification failed: ${dbml.stderr || dbml.stdout}`);
  else console.log('✅ DBML export test');

  const bpmnDir = path.join(tempRoot, 'verify-feature', 'bpmn');
  fs.mkdirSync(bpmnDir, { recursive: true });
  fs.writeFileSync(path.join(bpmnDir, 'verify.ir.json'), JSON.stringify({
    process: { id: 'Process_verify', title: 'Verify BPMN engine' },
    lanes: [{ id: 'Lane_system', name: 'System' }],
    nodes: [
      { id: 'Start_1', kind: 'start', lane: 'Lane_system', name: 'Start' },
      { id: 'Task_1', kind: 'task', lane: 'Lane_system', name: 'Process request' },
      { id: 'End_1', kind: 'end', lane: 'Lane_system', name: 'Done' },
    ],
    flows: [
      { id: 'Flow_1', src: 'Start_1', tgt: 'Task_1' },
      { id: 'Flow_2', src: 'Task_1', tgt: 'End_1' },
    ],
  }, null, 2));
  const bpmnBuild = path.join(skillRoot, 'bpmn', 'engine', 'bpmn-build.mjs');
  const bpmnGenerate = spawnSync(process.execPath, [bpmnBuild, '--dir', '.'], { cwd: bpmnDir, encoding: 'utf8' });
  const editorFile = path.join(bpmnDir, 'verify-feature-bpmn-editor.html');
  if (bpmnGenerate.status !== 0 || !fs.existsSync(editorFile)) {
    failures.push(`BPMN build/editor test failed: ${bpmnGenerate.stderr || bpmnGenerate.stdout}`);
  } else {
    const bpmnVerify = spawnSync(process.execPath, [bpmnBuild, '--dir', '.', '--verify'], { cwd: bpmnDir, encoding: 'utf8' });
    if (bpmnVerify.status !== 0) failures.push(`BPMN layout verification failed: ${bpmnVerify.stderr || bpmnVerify.stdout}`);
    else console.log('✅ BPMN build/editor/layout/verify test');
  }
} finally {
  const base = path.resolve(os.tmpdir());
  const resolved = path.resolve(tempRoot);
  if (resolved.startsWith(base) && path.basename(resolved).startsWith('fruitables-diagram-verify-')) {
    fs.rmSync(resolved, { recursive: true, force: true });
  }
}

if (failures.length) {
  console.error('\nInstallation verification failed:');
  failures.forEach((failure) => console.error(`- ${failure}`));
  process.exit(1);
}

console.log(`\n✅ ${skills.length} diagram skills and all five render/validation engines are ready.`);
