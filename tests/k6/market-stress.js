/**
 * market-stress.js
 *
 * Ramping stress test for the Market Service.
 * Starts at low load and pushes steadily upward to find throughput limits
 * and observe where latency degrades or errors appear.
 *
 * Stages (total ~5 min):
 *   0 →  10 VUs  over 30s  – warm-up
 *  10 →  50 VUs  over 60s  – light load
 *  50 → 200 VUs  over 90s  – moderate load
 * 200 → 500 VUs  over 90s  – heavy load
 * 500 →   0 VUs  over 30s  – cool-down
 *
 * Each VU continuously alternates between price-tick bulk reads
 * and latest-price lookups — the two hottest paths during market simulation.
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// 404 is an expected response for /price-ticks/latest when a symbol has no data yet.
// Without this, K6 would count every 404 as a failed request and inflate http_req_failed.
http.setResponseCallback(http.expectedStatuses(200, 201, 404));

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
const tickLatency = new Trend('tick_latency', true);
const reqCount    = new Counter('total_requests');

export const options = {
    stages: [
        { duration: '30s',  target: 10  },  // warm-up
        { duration: '60s',  target: 50  },  // light
        { duration: '90s',  target: 200 },  // moderate
        { duration: '90s',  target: 500 },  // heavy
        { duration: '30s',  target: 0   },  // cool-down
    ],
    thresholds: {
        'errors':        ['rate<0.05'],      // allow up to 5 % errors under stress
        'http_req_duration': ['p(95)<2000'], // warn if 95th pct exceeds 2 s
    },
};

function randomTicker() {
    return TICKERS[Math.floor(Math.random() * TICKERS.length)];
}

export default function () {
    const { symbol, exchange } = randomTicker();
    reqCount.add(1);

    if (Math.random() < 0.7) {
        // 70 % — bulk price-ticks read (highest DB pressure)
        const url = `${BASE_URL}/api/price-ticks?symbol=${symbol}&exchange=${exchange}&take=${TICK_TAKE}`;
        const res = http.get(url, { tags: { name: 'price-ticks' } });

        tickLatency.add(res.timings.duration);

        const ok = check(res, {
            'status 200': (r) => r.status === 200,
        });
        errorRate.add(!ok);
    } else {
        // 30 % — latest-price read (point lookup)
        const url = `${BASE_URL}/api/price-ticks/latest?symbol=${symbol}&exchange=${exchange}`;
        const res = http.get(url, { tags: { name: 'latest-price' } });

        const ok = check(res, {
            'status 200': (r) => r.status === 200,
        });
        errorRate.add(!ok);
    }

    // Small think-time to avoid spinning at 100 % CPU on the VU side
    sleep(0.01);
}
