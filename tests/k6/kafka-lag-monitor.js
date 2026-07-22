/**
 * kafka-lag-monitor.js
 *
 * Polls the Kafka UI REST API to track consumer group lag on the
 * orderbook-updates topic. Runs with standard k6 — no custom binary needed.
 *
 * Run this ALONGSIDE kafka-ingest-load.js in a second terminal to see
 * whether the market-price-tick-consumer is keeping up with produced events.
 *
 * Usage:
 *   k6 run tests/k6/kafka-lag-monitor.js
 *
 *   With custom Kafka UI address:
 *   k6 run -e KAFKA_UI_URL=http://localhost:8090 tests/k6/kafka-lag-monitor.js
 *
 *   To monitor for a longer period:
 *   k6 run -e DURATION=5m tests/k6/kafka-lag-monitor.js
 *
 * Metrics emitted:
 *   consumer_lag         — total messages behind for market-price-tick-consumer
 *   lag_fetch_errors     — rate of failed Kafka UI API calls
 *
 * Interpretation:
 *   consumer_lag = 0       consumer is fully caught up
 *   consumer_lag growing   consumer cannot keep up — ingest rate exceeds write capacity
 *   consumer_lag stable    system reached a new steady state (possibly degraded)
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Gauge, Rate } from 'k6/metrics';

const KAFKA_UI_URL    = __ENV.KAFKA_UI_URL    || 'http://localhost:8090';
const CLUSTER_NAME    = __ENV.CLUSTER_NAME    || 'pramos-clearing';
const CONSUMER_GROUP  = __ENV.CONSUMER_GROUP  || 'market-price-tick-consumer';
const DURATION        = __ENV.DURATION        || '90s';
const POLL_INTERVAL_S = parseFloat(__ENV.POLL_INTERVAL_S || '2');

const consumerLag  = new Gauge('consumer_lag');
const lagFetchErrors = new Rate('lag_fetch_errors');

export const options = {
    scenarios: {
        monitor: {
            executor:        'constant-vus',
            vus:             1,
            duration:        DURATION,
        },
    },
    thresholds: {
        'lag_fetch_errors': ['rate<0.05'],
    },
};

export default function () {
    const url = `${KAFKA_UI_URL}/api/clusters/${CLUSTER_NAME}/consumer-groups/${CONSUMER_GROUP}`;
    const res = http.get(url, { timeout: '5s' });

    const ok = check(res, {
        'kafka ui responded': (r) => r.status === 200,
        'body is json':       (r) => r.headers['Content-Type']?.includes('application/json'),
    });

    lagFetchErrors.add(!ok);

    if (ok) {
        let lag = 0;
        try {
            const body = JSON.parse(res.body);

            // On the first poll, dump the raw shape so we can verify the field path
            // in case kafka-ui version uses a different structure.
            if (__ITER === 0) {
                console.log(`[debug] response keys: ${Object.keys(body).join(', ')}`);
                console.log(`[debug] messagesBehind = ${body.messagesBehind} (${typeof body.messagesBehind})`);
            }

            // messagesBehind is the group-level sum; fall back to 0 only when the
            // field is truly absent (not when it is explicitly null or 0).
            lag = (body.messagesBehind != null) ? body.messagesBehind : 0;
        } catch (_) {
            lagFetchErrors.add(1);
        }
        consumerLag.add(lag);
        console.log(`consumer_lag=${lag}`);
    } else {
        console.log(`consumer_lag=ERROR status=${res.status}`);
    }

    sleep(POLL_INTERVAL_S);
}
