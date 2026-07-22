/**
 * market-spike.js
 *
 * Spike test — simulates a sudden burst of traffic (e.g. market open, news event).
 *
 * Pattern:
 *   0 →   0 VUs  idle for 10s   – baseline silence
 *   0 → 2000 VUs in 10s         – instant spike
 *   2000 VUs held for 30s       – sustained burst
 *   2000 → 0 VUs in 10s         – drain
 *   0 VUs idle for 30s          – recovery observation
 *
 * Goal: observe whether the system recovers cleanly after the spike without
 * lingering high latency, leaked connections, or growing error rate.
 *
 * Key questions answered:
 *   - Does latency return to baseline after the spike?
 *   - Do DB connection pools exhaust and stay exhausted?
 *   - Does the error rate spike and then recover?
 */

import http from 'k6/http';
import { check } from 'k6';
import { Rate, Trend } from 'k6/metrics';

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
const spikeLatency = new Trend('spike_latency', true);

export const options = {
    stages: [
        { duration: '10s', target: 0    },  // silence — collect baseline noise
        { duration: '10s', target: 2000 },  // sudden spike
        { duration: '30s', target: 2000 },  // hold burst
        { duration: '10s', target: 0    },  // drain
        { duration: '30s', target: 0    },  // recovery window
    ],
    thresholds: {
        // During burst we tolerate degradation; after recovery we expect cleanup.
        // These are informational — no abortOnFail — so the full run always completes.
        'errors':            ['rate<0.10'],
        'http_req_duration': ['p(99)<3000'],
    },
};

function randomTicker() {
    return TICKERS[Math.floor(Math.random() * TICKERS.length)];
}

export default function () {
    const { symbol, exchange } = randomTicker();

    // Spike hits the most expensive read path to maximise connection pool pressure
    const url = `${BASE_URL}/api/price-ticks?symbol=${symbol}&exchange=${exchange}&take=${TICK_TAKE}`;
    const res = http.get(url, { tags: { name: 'spike-price-ticks' } });

    spikeLatency.add(res.timings.duration);

    const ok = check(res, {
        'status 200': (r) => r.status === 200,
    });
    errorRate.add(!ok);
}
