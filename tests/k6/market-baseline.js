/**
 * market-baseline.js
 *
 * Steady-state load test for the Market Service.
 * Measures baseline latency and throughput at a fixed RPS.
 *
 * Key env vars (see tests/k6/README.md for full list):
 *   TARGET_RPS  – requests per second target (default 50)
 *   DURATION    – test duration            (default 60s)
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// 404 is an expected response for /price-ticks/latest when a symbol has no data yet.
// Without this, K6 would count every 404 as a failed request and inflate http_req_failed.
http.setResponseCallback(http.expectedStatuses(200, 201, 404));

const BASE_URL   = __ENV.BASE_URL  || 'http://localhost:5001';
const TARGET_RPS = parseInt(__ENV.TARGET_RPS || '50', 10);
const DURATION   = __ENV.DURATION  || '60s';
const TICK_TAKE  = parseInt(__ENV.TICK_TAKE  || '100', 10);

// Format: symbol@exchange — must match rows seeded by EF Core migrations.
const RAW_TICKERS = __ENV.TICKERS ||
    'AAPL@NASDAQ,MSFT@NASDAQ,NVDA@NASDAQ,TSLA@NASDAQ,AMZN@NASDAQ,META@NASDAQ,' +
    'JPM@NYSE,GS@NYSE,XOM@NYSE,JNJ@NYSE,KO@NYSE,BA@NYSE';

const TICKERS = RAW_TICKERS.split(',').map(t => {
    const [symbol, exchange] = t.split('@');
    return { symbol, exchange };
});

const errorRate        = new Rate('errors');
const priceTickLatency = new Trend('price_tick_latency', true);
const latestLatency    = new Trend('latest_price_latency', true);

// Constant-arrival-rate executor drives a stable RPS regardless of response time
export const options = {
    scenarios: {
        price_ticks: {
            executor:        'constant-arrival-rate',
            rate:            Math.ceil(TARGET_RPS * 0.6),  // 60 % → /price-ticks (bulk read)
            timeUnit:        '1s',
            duration:        DURATION,
            preAllocatedVUs: Math.ceil(TARGET_RPS * 0.6 * 2),
            maxVUs:          Math.ceil(TARGET_RPS * 0.6 * 10),
            exec:            'getPriceTicks',
        },
        latest_price: {
            executor:        'constant-arrival-rate',
            rate:            Math.ceil(TARGET_RPS * 0.3),  // 30 % → /price-ticks/latest
            timeUnit:        '1s',
            duration:        DURATION,
            preAllocatedVUs: Math.ceil(TARGET_RPS * 0.3 * 2),
            maxVUs:          Math.ceil(TARGET_RPS * 0.3 * 10),
            exec:            'getLatestPrice',
        },
        stock_list: {
            executor:        'constant-arrival-rate',
            rate:            Math.ceil(TARGET_RPS * 0.1),  // 10 % → /stocks (low frequency)
            timeUnit:        '1s',
            duration:        DURATION,
            preAllocatedVUs: 5,
            maxVUs:          20,
            exec:            'getStocks',
        },
    },
    thresholds: {
        'errors':               ['rate<0.01'],          // < 1 % error rate
        'http_req_duration':    ['p(95)<500'],          // 95th percentile under 500 ms
        'price_tick_latency':   ['p(95)<500'],
        'latest_price_latency': ['p(95)<200'],          // latest-price should be faster
    },
};

function randomTicker() {
    return TICKERS[Math.floor(Math.random() * TICKERS.length)];
}

export function getPriceTicks() {
    const { symbol, exchange } = randomTicker();
    const url = `${BASE_URL}/api/price-ticks?symbol=${symbol}&exchange=${exchange}&take=${TICK_TAKE}`;
    const res = http.get(url, { tags: { name: 'price-ticks' } });

    priceTickLatency.add(res.timings.duration);

    const ok = check(res, {
        'price-ticks 200':      (r) => r.status === 200,
        'price-ticks body set': (r) => r.body && r.body.length > 0,
    });
    errorRate.add(!ok);
}

export function getLatestPrice() {
    const { symbol, exchange } = randomTicker();
    const url = `${BASE_URL}/api/price-ticks/latest?symbol=${symbol}&exchange=${exchange}`;
    const res = http.get(url, { tags: { name: 'latest-price' } });

    latestLatency.add(res.timings.duration);

    const ok = check(res, {
        'latest-price 200': (r) => r.status === 200,
    });
    errorRate.add(!ok);
}

export function getStocks() {
    const url = `${BASE_URL}/api/stocks?pageSize=20&page=1`;
    const res = http.get(url, { tags: { name: 'stocks-list' } });

    const ok = check(res, {
        'stocks 200': (r) => r.status === 200,
    });
    errorRate.add(!ok);
}
