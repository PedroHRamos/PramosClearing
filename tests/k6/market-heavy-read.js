/**
 * market-heavy-read.js
 *
 * Heavy read test — same throughput as the baseline but with large payloads
 * (TICK_TAKE=1000, 10x more rows per query) to stress:
 *   - TimescaleDB scan + serialisation cost
 *   - ASP.NET Core response buffering and JSON serialisation
 *   - Network bandwidth between TimescaleDB and the API container
 *
 * This test answers: "does the system degrade when data volume per request
 * grows, even if request rate stays constant?"
 *
 * Run alongside Grafana to observe TimescaleDB CPU, shared_buffers hit rate,
 * and API container memory.
 */

import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

http.setResponseCallback(http.expectedStatuses(200, 201));

const BASE_URL  = __ENV.BASE_URL  || 'http://localhost:5001';
const TICK_TAKE = parseInt(__ENV.TICK_TAKE || '1000', 10);  // 10x default on purpose

// Format: symbol@exchange — must match rows seeded by EF Core migrations.
const RAW_TICKERS = __ENV.TICKERS ||
    'AAPL@NASDAQ,MSFT@NASDAQ,NVDA@NASDAQ,TSLA@NASDAQ,AMZN@NASDAQ,META@NASDAQ,' +
    'JPM@NYSE,GS@NYSE,XOM@NYSE,JNJ@NYSE,KO@NYSE,BA@NYSE';

const TICKERS = RAW_TICKERS.split(',').map(t => {
    const [symbol, exchange] = t.split('@');
    return { symbol, exchange };
});

const TARGET_RPS = parseInt(__ENV.TARGET_RPS || '50', 10);
const DURATION   = __ENV.DURATION || '60s';

const errorRate      = new Rate('errors');
const heavyLatency   = new Trend('heavy_read_latency', true);

export const options = {
    scenarios: {
        heavy_reads: {
            executor:        'constant-arrival-rate',
            rate:            TARGET_RPS,
            timeUnit:        '1s',
            duration:        DURATION,
            preAllocatedVUs: TARGET_RPS * 4,
            maxVUs:          TARGET_RPS * 20,
        },
    },
    thresholds: {
        'errors':             ['rate<0.01'],
        'http_req_duration':  ['p(95)<2000'],   // large payload — wider threshold
        'heavy_read_latency': ['p(95)<2000'],
    },
};

function randomTicker() {
    return TICKERS[Math.floor(Math.random() * TICKERS.length)];
}

export default function () {
    const { symbol, exchange } = randomTicker();
    const url = `${BASE_URL}/api/price-ticks?symbol=${symbol}&exchange=${exchange}&take=${TICK_TAKE}`;
    const res = http.get(url, { tags: { name: 'heavy-read' } });

    heavyLatency.add(res.timings.duration);

    const ok = check(res, {
        'status 200':       (r) => r.status === 200,
        'body not empty':   (r) => r.body && r.body.length > 2,
    });
    errorRate.add(!ok);
}
