import http from "k6/http";
import { check, fail, sleep } from "k6";
import ws from "k6/ws";
import { Rate } from "k6/metrics";

const baseUrl = (__ENV.BASE_URL || "http://localhost:8080").replace(/\/$/, "");
const hostHeader = __ENV.HOST_HEADER;
const email = __ENV.PERF_EMAIL;
const password = __ENV.PERF_PASSWORD;
const commandTripId = __ENV.PERF_TRIP_ID;
const signalRHandshakeSuccess = new Rate("signalr_handshake_success");

export const options = {
  discardResponseBodies: false,
  scenarios: {
    login: {
      executor: "per-vu-iterations",
      vus: 10,
      iterations: 1,
      maxDuration: "30s",
      exec: "loginScenario",
    },
    trip_list: {
      executor: "constant-arrival-rate",
      rate: 2,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 5,
      maxVUs: 20,
      exec: "tripListScenario",
    },
    operations_board: {
      executor: "constant-arrival-rate",
      rate: 1,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 3,
      maxVUs: 10,
      exec: "operationsBoardScenario",
    },
    status_transition: {
      executor: "constant-arrival-rate",
      rate: 1,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 3,
      maxVUs: 10,
      exec: "statusTransitionScenario",
    },
    location_update: {
      executor: "constant-arrival-rate",
      rate: 1,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 3,
      maxVUs: 10,
      exec: "locationUpdateScenario",
    },
    weighbridge_submission: {
      executor: "constant-arrival-rate",
      rate: 1,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 2,
      maxVUs: 8,
      exec: "weighbridgeScenario",
    },
    signalr_connection: {
      executor: "constant-arrival-rate",
      rate: 1,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 2,
      maxVUs: 8,
      exec: "signalRScenario",
    },
    report_request: {
      executor: "constant-arrival-rate",
      rate: 1,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 2,
      maxVUs: 8,
      exec: "reportRequestScenario",
    },
  },
  thresholds: {
    "http_req_duration{endpoint:login}": ["p(95)<500"],
    "http_req_duration{endpoint:trip-list}": ["p(95)<500"],
    "http_req_duration{endpoint:operations-board}": ["p(95)<500", "max<3000"],
    "http_req_duration{endpoint:status-transition}": ["p(95)<800"],
    "http_req_duration{endpoint:location-update}": ["p(95)<800"],
    "http_req_duration{endpoint:weighbridge-submission}": ["p(95)<800"],
    "http_req_duration{endpoint:report-request}": ["p(95)<800"],
    "ws_session_duration{endpoint:signalr}": ["p(95)<3000"],
    signalr_handshake_success: ["rate==1"],
  },
};

export function setup() {
  if (!email || !password) {
    fail("Set PERF_EMAIL and PERF_PASSWORD for the generated performance tenant.");
  }
  if (!commandTripId) {
    fail("Set PERF_TRIP_ID to an assigned trip linked to the performance admin driver.");
  }

  return { token: login() };
}

export function loginScenario() {
  const response = loginResponse();
  check(response, {
    "login returns 200": (item) => item.status === 200,
    "login returns an access token": (item) => Boolean(item.json("accessToken")),
  });
}

export function tripListScenario(data) {
  const response = http.get(
    `${baseUrl}/api/v1/trips/?page=1&pageSize=50&sort=collection&direction=asc`,
    requestParams(data.token, "trip-list"),
  );
  check(response, { "trip list returns 200": (item) => item.status === 200 });
}

export function operationsBoardScenario(data) {
  const response = http.get(
    `${baseUrl}/api/v1/operations/board?limit=250`,
    requestParams(data.token, "operations-board"),
  );
  check(response, { "operations board returns 200": (item) => item.status === 200 });
}

export function statusTransitionScenario(data) {
  const list = http.get(
    `${baseUrl}/api/v1/trips/?status=Assigned&page=1&pageSize=20&sort=collection&direction=asc`,
    requestParams(data.token, "status-transition-discovery"),
  );
  if (list.status !== 200) {
    check(list, { "status transition discovery returns 200": (item) => item.status === 200 });
    return;
  }

  const items = list.json("items") || [];
  if (items.length === 0) return;
  const item = items[(__VU + __ITER) % items.length];
  const response = http.post(
    `${baseUrl}/api/v1/trips/${item.id}/dispatch`,
    JSON.stringify({ version: item.version }),
    commandParams(data.token, "status-transition", `dispatch-${__VU}-${__ITER}`),
  );
  check(response, {
    "status transition returns success or a concurrency conflict": (result) =>
      result.status === 200 || result.status === 409,
  });
}

export function locationUpdateScenario(data) {
  const trip = readCommandTrip(data.token, "location-discovery");
  if (!trip) return;
  const response = http.post(
    `${baseUrl}/api/v1/trips/${commandTripId}/location`,
    JSON.stringify({
      version: trip.version,
      latitude: -26.2041,
      longitude: 28.0471,
      accuracyMetres: 25,
      capturedAtUtc: new Date().toISOString(),
    }),
    commandParams(data.token, "location-update", `location-${__VU}-${__ITER}`),
  );
  check(response, {
    "location update returns success or a concurrency conflict": (result) =>
      result.status === 200 || result.status === 409,
  });
}

export function weighbridgeScenario(data) {
  const trip = readCommandTrip(data.token, "weighbridge-discovery");
  if (!trip) return;
  const response = http.post(
    `${baseUrl}/api/v1/trips/${commandTripId}/source-weighbridge`,
    JSON.stringify({
      version: trip.version,
      grossWeight: 40000,
      tareWeight: 10000,
      reference: "K6-PERFORMANCE-SOURCE",
      reason: "Synthetic performance correction.",
    }),
    commandParams(data.token, "weighbridge-submission", `weighbridge-${__VU}-${__ITER}`),
  );
  check(response, {
    "weighbridge submission returns success or a concurrency conflict": (result) =>
      result.status === 200 || result.status === 409,
  });
}

export function signalRScenario(data) {
  const socketUrl = baseUrl.replace(/^http:/, "ws:").replace(/^https:/, "wss:")
    + `/hubs/operations?access_token=${encodeURIComponent(data.token)}`;
  const result = ws.connect(socketUrl, {
    tags: { endpoint: "signalr" },
    headers: hostHeader ? { Host: hostHeader } : undefined,
  }, (socket) => {
    let handshakeCompleted = false;
    socket.on("open", () => {
      socket.send('{"protocol":"json","version":1}\u001e');
    });
    socket.on("message", (message) => {
      if (!handshakeCompleted && String(message).includes("{}\u001e")) {
        handshakeCompleted = true;
        signalRHandshakeSuccess.add(true);
        socket.close();
      }
    });
    socket.setTimeout(() => {
      if (!handshakeCompleted) signalRHandshakeSuccess.add(false);
      socket.close();
    }, 1000);
  });
  check(result, { "SignalR handshake accepts a WebSocket": (item) => item.status === 101 });
}

export function reportRequestScenario(data) {
  const response = http.post(
    `${baseUrl}/api/v1/reports/trip-register/export-jobs`,
    null,
    commandParams(data.token, "report-request", `report-${__VU}-${__ITER}`),
  );
  check(response, { "report export is queued": (item) => item.status === 202 });
}

function login() {
  const response = loginResponse();
  if (response.status !== 200) {
    fail(`Login failed with HTTP ${response.status}: ${response.body}`);
  }
  return response.json("accessToken");
}

function loginResponse() {
  return http.post(
    `${baseUrl}/api/v1/auth/login`,
    JSON.stringify({ email, password }),
    {
      headers: {
        "Content-Type": "application/json",
        ...(hostHeader ? { Host: hostHeader } : {}),
      },
      tags: { endpoint: "login" },
    },
  );
}

function readCommandTrip(token, endpoint) {
  const response = http.get(
    `${baseUrl}/api/v1/trips/${commandTripId}`,
    requestParams(token, endpoint),
  );
  check(response, { [`${endpoint} returns 200`]: (item) => item.status === 200 });
  return response.status === 200 ? response.json() : null;
}

function requestParams(token, endpoint) {
  return {
    headers: {
      Authorization: `Bearer ${token}`,
      ...(hostHeader ? { Host: hostHeader } : {}),
    },
    tags: { endpoint },
  };
}

function commandParams(token, endpoint, idempotencyKey) {
  return {
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
      "Idempotency-Key": idempotencyKey,
      ...(hostHeader ? { Host: hostHeader } : {}),
    },
    tags: { endpoint },
  };
}
