import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import https                                      from 'node:https';
import { fileURLToPath }                          from 'node:url';

export const TARGET_METHOD = 'kpmg-customer';

// ─── PURE FUNCTIONS ──────────────────────────────────────────────────────────

function normalizeHeader(value) {
  return (value ?? '').toString().trim().toLowerCase();
}

function normalizeStatus(value) {
  return (value ?? '')
    .toString()
    .trim()
    .toLowerCase()
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '');
}

function isFoundStatus(value) {
  return normalizeStatus(value) === 'trouve';
}

export function parseCSV(raw) {
  const lines = raw.split('\n').map(l => l.trim()).filter(Boolean);
  if (lines.length <= 1) return [];

  const headers = lines[0].split(',').map(normalizeHeader);
  const findIdx = names => {
    for (const name of names) {
      const idx = headers.indexOf(name);
      if (idx !== -1) return idx;
    }
    return -1;
  };

  const emailIdx   = findIdx(['email', 'email user', 'email_user']);
  const raisonIdx  = findIdx(['raison_sociale', 'raison sociale']);
  const akuiteoIdx = findIdx(['id_akuiteo', 'id akuiteo']);
  const pennylaneIdx = findIdx(['id_pennylane', 'id pennylane']);
  const statutIdx  = findIdx(['statut', 'status']);

  return lines.slice(1).map(line => {
    const cols = line.split(',').map(c => c.trim());
    return {
      email:         emailIdx >= 0 ? cols[emailIdx] : '',
      raison_sociale: raisonIdx >= 0 ? cols[raisonIdx] : '',
      id_akuiteo:    akuiteoIdx >= 0 ? cols[akuiteoIdx] : '',
      id_pennylane:  pennylaneIdx >= 0 ? cols[pennylaneIdx] : '',
      statut:        statutIdx >= 0 ? cols[statutIdx] : '',
    };
  });
}

export function buildCsvOutput(results) {
  const header = 'email,id_pennylane,statut,current_method,action,http_status,resultat';
  const rows = results.map(r =>
    [r.email, r.id_pennylane, r.statut,
     r.current_method ?? '', r.action, r.http_status ?? '', r.resultat].join(',')
  );
  return [header, ...rows].join('\n');
}

// ─── LOGGER ──────────────────────────────────────────────────────────────────

export function createLogger() {
  const lines = [];
  function log(level, message) {
    const line = `[${new Date().toISOString()}] [${level.padEnd(5)}] ${message}`;
    console[level === 'ERROR' ? 'error' : 'log'](line);
    lines.push(line);
  }
  function flush(filepath, writeFn = writeFileSync) {
    writeFn(filepath, lines.join('\n') + '\n', 'utf-8');
  }
  return { log, flush, lines };
}

// ─── HTTP ────────────────────────────────────────────────────────────────────

export function makeRequest(env, token, httpsLib = https) {
  return function request(method, path, body = null) {
    const payload = body ? JSON.stringify(body) : null;
    return new Promise((resolve, reject) => {
      const options = {
        hostname: `${env}.pennylane.com`,
        path,
        method,
        headers: {
          Authorization: `Bearer ${token}`,
          Accept:        'application/json',
          ...(payload && {
            'Content-Type':   'application/json',
            'Content-Length': Buffer.byteLength(payload),
          }),
        },
      };
      const req = httpsLib.request(options, res => {
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
      if (payload) req.write(payload);
      req.end();
    });
  };
}

// ─── CORE LOGIC ──────────────────────────────────────────────────────────────

export async function run({
  allCustomers,
  whitelistEnabled,
  whitelist,
  targetMethod = TARGET_METHOD,
  getUser,
  updateUser,
  log,
  sleep,
}) {
  const ignored    = allCustomers
    .filter(c => !c.id_pennylane || !isFoundStatus(c.statut))
    .map(c => ({ ...c, id_pennylane: isFoundStatus(c.statut) ? c.id_pennylane : '' }));
  let   processable = allCustomers.filter(c =>  c.id_pennylane && isFoundStatus(c.statut));

  if (whitelistEnabled) {
    processable = processable.filter(c => whitelist.has(c.id_pennylane));
    log('INFO', `[WHITELIST] Mode actif — ${processable.length} ID(s) autorisé(s)`);
    whitelist.forEach(id => log('INFO', `  → whitelist: ${id}`));
  } else {
    log('INFO', `[FULL RUN] ${processable.length} customers à traiter`);
  }

  const modified = [], alreadySet = [], failed = [], results = [];
  let done = 0;

  for (const c of processable) {
    const prefix = `[${++done}/${processable.length}] ${c.email} (${c.id_pennylane})`;
    try {
      const { status: getStatus, body: getBody } = await getUser(c.id_pennylane);

      if (getStatus !== 200) {
        log('ERROR', `${prefix} → GET échoué HTTP ${getStatus}`);
        failed.push(c);
        results.push({ ...c, current_method: '', action: 'erreur-get', http_status: getStatus, resultat: 'erreur' });
        await sleep();
        continue;
      }

      const currentMethod = getBody?.authentication_method
        ?? getBody?.user?.authentication_method
        ?? 'inconnu';

      if (currentMethod === targetMethod) {
        log('INFO', `${prefix} → déjà "${targetMethod}", ignoré`);
        alreadySet.push({ ...c, current_method: currentMethod });
        results.push({ ...c, current_method: currentMethod, action: 'déjà-à-jour', http_status: getStatus, resultat: 'ignoré' });
        await sleep();
        continue;
      }

      log('INFO', `${prefix} → méthode actuelle "${currentMethod}" → mise à jour vers "${targetMethod}"`);

      const { status: putStatus } = await updateUser(c.id_pennylane);
      const ok = putStatus >= 200 && putStatus < 300;

      if (ok) {
        log('INFO', `${prefix} → PUT OK (HTTP ${putStatus})`);
        modified.push({ ...c, current_method: currentMethod });
        results.push({ ...c, current_method: currentMethod, action: 'modifié', http_status: putStatus, resultat: 'ok' });
      } else {
        log('ERROR', `${prefix} → PUT échoué HTTP ${putStatus}`);
        failed.push({ ...c, current_method: currentMethod });
        results.push({ ...c, current_method: currentMethod, action: 'erreur-put', http_status: putStatus, resultat: 'erreur' });
      }
    } catch (err) {
      log('ERROR', `${prefix} → ${err.message}`);
      failed.push(c);
      results.push({ ...c, current_method: '', action: 'erreur', http_status: '', resultat: 'erreur' });
    }

    await sleep();
  }

  return { modified, alreadySet, failed, ignored, results };
}

// ─── RAPPORT ─────────────────────────────────────────────────────────────────

export function logReport(log, { modified, alreadySet, failed, ignored, runAt, env, whitelistEnabled }) {
  function section(title, items, showId = true) {
    log('INFO', ``);
    log('INFO', `  ┌─ ${title} (${items.length})`);
    if (items.length === 0) {
      log('INFO', `  │  (aucun)`);
    } else {
      items.forEach(c => {
        if (showId) {
          log('INFO', `  │  ${c.id_pennylane ?? ''}  ${c.email}`);
        } else {
          log('INFO', `  │  ${c.email}`);
        }
      });
    }
    log('INFO', `  └${'─'.repeat(60)}`);
  }

  log('INFO', ``);
  log('INFO', `╔══════════════════════════════════════════════════════════╗`);
  log('INFO', `  RAPPORT FINAL — ${runAt}`);
  log('INFO', `  ENV       : ${env}`);
  log('INFO', `  Whitelist : ${whitelistEnabled}`);
  log('INFO', `  ✔ Modifiés      : ${modified.length}`);
  log('INFO', `  ~ Déjà à jour   : ${alreadySet.length}`);
  log('INFO', `  ✘ Erreurs       : ${failed.length}`);
  log('INFO', `  ○ Ignorés (pas trouvés dans Pennylane) : ${ignored.length}`);
  log('INFO', `╚══════════════════════════════════════════════════════════╝`);

  section('MODIFIÉS', modified);
  section('DÉJÀ À JOUR (non touchés)', alreadySet);
  section('ERREURS', failed);
  section('IGNORÉS — pas trouvés dans Pennylane', ignored, false);
}

// ─── ENTRY POINT ─────────────────────────────────────────────────────────────

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const ENV   = process.env.PL_ENV   || 'app';
  const TOKEN = process.env.PL_TOKEN || 'xxxx';

  const WHITELIST_ENABLED = false;
  const WHITELIST         = new Set(['b9e29ee9-c0f1-4fc8-be33-1a0898ff76a9']);
  const CUSTOMERS_FILE    = './users-ids-output-2026-05-19T14-44-40.csv';
  const OUTPUT_FILE       = './update-results.csv';
  const DELAY_MS          = 500;

  const RUN_AT   = new Date();
  const ts       = RUN_AT.toISOString().replaceAll(/[:.]/g, '-').slice(0, 19);
  mkdirSync('./logs', { recursive: true });
  const LOG_FILE = `./logs/run-${ts}.log`;

  const { log, flush } = createLogger();

  process.on('exit', () => flush(LOG_FILE));
  process.on('uncaughtException', err => { log('ERROR', `Uncaught: ${err.message}`); flush(LOG_FILE); process.exit(1); });

  const request    = makeRequest(ENV, TOKEN);
  const getUser    = id => request('GET', `/api/firm/v3/users/${id}`);
  const updateUser = id => request('PUT', `/api/firm/v3/users/${id}`, { authentication_method: TARGET_METHOD });

  log('INFO', `=== Démarrage update-auth-method (${ENV}) ===`);
  log('INFO', `Fichier source : ${CUSTOMERS_FILE}`);

  const allCustomers = parseCSV(readFileSync(CUSTOMERS_FILE, 'utf-8'));
  log('INFO', `Total lignes : ${allCustomers.length}`);

  const { modified, alreadySet, failed, ignored, results } = await run({
    allCustomers,
    whitelistEnabled: WHITELIST_ENABLED,
    whitelist:        WHITELIST,
    getUser,
    updateUser,
    log,
    sleep: () => new Promise(r => setTimeout(r, DELAY_MS)),
  });

  writeFileSync(OUTPUT_FILE, buildCsvOutput(results), 'utf-8');

  logReport(log, {
    modified, alreadySet, failed, ignored,
    runAt: RUN_AT.toISOString(), env: ENV, whitelistEnabled: WHITELIST_ENABLED,
  });

  log('INFO', `Fichiers générés : ${OUTPUT_FILE} | ${LOG_FILE}`);
}
