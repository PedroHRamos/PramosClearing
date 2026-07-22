/**
 * kafka-ingest-load.js
 *
 * Produces synthetic OrderBookUpdate events directly to Kafka at high rate
 * via the producer-sidecar HTTP bridge (tests/k6/producer-sidecar/).
 * The sidecar uses the native Kafka protocol (kafkajs), so it can sustain
 * thousands of events/sec — unlike the Kafka UI REST API which is a debug tool.
 *
 * Start the sidecar first (see README), then run with standard k6:
 *   k6 run -e EVENTS_PER_SEC=1000 tests/k6/kafka-ingest-load.js
 *
 * Monitor consumer lag in real time at http://localhost:8090 → Consumer Groups
 * → market-price-tick-consumer (Messages Behind column).
 *
 * Key questions answered:
 *   - At what events/sec does the consumer group start accumulating lag?
 *   - What is the maximum sustainable ingest rate into TimescaleDB?
 *   - Does write pressure degrade the read path (test with market-heavy-read.js
 *     running in a third terminal at the same time)?
 */

import http from 'k6/http';
import { check } from 'k6';
import { Rate, Counter } from 'k6/metrics';

const SIDECAR_URL    = __ENV.SIDECAR_URL   || 'http://localhost:3001';
const EVENTS_PER_SEC = parseInt(__ENV.EVENTS_PER_SEC || '1000', 10);
const BATCH_SIZE     = parseInt(__ENV.BATCH_SIZE     || '10',   10);
const DURATION       = __ENV.DURATION      || '60s';

// Rate = iterations/s. Each iteration sends BATCH_SIZE events, so total
// events/s = RATE * BATCH_SIZE = EVENTS_PER_SEC.
const RATE = Math.ceil(EVENTS_PER_SEC / BATCH_SIZE);

const PRODUCE_URL = `${SIDECAR_URL}/produce`;

const TICKERS = (__ENV.TICKERS ||
    'AAPL@NASDAQ,MSFT@NASDAQ,NVDA@NASDAQ,TSLA@NASDAQ,AMZN@NASDAQ,META@NASDAQ,' +
    'JPM@NYSE,GS@NYSE,XOM@NYSE,JNJ@NYSE,KO@NYSE,BA@NYSE'
).split(',').map(t => {
    const [symbol, exchange] = t.split('@');
    return { symbol, exchange };
});

const SIDES   = ['bid', 'ask'];
const ACTIONS = ['add', 'update', 'remove'];

const errorRate     = new Rate('produce_errors');
const eventsCounter = new Counter('events_produced');

export const options = {
    scenarios: {
        ingest: {
            executor:        'constant-arrival-rate',
            rate:            RATE,
            timeUnit:        '1s',
            duration:        DURATION,
            preAllocatedVUs: Math.ceil(RATE / 10),
            maxVUs:          Math.max(100, RATE * 3),
        },
    },
    thresholds: {
        'produce_errors': ['rate<0.01'],
    },
};

function randomItem(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
}

export default function () {
    const messages = [];
    for (let i = 0; i < BATCH_SIZE; i++) {
        const { symbol, exchange } = randomItem(TICKERS);
        messages.push({
            key: `${symbol}@${exchange}`,
            value: JSON.stringify({
                symbol,
                exchange,
                side:      randomItem(SIDES),
                price:     Math.round((10 + Math.random() * 990) * 100) / 100,
                size:      Math.floor(Math.random() * 2000) + 100,
                action:    randomItem(ACTIONS),
                timestamp: new Date().toISOString(),
            }),
        });
    }

    const res = http.post(PRODUCE_URL, JSON.stringify({ messages }), {
        headers: { 'Content-Type': 'application/json' },
        timeout: '5s',
    });

    const ok = check(res, { 'messages produced': (r) => r.status === 200 });
    errorRate.add(!ok);
    if (ok) eventsCounter.add(BATCH_SIZE);
}
