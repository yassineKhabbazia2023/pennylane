import { describe, test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { EventEmitter } from 'node:events';

import {
  TARGET_METHOD,
  parseCSV,
  buildCsvOutput,
  createLogger,
  makeRequest,
  run,
  logReport,
} from './update-auth-method.js';

// ─── HELPERS ─────────────────────────────────────────────────────────────────

const noop = () => {};
const noopAsync = async () => {};

function fakeHttps({ statusCode = 200, chunks = ['{}'], networkError = null } = {}) {
  const state = { capturedOptions: null, written: [] };
  const lib = {
    request(options, callback) {
      state.capturedOptions = options;
      const res = new EventEmitter();
      res.statusCode = statusCode;
      const req = new EventEmitter();
      req.write = chunk => state.written.push(chunk);
      req.end   = () => {
        if (networkError) {
          req.emit('error', networkError);
        } else {
          callback(res);
          chunks.forEach(c => res.emit('data', c));
          res.emit('end');
        }
      };
      return req;
    },
    state,
  };
  return lib;
}

function makeCustomer(overrides = {}) {
  return {
    email:         'test@example.com',
    raison_sociale: 'Test SAS',
    id_akuiteo:    '12345',
    id_pennylane:  'uuid-001',
    statut:        'trouvé',
    ...overrides,
  };
}

// Silence console during test run
let _log, _err;
before(() => { _log = console.log; _err = console.error; console.log = noop; console.error = noop; });
after(()  => { console.log = _log; console.error = _err; });

// ─── parseCSV ────────────────────────────────────────────────────────────────

describe('parseCSV', () => {
  test('skips header and parses rows correctly', () => {
    const raw = 'email,raison_sociale,id_akuiteo,id_pennylane,statut\nfoo@bar.com,SCI TEST,999,uuid-1,trouvé\nbaz@qux.com,SAS XY,111,uuid-2,pas trouvé';
    const result = parseCSV(raw);
    assert.equal(result.length, 2);
    assert.deepEqual(result[0], { email: 'foo@bar.com', raison_sociale: 'SCI TEST', id_akuiteo: '999', id_pennylane: 'uuid-1', statut: 'trouvé' });
    assert.deepEqual(result[1], { email: 'baz@qux.com', raison_sociale: 'SAS XY',  id_akuiteo: '111', id_pennylane: 'uuid-2', statut: 'pas trouvé' });
  });

  test('returns empty array when only header is present', () => {
    assert.deepEqual(parseCSV('header\n'), []);
  });
});

// ─── buildCsvOutput ──────────────────────────────────────────────────────────

describe('buildCsvOutput', () => {
  test('returns only header for empty results', () => {
    assert.equal(
      buildCsvOutput([]),
      'email,raison_sociale,id_akuiteo,id_pennylane,current_method,action,http_status,resultat'
    );
  });

  test('generates correct header and data row', () => {
    const lines = buildCsvOutput([{
      email: 'a@b.com', raison_sociale: 'SAS A', id_akuiteo: '1', id_pennylane: 'p1',
      current_method: 'password', action: 'modifié', http_status: 200, resultat: 'ok',
    }]).split('\n');
    assert.equal(lines[0], 'email,raison_sociale,id_akuiteo,id_pennylane,current_method,action,http_status,resultat');
    assert.equal(lines[1], 'a@b.com,SAS A,1,p1,password,modifié,200,ok');
  });

  test('replaces null current_method and http_status with empty string', () => {
    const row = buildCsvOutput([{
      email: 'a@b.com', raison_sociale: 'X', id_akuiteo: '1', id_pennylane: 'p1',
      current_method: null, action: 'erreur', http_status: null, resultat: 'erreur',
    }]).split('\n')[1];
    assert.equal(row, 'a@b.com,X,1,p1,,erreur,,erreur');
  });
});

// ─── createLogger ────────────────────────────────────────────────────────────

describe('createLogger', () => {
  test('INFO calls console.log', () => {
    const calls = [];
    console.log = (...a) => calls.push(a);
    try {
      const { log } = createLogger();
      log('INFO', 'hello world');
      assert.equal(calls.length, 1);
      assert.ok(calls[0][0].includes('INFO'));
      assert.ok(calls[0][0].includes('hello world'));
    } finally {
      console.log = noop;
    }
  });

  test('ERROR calls console.error', () => {
    const calls = [];
    console.error = (...a) => calls.push(a);
    try {
      const { log } = createLogger();
      log('ERROR', 'something broke');
      assert.equal(calls.length, 1);
      assert.ok(calls[0][0].includes('ERROR'));
      assert.ok(calls[0][0].includes('something broke'));
    } finally {
      console.error = noop;
    }
  });

  test('accumulates lines', () => {
    const { log, lines } = createLogger();
    log('INFO',  'first');
    log('ERROR', 'second');
    assert.equal(lines.length, 2);
    assert.ok(lines[0].includes('first'));
    assert.ok(lines[1].includes('second'));
  });

  test('flush writes all lines to file via writeFn', () => {
    const { log, flush } = createLogger();
    log('INFO', 'line-a');
    log('INFO', 'line-b');
    let written;
    flush('/fake/path', (_, content) => { written = content; });
    assert.ok(written.includes('line-a'));
    assert.ok(written.includes('line-b'));
    assert.ok(written.endsWith('\n'));
  });
});

// ─── makeRequest ─────────────────────────────────────────────────────────────

describe('makeRequest', () => {
  test('GET — no body written, no Content-Type header', async () => {
    const fake = fakeHttps();
    const request = makeRequest('staging', 'tok', fake);
    const { status, body } = await request('GET', '/api/firm/v3/users/123');
    assert.equal(status, 200);
    assert.deepEqual(body, {});
    assert.equal(fake.state.written.length, 0);
    assert.equal(fake.state.capturedOptions.headers['Content-Type'], undefined);
  });

  test('PUT — body written, Content-Type set', async () => {
    const fake = fakeHttps({ chunks: ['{"id":"x"}'] });
    const request = makeRequest('app', 'tok', fake);
    await request('PUT', '/api/firm/v3/users/123', { authentication_method: 'kpmg-customer' });
    assert.equal(fake.state.written.length, 1);
    assert.equal(JSON.parse(fake.state.written[0]).authentication_method, 'kpmg-customer');
    assert.equal(fake.state.capturedOptions.headers['Content-Type'], 'application/json');
  });

  test('builds hostname from env param', async () => {
    const fake = fakeHttps();
    await makeRequest('staging', 'tok', fake)('GET', '/path');
    assert.equal(fake.state.capturedOptions.hostname, 'staging.pennylane.com');
  });

  test('sets Authorization header with token', async () => {
    const fake = fakeHttps();
    await makeRequest('app', 'secret-token', fake)('GET', '/path');
    assert.equal(fake.state.capturedOptions.headers.Authorization, 'Bearer secret-token');
  });

  test('resolves with parsed JSON body', async () => {
    const fake = fakeHttps({ chunks: ['{"key":', '"value"}'] });
    const { body } = await makeRequest('app', 'tok', fake)('GET', '/path');
    assert.deepEqual(body, { key: 'value' });
  });

  test('resolves with raw string when response is invalid JSON', async () => {
    const fake = fakeHttps({ chunks: ['not-json'] });
    const { body } = await makeRequest('app', 'tok', fake)('GET', '/path');
    assert.equal(body, 'not-json');
  });

  test('resolves with null when response body is empty', async () => {
    const fake = fakeHttps({ chunks: [] });
    const { body } = await makeRequest('app', 'tok', fake)('GET', '/path');
    assert.equal(body, null);
  });

  test('rejects on network error', async () => {
    const fake = fakeHttps({ networkError: new Error('ECONNREFUSED') });
    await assert.rejects(
      () => makeRequest('app', 'tok', fake)('GET', '/path'),
      { message: 'ECONNREFUSED' }
    );
  });
});

// ─── run ─────────────────────────────────────────────────────────────────────

describe('run', () => {
  function baseConfig(overrides = {}) {
    return {
      allCustomers:    [],
      whitelistEnabled: false,
      whitelist:       new Set(),
      targetMethod:    TARGET_METHOD,
      getUser:         async () => ({ status: 200, body: { authentication_method: 'password' } }),
      updateUser:      async () => ({ status: 200, body: {} }),
      log:             noop,
      sleep:           noopAsync,
      ...overrides,
    };
  }

  test('customer without id_pennylane → ignored', async () => {
    const { ignored, modified, alreadySet, failed } = await run(
      baseConfig({ allCustomers: [makeCustomer({ id_pennylane: '' })] })
    );
    assert.equal(ignored.length, 1);
    assert.equal(modified.length + alreadySet.length + failed.length, 0);
  });

  test('customer with statut !== trouvé → ignored', async () => {
    const { ignored } = await run(
      baseConfig({ allCustomers: [makeCustomer({ statut: 'pas trouvé' })] })
    );
    assert.equal(ignored.length, 1);
  });

  test('whitelist enabled — only matching IDs processed', async () => {
    const c1 = makeCustomer({ id_pennylane: 'uuid-001', email: 'a@a.com' });
    const c2 = makeCustomer({ id_pennylane: 'uuid-002', email: 'b@b.com' });
    const { modified } = await run(baseConfig({
      allCustomers:     [c1, c2],
      whitelistEnabled: true,
      whitelist:        new Set(['uuid-001']),
    }));
    assert.equal(modified.length, 1);
    assert.equal(modified[0].id_pennylane, 'uuid-001');
  });

  test('whitelist disabled — all customers processed', async () => {
    const c1 = makeCustomer({ id_pennylane: 'uuid-001', email: 'a@a.com' });
    const c2 = makeCustomer({ id_pennylane: 'uuid-002', email: 'b@b.com' });
    const { modified } = await run(baseConfig({ allCustomers: [c1, c2] }));
    assert.equal(modified.length, 2);
  });

  test('already kpmg-customer (direct field) → alreadySet', async () => {
    const { alreadySet, modified } = await run(baseConfig({
      allCustomers: [makeCustomer()],
      getUser: async () => ({ status: 200, body: { authentication_method: 'kpmg-customer' } }),
    }));
    assert.equal(alreadySet.length, 1);
    assert.equal(modified.length, 0);
    assert.equal(alreadySet[0].current_method, 'kpmg-customer');
  });

  test('already kpmg-customer (via user object) → alreadySet', async () => {
    const { alreadySet } = await run(baseConfig({
      allCustomers: [makeCustomer()],
      getUser: async () => ({ status: 200, body: { user: { authentication_method: 'kpmg-customer' } } }),
    }));
    assert.equal(alreadySet.length, 1);
    assert.equal(alreadySet[0].current_method, 'kpmg-customer');
  });

  test('null body → currentMethod = inconnu → goes to PUT → modified', async () => {
    const { modified } = await run(baseConfig({
      allCustomers: [makeCustomer()],
      getUser: async () => ({ status: 200, body: null }),
    }));
    assert.equal(modified.length, 1);
    assert.equal(modified[0].current_method, 'inconnu');
  });

  test('different method → PUT 200 → modified', async () => {
    const { modified, failed } = await run(baseConfig({
      allCustomers: [makeCustomer()],
      getUser:      async () => ({ status: 200, body: { authentication_method: 'password' } }),
      updateUser:   async () => ({ status: 200 }),
    }));
    assert.equal(modified.length, 1);
    assert.equal(failed.length, 0);
    assert.equal(modified[0].current_method, 'password');
  });

  test('different method → PUT 400 → failed', async () => {
    const { modified, failed } = await run(baseConfig({
      allCustomers: [makeCustomer()],
      getUser:      async () => ({ status: 200, body: { authentication_method: 'password' } }),
      updateUser:   async () => ({ status: 400 }),
    }));
    assert.equal(modified.length, 0);
    assert.equal(failed.length, 1);
  });

  test('GET returns non-200 → failed with action erreur-get', async () => {
    const { failed, results } = await run(baseConfig({
      allCustomers: [makeCustomer()],
      getUser: async () => ({ status: 404, body: null }),
    }));
    assert.equal(failed.length, 1);
    assert.equal(results[0].action, 'erreur-get');
    assert.equal(results[0].http_status, 404);
  });

  test('GET throws → failed with action erreur', async () => {
    const { failed, results } = await run(baseConfig({
      allCustomers: [makeCustomer()],
      getUser: async () => { throw new Error('ECONNREFUSED'); },
    }));
    assert.equal(failed.length, 1);
    assert.equal(results[0].action, 'erreur');
  });

  test('sleep called once per processed customer', async () => {
    let sleepCalls = 0;
    await run(baseConfig({
      allCustomers: [
        makeCustomer({ id_pennylane: 'u1', email: 'a@a.com' }),
        makeCustomer({ id_pennylane: 'u2', email: 'b@b.com' }),
      ],
      sleep: async () => { sleepCalls++; },
    }));
    assert.equal(sleepCalls, 2);
  });

  test('mixed scenario — correct categorisation of all actions', async () => {
    const c1 = makeCustomer({ id_pennylane: 'u1', email: 'a@a.com' }); // alreadySet
    const c2 = makeCustomer({ id_pennylane: 'u2', email: 'b@b.com' }); // modified
    const c3 = makeCustomer({ id_pennylane: 'u3', email: 'c@c.com' }); // erreur-get
    const c4 = makeCustomer({ id_pennylane: 'u4', email: 'd@d.com' }); // erreur-put
    const c5 = makeCustomer({ id_pennylane: '',   email: 'e@e.com', statut: 'pas trouvé' }); // ignored

    const getUser = async id => ({
      u1: { status: 200, body: { authentication_method: 'kpmg-customer' } },
      u2: { status: 200, body: { authentication_method: 'password' } },
      u3: { status: 404, body: null },
      u4: { status: 200, body: { authentication_method: 'password' } },
    })[id];

    const updateUser = async id => ({ u2: { status: 200 }, u4: { status: 500 } })[id];

    const { modified, alreadySet, failed, ignored, results } = await run(
      baseConfig({ allCustomers: [c1, c2, c3, c4, c5], getUser, updateUser })
    );

    assert.equal(modified.length,   1);
    assert.equal(alreadySet.length, 1);
    assert.equal(failed.length,     2);
    assert.equal(ignored.length,    1);
    assert.equal(results.length,    4);

    const actions = results.map(r => r.action);
    assert.ok(actions.includes('déjà-à-jour'));
    assert.ok(actions.includes('modifié'));
    assert.ok(actions.includes('erreur-get'));
    assert.ok(actions.includes('erreur-put'));
  });
});

// ─── logReport ───────────────────────────────────────────────────────────────

describe('logReport', () => {
  test('logs summary counts and item details for each section', () => {
    const calls = [];
    const log   = (_, msg) => calls.push(msg ?? '');
    const c = makeCustomer({ id_pennylane: 'p1', email: 'a@a.com', current_method: 'password' });

    logReport(log, {
      modified:     [c],
      alreadySet:   [],
      failed:       [],
      ignored:      [makeCustomer({ id_pennylane: '', statut: 'pas trouvé' })],
      runAt:        '2026-04-30T12:00:00.000Z',
      env:          'app',
      whitelistEnabled: true,
    });

    const out = calls.join('\n');
    assert.ok(out.includes('RAPPORT FINAL'));
    assert.ok(out.includes('ENV       : app'));
    assert.ok(out.includes('Whitelist : true'));
    assert.ok(out.includes('✔ Modifiés      : 1'));
    assert.ok(out.includes('~ Déjà à jour   : 0'));
    assert.ok(out.includes('✘ Erreurs       : 0'));
    assert.ok(out.includes('○ Ignorés (pas trouvés dans Pennylane) : 1'));
    assert.ok(out.includes('MODIFIÉS (1)'));
    assert.ok(out.includes('p1  a@a.com'));
  });

  test('logs (aucun) for every empty section', () => {
    const calls = [];
    logReport((_, msg) => calls.push(msg ?? ''), {
      modified: [], alreadySet: [], failed: [], ignored: [],
      runAt: '2026-04-30T00:00:00.000Z', env: 'staging', whitelistEnabled: false,
    });
    const anonCount = calls.filter(m => m.includes('(aucun)')).length;
    assert.equal(anonCount, 4);
  });
});
