import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import https                                      from 'node:https';
import { fileURLToPath }                          from 'node:url';

// ─── CONFIG ───────────────────────────────────────────────────────────────────

const ENV        = process.env.PL_ENV   || 'app';
const TOKEN      = process.env.PL_TOKEN || 'xxxx';
const INPUT_FILE = process.argv[2] || './list email.txt';
const DELAY_MS   = 300;

// ─── LOGGER ──────────────────────────────────────────────────────────────────

function createLogger() {
  const lines = [];
  function log(level, message) {
    const line = `[${new Date().toISOString()}] [${level.padEnd(5)}] ${message}`;
    console[level === 'ERROR' ? 'error' : 'log'](line);
    lines.push(line);
  }
  function flush(filepath) {
    writeFileSync(filepath, lines.join('\n') + '\n', 'utf-8');
  }
  return { log, flush };
}

// ─── HTTP ────────────────────────────────────────────────────────────────────

function request(method, path) {
  return new Promise((resolve, reject) => {
    const options = {
      hostname: `${ENV}.pennylane.com`,
      path,
      method,
      headers: {
        Authorization: `Bearer ${TOKEN}`,
        Accept:        'application/json',
      },
    };
    const req = https.request(options, res => {
      let data = '';
      res.on('data', chunk => (data += chunk));
      res.on('end', () => {
        try {
          resolve({ status: res.statusCode, body: data ? JSON.parse(data) : null });
        } catch {
          resolve({ status: res.statusCode, body: data });
        }
      });
    });
    req.on('error', reject);
    req.end();
  });
}

// ─── CORE ────────────────────────────────────────────────────────────────────

function parseEmailList(raw) {
  const lines = raw.split('\n').map(l => l.trim()).filter(Boolean);
  // Skip header line if present (e.g. "Email user")
  const emails = lines.filter(l => l.includes('@'));
  // Deduplicate while preserving first-seen order
  return [...new Set(emails.map(e => e.toLowerCase()))];
}

async function fetchUserId(email) {
  const filter = JSON.stringify([{ field: 'email', operator: 'eq', value: email }]);
  const path   = `/api/firm/v3/users?filter=${encodeURIComponent(filter)}`;
  return request('GET', path);
}

// ─── ENTRY POINT ─────────────────────────────────────────────────────────────

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const { log, flush } = createLogger();

  const RUN_AT = new Date();
  const ts     = RUN_AT.toISOString().replaceAll(/[:.]/g, '-').slice(0, 19);
  mkdirSync('./logs', { recursive: true });
  const LOG_FILE = `./logs/fetch-user-ids-${ts}.log`;

  process.on('exit',             () => flush(LOG_FILE));
  process.on('uncaughtException', err => { log('ERROR', `Uncaught: ${err.message}`); flush(LOG_FILE); process.exit(1); });

  log('INFO', `=== fetch-user-ids démarrage (${ENV}) ===`);
  log('INFO', `Fichier source : ${INPUT_FILE}`);

  const emails = parseEmailList(readFileSync(INPUT_FILE, 'utf-8'));
  log('INFO', `${emails.length} email(s) uniques à traiter`);

  const results = [];
  let done = 0;

  for (const email of emails) {
    const prefix = `[${++done}/${emails.length}] ${email}`;
    try {
      const { status, body } = await fetchUserId(email);

      if (status !== 200) {
        log('ERROR', `${prefix} → HTTP ${status}`);
        results.push({ email, id_pennylane: '', statut: `erreur-http-${status}` });
      } else {
        // API returns { users: [...] } or { data: [...] } depending on version
        const users = body?.users ?? body?.data ?? [];
        if (users.length === 0) {
          log('WARN',  `${prefix} → non trouvé`);
          results.push({ email, id_pennylane: '', statut: 'non trouvé' });
        } else {
          const user = users[0];
          const id   = user.id ?? user.source_id ?? '';
          log('INFO',  `${prefix} → id=${id}`);
          results.push({ email, id_pennylane: id, statut: 'trouvé' });
        }
      }
    } catch (err) {
      log('ERROR', `${prefix} → ${err.message}`);
      results.push({ email, id_pennylane: '', statut: 'erreur' });
    }

    await new Promise(r => setTimeout(r, DELAY_MS));
  }

  // ─── Écriture CSV ──────────────────────────────────────────────────────────
  const header = 'email,id_pennylane,statut';
  const rows   = results.map(r => [r.email, r.id_pennylane, r.statut].join(','));
  const csv    = [header, ...rows].join('\n');

  const OUTPUT_FILE = `./users-ids-output-${ts}.csv`;
  writeFileSync(OUTPUT_FILE, csv, 'utf-8');

  // ─── Résumé ────────────────────────────────────────────────────────────────
  const found   = results.filter(r => r.statut === 'trouvé').length;
  const missing = results.filter(r => r.statut === 'non trouvé').length;
  const errors  = results.filter(r => r.statut.startsWith('erreur')).length;

  log('INFO', ``);
  log('INFO', `╔══════════════════════════════════════════════════════════╗`);
  log('INFO', `  RÉSULTAT FINAL`);
  log('INFO', `  ✔ Trouvés   : ${found}`);
  log('INFO', `  ~ Absents   : ${missing}`);
  log('INFO', `  ✘ Erreurs   : ${errors}`);
  log('INFO', `  Fichier CSV : ${OUTPUT_FILE}`);
  log('INFO', `╚══════════════════════════════════════════════════════════╝`);
}
