import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const errorRate = new Rate('errors');
const BASE_URL = (__ENV.BASE_URL || 'http://localhost:5270').replace(/\/$/, '');
const CUSTOMER_PASSWORD = __ENV.CUSTOMER_PASSWORD || 'LoadTest-Password-123!';
const ADMIN_USERNAME = __ENV.ADMIN_USERNAME;
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD;
const THINK_TIME_SECONDS = Number(__ENV.THINK_TIME_SECONDS || 1);

export const options = {
  stages: [
    { duration: '30s', target: 5 },
    { duration: '1m', target: 10 },
    { duration: '2m', target: 20 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000'],
    errors: ['rate<0.01'],
    http_req_failed: ['rate<0.01'],
  },
};

let vuState;

export function setup() {
  if (!ADMIN_USERNAME || !ADMIN_PASSWORD) {
    throw new Error('Set ADMIN_USERNAME and ADMIN_PASSWORD to an administrator login.');
  }
  if (CUSTOMER_PASSWORD.length < 12) {
    throw new Error('CUSTOMER_PASSWORD must contain at least 12 characters.');
  }

  const response = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ username: ADMIN_USERNAME, password: ADMIN_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  if (response.status !== 200) {
    throw new Error(`Administrator login failed with HTTP ${response.status}: ${response.body}`);
  }

  const login = response.json();
  if (!login.accessToken) {
    throw new Error('Administrator login response did not contain accessToken.');
  }

  const healthResponse = http.get(`${BASE_URL}/health`);
  if (healthResponse.status !== 200) {
    throw new Error(`Health check failed with HTTP ${healthResponse.status}: ${healthResponse.body}`);
  }

  const now = Date.now();
  return {
    adminToken: login.accessToken,
    runId: `${now}-${Math.floor(Math.random() * 1_000_000)}`,
    fromUtc: new Date(now - 24 * 60 * 60 * 1000).toISOString(),
    toUtc: new Date(now + 24 * 60 * 60 * 1000).toISOString(),
  };
}

export default function (data) {
  if (!vuState) {
    vuState = initializeVirtualUser(data);
  }

  const customerHeaders = {
    Authorization: `Bearer ${vuState.customerToken}`,
    'Content-Type': 'application/json',
  };
  const adminHeaders = {
    Authorization: `Bearer ${data.adminToken}`,
    'Content-Type': 'application/json',
  };

  // Customer and account reads, profile update, and account-status management.
  expectStatus('GET customer page', http.get(
    `${BASE_URL}/api/customers?pageNumber=1&pageSize=20`, { headers: customerHeaders }), 200);
  expectStatus('GET customer profile', http.get(
    `${BASE_URL}/api/customers/${vuState.customerId}`, { headers: customerHeaders }), 200);
  expectStatus('PUT own customer profile', http.put(
    `${BASE_URL}/api/customers/${vuState.customerId}`,
    JSON.stringify({
      id: vuState.customerId,
      firstName: 'Load',
      lastName: `User${__VU}`,
      email: vuState.email,
      phoneNumber: '+90 555 000 00 00',
    }), { headers: customerHeaders }), 204);
  expectStatus('GET own accounts', http.get(
    `${BASE_URL}/api/accounts?pageNumber=1&pageSize=20`, { headers: customerHeaders }), 200);
  expectStatus('GET own account and balance', http.get(
    `${BASE_URL}/api/accounts/${vuState.accountId}`, { headers: customerHeaders }), 200);
  expectStatus('PUT account status', http.put(
    `${BASE_URL}/api/accounts/${vuState.accountId}`,
    JSON.stringify({ id: vuState.accountId, status: 'Active' }), { headers: customerHeaders }), 204);

  // Each financial write gets a unique idempotency key and is followed by a read.
  const operationKey = `${data.runId}-${__VU}-${__ITER}`;
  const deposit = expectStatus('POST deposit', http.post(
    `${BASE_URL}/api/financialtransactions/deposit`,
    JSON.stringify({ accountId: vuState.accountId, amount: 100.25, description: 'k6 deposit' }),
    { headers: { ...customerHeaders, 'Idempotency-Key': `${operationKey}-deposit` } }), 201);
  const depositBody = deposit.json();

  expectStatus('POST withdrawal', http.post(
    `${BASE_URL}/api/financialtransactions/withdraw`,
    JSON.stringify({ accountId: vuState.accountId, amount: 10.00, description: 'k6 withdrawal' }),
    { headers: { ...customerHeaders, 'Idempotency-Key': `${operationKey}-withdraw` } }), 201);
  const transfer = expectStatus('POST transfer', http.post(
    `${BASE_URL}/api/financialtransactions/transfer`,
    JSON.stringify({
      sourceAccountId: vuState.accountId,
      destinationAccountId: vuState.destinationAccountId,
      amount: 25.50,
      description: 'k6 transfer',
    }), { headers: { ...customerHeaders, 'Idempotency-Key': `${operationKey}-transfer` } }), 201);
  const transferBody = transfer.json();
  const transactionId = transferBody.transactionId || depositBody.transactionId;

  expectStatus('GET transaction page with filters', http.get(
    `${BASE_URL}/api/financialtransactions?accountId=${vuState.accountId}` +
      `&fromUtc=${encodeURIComponent(data.fromUtc)}&toUtc=${encodeURIComponent(data.toUtc)}` +
      '&type=Transfer&minimumAmount=1&maximumAmount=1000&pageNumber=1&pageSize=20&descending=true',
    { headers: customerHeaders }), 200);
  expectStatus('GET transaction details', http.get(
    `${BASE_URL}/api/financialtransactions/${transactionId}`, { headers: customerHeaders }), 200);
  expectStatus('GET account history', http.get(
    `${BASE_URL}/api/financialtransactions/account/${vuState.accountId}` +
      `?fromUtc=${encodeURIComponent(data.fromUtc)}&toUtc=${encodeURIComponent(data.toUtc)}` +
      '&pageNumber=1&pageSize=20&descending=true', { headers: customerHeaders }), 200);
  expectStatus('GET daily account report', http.get(
    `${BASE_URL}/api/financialtransactions/reports?fromUtc=${encodeURIComponent(data.fromUtc)}` +
      `&toUtc=${encodeURIComponent(data.toUtc)}&period=Daily&accountId=${vuState.accountId}`,
    { headers: customerHeaders }), 200);
  expectStatus('GET weekly account report', http.get(
    `${BASE_URL}/api/financialtransactions/reports?fromUtc=${encodeURIComponent(data.fromUtc)}` +
      `&toUtc=${encodeURIComponent(data.toUtc)}&period=Weekly&accountId=${vuState.accountId}`,
    { headers: customerHeaders }), 200);
  expectStatus('GET monthly account report', http.get(
    `${BASE_URL}/api/financialtransactions/reports?fromUtc=${encodeURIComponent(data.fromUtc)}` +
      `&toUtc=${encodeURIComponent(data.toUtc)}&period=Monthly&accountId=${vuState.accountId}`,
    { headers: customerHeaders }), 200);

  // Administrator reads and CRUD endpoints are exercised with isolated, disposable records.
  expectStatus('GET all customers (admin)', http.get(
    `${BASE_URL}/api/customers?pageNumber=1&pageSize=20`, { headers: adminHeaders }), 200);
  expectStatus('GET all products (admin)', http.get(
    `${BASE_URL}/api/products?pageNumber=1&pageSize=20`, { headers: adminHeaders }), 200);

  sleep(THINK_TIME_SECONDS);
}

function initializeVirtualUser(data) {
  const unique = `${data.runId}-${__VU}`;
  const email = `k6-${unique}@example.test`;
  const username = `k6-${unique}`;

  const registration = expectStatus('POST customer registration', http.post(
    `${BASE_URL}/api/auth/register`,
    JSON.stringify({
      username,
      password: CUSTOMER_PASSWORD,
      firstName: 'Load',
      lastName: `User${__VU}`,
      email,
      phoneNumber: '+90 555 000 00 00',
    }), { headers: { 'Content-Type': 'application/json' } }), 201);
  const customerId = registration.json().customerId;

  const login = expectStatus('POST customer login', http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ username, password: CUSTOMER_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } }), 200).json();
  const customerHeaders = {
    Authorization: `Bearer ${login.accessToken}`,
    'Content-Type': 'application/json',
  };
  const adminHeaders = {
    Authorization: `Bearer ${data.adminToken}`,
    'Content-Type': 'application/json',
  };

  const accountId = createAccount(customerHeaders, customerId, `K6-${unique}-A`);
  const destinationAccountId = createAccount(customerHeaders, customerId, `K6-${unique}-B`);
  const disposableAccountId = createAccount(customerHeaders, customerId, `K6-${unique}-X`);
  expectStatus('PUT disposable account inactive', http.put(
    `${BASE_URL}/api/accounts/${disposableAccountId}`,
    JSON.stringify({ id: disposableAccountId, status: 'Inactive' }), { headers: customerHeaders }), 204);
  expectStatus('DELETE empty disposable account', http.del(
    `${BASE_URL}/api/accounts/${disposableAccountId}`, null, { headers: customerHeaders }), 204);

  const adminCustomerId = createAdminCustomer(adminHeaders, unique);
  expectStatus('GET created customer (admin)', http.get(
    `${BASE_URL}/api/customers/${adminCustomerId}`, { headers: adminHeaders }), 200);
  expectStatus('PUT customer (admin)', http.put(
    `${BASE_URL}/api/customers/${adminCustomerId}`,
    JSON.stringify({
      id: adminCustomerId,
      firstName: 'Updated',
      lastName: 'LoadCustomer',
      email: `updated-${unique}@example.test`,
      phoneNumber: '+90 555 000 01 00',
      isActive: true,
    }), { headers: adminHeaders }), 204);
  expectStatus('DELETE customer without accounts (admin)', http.del(
    `${BASE_URL}/api/customers/${adminCustomerId}`, null, { headers: adminHeaders }), 204);

  const productId = createAdminProduct(adminHeaders, unique);
  expectStatus('GET product (admin)', http.get(
    `${BASE_URL}/api/products/${productId}`, { headers: adminHeaders }), 200);
  expectStatus('PUT product (admin)', http.put(
    `${BASE_URL}/api/products/${productId}`,
    JSON.stringify({
      id: productId,
      name: `Updated-${unique}`,
      description: 'Updated k6 load-test product',
      price: 20.99,
      stock: 8,
    }), { headers: adminHeaders }), 204);
  expectStatus('DELETE product (admin)', http.del(
    `${BASE_URL}/api/products/${productId}`, null, { headers: adminHeaders }), 204);

  return { customerId, customerToken: login.accessToken, email, accountId, destinationAccountId };
}

function createAccount(headers, customerId, accountNumber) {
  const response = expectStatus('POST account', http.post(
    `${BASE_URL}/api/accounts`,
    JSON.stringify({ customerId, accountNumber, currencyCode: 'TRY' }), { headers }), 201);
  return parseCreatedId(response);
}

function createAdminCustomer(headers, unique) {
  const response = expectStatus('POST customer (admin)', http.post(
    `${BASE_URL}/api/customers`,
    JSON.stringify({
      firstName: 'Disposable',
      lastName: 'LoadCustomer',
      email: `disposable-${unique}@example.test`,
      phoneNumber: '+90 555 000 02 00',
    }), { headers }), 201);
  return parseCreatedId(response);
}

function createAdminProduct(headers, unique) {
  const response = expectStatus('POST product (admin)', http.post(
    `${BASE_URL}/api/products`,
    JSON.stringify({
      name: `K6-${unique}`,
      description: 'Disposable k6 load-test product',
      price: 19.99,
      stock: 10,
    }), { headers }), 201);
  return parseCreatedId(response);
}

function parseCreatedId(response) {
  const body = response.json();
  if (typeof body === 'string') return body;
  if (body && (body.id || body.customerId || body.accountId || body.productId)) {
    return body.id || body.customerId || body.accountId || body.productId;
  }
  throw new Error(`Create response did not contain a resource ID: ${response.body}`);
}

function expectStatus(name, response, expectedStatus) {
  const passed = check(response, {
    [`${name}: HTTP ${expectedStatus}`]: (result) => result.status === expectedStatus,
  });
  errorRate.add(!passed);
  if (!passed) {
    throw new Error(`${name} expected HTTP ${expectedStatus}, received ${response.status}: ${response.body}`);
  }
  return response;
}
