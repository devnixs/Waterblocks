// A small stand-in for an exchange simulator, so that the browser tests need no real simulator.
// It serves the common simulator admin API that Waterblocks calls, plus /_test helpers for the tests.
import { createHmac, randomBytes } from 'node:crypto';
import { createServer } from 'node:http';

const port = Number(process.env.FAKE_EXCHANGE_SIMULATOR_PORT ?? 18084);
const host = process.env.FAKE_EXCHANGE_SIMULATOR_HOST ?? '127.0.0.1';
const maxProofAgeSeconds = 60;
const accounts = new Map();
const events = new Map();
let nextUserId = 30000001;

function sendJson(response, status, body) {
  response.writeHead(status, { 'Content-Type': 'application/json' });
  response.end(JSON.stringify(body));
}

async function readJson(request) {
  const chunks = [];
  for await (const chunk of request) {
    chunks.push(chunk);
  }

  return JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}');
}

function keyProof(secret, key, timestamp) {
  return createHmac('sha256', secret).update(`KEY_VERIFY\n${key}\n${timestamp}`).digest('hex');
}

function createAccount(name) {
  const account = {
    name,
    userId: nextUserId,
    key: randomBytes(16).toString('hex'),
    secret: randomBytes(32).toString('hex'),
    balances: {},
  };
  nextUserId += 1;
  accounts.set(name, account);
  return account;
}

function balanceOf(account, currency) {
  account.balances[currency] ??= { spot_available: 0, spot_locked: 0 };
  return account.balances[currency];
}

function accountReply(account) {
  const balances = Object.fromEntries(
    Object.entries(account.balances).map(([currency, balance]) => [
      currency,
      {
        spot_available: String(balance.spot_available),
        spot_locked: String(balance.spot_locked),
      },
    ]),
  );
  return { name: account.name, user_id: account.userId, balances };
}

async function handleKeyVerify(request, response) {
  const body = await readJson(request);
  const account = [...accounts.values()].find((item) => item.key === body.key);
  const age = Math.abs(Date.now() / 1000 - Number(body.timestamp));
  if (!account || !(age <= maxProofAgeSeconds) || body.signature !== keyProof(account.secret, body.key, body.timestamp)) {
    sendJson(response, 422, { error: 'the key or its proof is not valid' });
    return;
  }

  sendJson(response, 200, { name: account.name, user_id: account.userId });
}

function applyEvent(body) {
  const account = accounts.get(body.account);
  const amount = Number(body.amount);
  const balance = balanceOf(account, body.asset);
  if (body.type === 'add') {
    balance.spot_available += amount;
    return { status: 'applied' };
  }

  if (balance.spot_available < amount) {
    return { status: 'rejected', reason: 'the amount is above the available balance' };
  }

  balance.spot_available -= amount;
  return { status: 'applied' };
}

async function handleCollateralEvent(request, response) {
  const body = await readJson(request);
  if (!accounts.has(body.account)) {
    sendJson(response, 404, { error: 'account not found' });
    return;
  }

  const existing = events.get(body.event_id);
  if (existing) {
    sendJson(response, 200, { ...existing, repeated: true });
    return;
  }

  const event = { ...body, ...applyEvent(body) };
  events.set(body.event_id, event);
  sendJson(response, 201, event);
}

async function handleTestAccount(request, response) {
  const body = await readJson(request);
  const account = createAccount(body.name);
  sendJson(response, 201, { name: account.name, key: account.key, secret: account.secret });
}

async function handleTestBalance(request, response, accountName, currency) {
  const body = await readJson(request);
  const balance = balanceOf(accounts.get(accountName), currency);
  balance.spot_available = Number(body.spot_available);
  sendJson(response, 200, accountReply(accounts.get(accountName)));
}

async function route(request, response) {
  const { pathname } = new URL(request.url, `http://${host}`);
  const parts = pathname.split('/').filter(Boolean).map(decodeURIComponent);

  if (request.method === 'POST' && pathname === '/_api/keys/verify') {
    await handleKeyVerify(request, response);
  } else if (request.method === 'GET' && pathname === '/_api/accounts') {
    sendJson(response, 200, [...accounts.values()].map(accountReply));
  } else if (request.method === 'GET' && parts[0] === '_api' && parts[1] === 'accounts' && parts.length === 3) {
    const account = accounts.get(parts[2]);
    sendJson(response, account ? 200 : 404, account ? accountReply(account) : { error: 'account not found' });
  } else if (request.method === 'POST' && pathname === '/_api/collateral/events') {
    await handleCollateralEvent(request, response);
  } else if (request.method === 'POST' && pathname === '/_test/accounts') {
    await handleTestAccount(request, response);
  } else if (request.method === 'PUT' && parts[0] === '_test' && parts[1] === 'accounts' && parts[3] === 'balances') {
    await handleTestBalance(request, response, parts[2], parts[4]);
  } else {
    sendJson(response, 404, { error: 'not found' });
  }
}

createServer((request, response) => {
  route(request, response).catch((error) => sendJson(response, 500, { error: error.message }));
}).listen(port, host, () => {
  console.log(`Fake exchange simulator listens on http://${host}:${port}`);
});
