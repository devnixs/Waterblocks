// A small stand-in for the Gate simulator, so that the browser tests need no real simulator.
import { createHash, createHmac, randomBytes } from 'node:crypto';
import { createServer } from 'node:http';

const port = Number(process.env.FAKE_GATE_SIMULATOR_PORT ?? 18084);
const host = process.env.FAKE_GATE_SIMULATOR_HOST ?? '127.0.0.1';
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

function sign(secret, timestamp) {
  const bodyHash = createHash('sha512').update('').digest('hex');
  const payload = `GET\n/api/v4/spot/fee\n\n${bodyHash}\n${timestamp}`;
  return createHmac('sha512', secret).update(payload).digest('hex');
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

function balanceOf(account, asset) {
  account.balances[asset] ??= { spot_available: 0, spot_locked: 0 };
  return account.balances[asset];
}

function accountReply(account) {
  const balances = Object.fromEntries(
    Object.entries(account.balances).map(([asset, balance]) => [
      asset,
      {
        spot_available: String(balance.spot_available),
        spot_locked: String(balance.spot_locked),
      },
    ]),
  );
  return { name: account.name, user_id: account.userId, balances };
}

function handleSpotFee(request, response) {
  const account = [...accounts.values()].find((item) => item.key === request.headers.key);
  if (!account || request.headers.sign !== sign(account.secret, request.headers.timestamp)) {
    sendJson(response, 401, { label: 'INVALID_KEY', message: 'Invalid key provided' });
    return;
  }

  sendJson(response, 200, { user_id: account.userId });
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

async function handleTestBalance(request, response, accountName, asset) {
  const body = await readJson(request);
  const balance = balanceOf(accounts.get(accountName), asset);
  balance.spot_available = Number(body.spot_available);
  sendJson(response, 200, accountReply(accounts.get(accountName)));
}

async function route(request, response) {
  const { pathname } = new URL(request.url, `http://${host}`);
  const parts = pathname.split('/').filter(Boolean).map(decodeURIComponent);

  if (request.method === 'GET' && pathname === '/api/v4/spot/fee') {
    handleSpotFee(request, response);
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
  console.log(`Fake Gate simulator listens on http://${host}:${port}`);
});
