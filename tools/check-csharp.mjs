// Static sanity check for the Unity C# sources.
// Not a compiler: a small tokenizer skips comments and string/char literals, then verifies
// bracket balance and required namespace declarations. Catches truncated or corrupted writes.
import fs from 'node:fs';
import path from 'node:path';

const root = process.argv[2] ?? 'ProjectInfinity/Assets';
const files = [];

function walk(dir) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full);
    else if (entry.name.endsWith('.cs')) files.push(full);
  }
}
walk(root);

/** Removes comments and literals, leaving only code characters. */
function codeOnly(source) {
  let out = '';
  let i = 0;
  const n = source.length;

  while (i < n) {
    const c = source[i];
    const next = source[i + 1];

    if (c === '/' && next === '/') {
      while (i < n && source[i] !== '\n') i += 1;
      continue;
    }
    if (c === '/' && next === '*') {
      i += 2;
      while (i < n && !(source[i] === '*' && source[i + 1] === '/')) i += 1;
      i += 2;
      continue;
    }
    if (c === '"') {
      i += 1;
      while (i < n && source[i] !== '"') {
        if (source[i] === '\\') i += 1;
        i += 1;
      }
      i += 1;
      out += '""';
      continue;
    }
    if (c === "'") {
      i += 1;
      while (i < n && source[i] !== "'") {
        if (source[i] === '\\') i += 1;
        i += 1;
      }
      i += 1;
      out += "''";
      continue;
    }

    out += c;
    i += 1;
  }

  return out;
}

let failures = 0;

for (const file of files) {
  const raw = fs.readFileSync(file, 'utf8');
  const code = codeOnly(raw);

  const stack = [];
  const pairs = { '}': '{', ')': '(', ']': '[' };
  let line = 1;
  let broken = false;

  for (const ch of code) {
    if (ch === '\n') line += 1;
    if ('{(['.includes(ch)) stack.push(ch);
    else if ('})]'.includes(ch)) {
      if (stack.pop() !== pairs[ch]) {
        failures += 1;
        console.log(`${file}:${line}: unbalanced '${ch}'`);
        broken = true;
        break;
      }
    }
  }

  if (!broken && stack.length) {
    failures += 1;
    console.log(`${file}: ${stack.length} unclosed bracket(s)`);
  }

  if (!/namespace ProjectInfinity/.test(raw)) {
    failures += 1;
    console.log(`${file}: missing ProjectInfinity namespace`);
  }

  if (raw.length < 120) {
    failures += 1;
    console.log(`${file}: suspiciously short (${raw.length} chars)`);
  }
}

console.log(`checked ${files.length} C# files, ${failures} problem(s)`);
process.exit(failures ? 1 : 0);
