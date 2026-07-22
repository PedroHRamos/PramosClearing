const { Kafka } = require('kafkajs');
const http = require('http');

const kafka = new Kafka({
    clientId: 'k6-producer-sidecar',
    brokers: (process.env.KAFKA_BROKERS || 'kafka:9092').split(','),
});

const producer = kafka.producer({
    allowAutoTopicCreation: false,
    maxInFlightRequests: 5,
    idempotent: false,
});

const TOPIC             = process.env.KAFKA_TOPIC    || 'orderbook-updates';
const PORT              = parseInt(process.env.PORT       || '3000', 10);
const FLUSH_INTERVAL_MS = parseInt(process.env.FLUSH_MS   || '10',   10);
const MAX_BUFFER        = parseInt(process.env.MAX_BUFFER || '10000', 10);

const buffer = [];
let flushing = false;

setInterval(async () => {
    // Skip tick if a flush is already in progress — prevents overlapping sends
    // that would cause the buffer to grow unboundedly under slow Kafka acks.
    if (flushing || buffer.length === 0) return;
    flushing = true;
    const batch = buffer.splice(0, buffer.length);
    try {
        await producer.send({
            topic: TOPIC,
            messages: batch.map(({ key, value }) => ({ key, value })),
        });
        batch.forEach(({ resolve }) => resolve());
    } catch (e) {
        batch.forEach(({ reject }) => reject(e));
    } finally {
        flushing = false;
    }
}, FLUSH_INTERVAL_MS);

async function start() {
    await producer.connect();

    const server = http.createServer((req, res) => {
        if (req.method !== 'POST' || req.url !== '/produce') {
            res.writeHead(404);
            res.end();
            return;
        }

        if (buffer.length >= MAX_BUFFER) {
            res.writeHead(503);
            res.end('buffer full');
            return;
        }

        let body = '';
        req.on('data', chunk => { body += chunk; });
        req.on('end', () => {
            let parsed;
            try { parsed = JSON.parse(body); } catch (_) {
                res.writeHead(400);
                res.end('bad json');
                return;
            }

            // Accept both batch { messages: [{key,value},...] } and single { key, value }
            const msgs = Array.isArray(parsed.messages)
                ? parsed.messages
                : [{ key: parsed.key, value: parsed.value }];

            if (buffer.length + msgs.length > MAX_BUFFER) {
                res.writeHead(503);
                res.end('buffer full');
                return;
            }

            Promise.all(msgs.map(({ key, value }) =>
                new Promise((resolve, reject) => {
                    buffer.push({ key, value, resolve, reject });
                })
            )).then(() => {
                res.writeHead(200);
                res.end('ok');
            }).catch(e => {
                res.writeHead(500);
                res.end(e.message);
            });
        });
    });

    server.listen(PORT, () =>
        console.log(`k6-producer-sidecar ready on :${PORT} (flush=${FLUSH_INTERVAL_MS}ms max_buffer=${MAX_BUFFER})`)
    );
}

start().catch(err => { console.error(err); process.exit(1); });
