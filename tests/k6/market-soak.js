/**
 * market-soak.js
 *
 * Soak test — runs moderate load for an extended period to surface:
 *   - Memory leaks (heap growth over time)
 *   - DB connection pool exhaustion
 *   - TimescaleDB chunk bloat or planner degradation
 *   - Latency creep as the API warm-up effects wear off
 *
 * Pattern (total ~10 min):
 *   0 → 200 VUs over 1min   – ramp-up
 *   200 VUs held for 8min   – sustained soak
 *   200 → 0 VUs over 1min   – drain
 *
 * Compare p95 at minute 5 vs minute 30 to detect latency creep.
 * Monitor Docker stats alongside this test for memory growth signals.
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

http.setResponseCallback(http.expectedStatuses(200, 201));

const BASE_URL  = __ENV.BASE_URL  || 'http://localhost:5001';
const TICK_TAKE = parseInt(__ENV.TICK_TAKE || '100', 10);

// Format: symbol@exchange — must match rows seeded by EF Core migrations.
const RAW_TICKERS = __ENV.TICKERS ||
    'AAPL@NASDAQ,MSFT@NASDAQ,NVDA@NASDAQ,TSLA@NASDAQ,AMZN@NASDAQ,META@NASDAQ,' +
    'JPM@NYSE,GS@NYSE,XOM@NYSE,JNJ@NYSE,KO@NYSE,BA@NYSE';

const TICKERS = RAW_TICKERS.split(',').map(t => {
    const [symbol, exchange] = t.split('@');
    return { symbol, exchange };
});

const errorRate   = new Rate('errors');
const soakLatency = new Trend('soak_latency', true);

export const options = {
    stages: [
        { duration: '1m',  target: 200 },
        { duration: '8m',  target: 200 },
        { duration: '1m',  target: 0   },
    ],
    thresholds: {
        'errors':            ['rate<0.01'],
        'http_req_duration': ['p(95)<500'],
        'soak_latency':      ['p(95)<500'],
    },
};

function randomTicker() {
    return TICKERS[Math.floor(Math.random() * TICKERS.length)];
}

export default function () {
    const { symbol, exchange } = randomTicker();

    // Alternate read patterns to exercise both query paths uniformly
    if (Math.random() < 0.6) {
        const url = `${BASE_URL}/api/price-ticks?symbol=${symbol}&exchange=${exchange}&take=${TICK_TAKE}`;
        const res = http.get(url, { tags: { name: 'soak-bulk' } });

        soakLatency.add(res.timings.duration);

        const ok = check(res, { 'status 200': (r) => r.status === 200 });
        errorRate.add(!ok);
    } else {
        const url = `${BASE_URL}/api/price-ticks/latest?symbol=${symbol}&exchange=${exchange}`;
        const res = http.get(url, { tags: { name: 'soak-latest' } });

        soakLatency.add(res.timings.duration);

        const ok = check(res, { 'status 200': (r) => r.status === 200 });
        errorRate.add(!ok);
    }

    sleep(0.05);
}
