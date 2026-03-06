SELECT add_compression_policy('price_ticks',   INTERVAL '7 days');
SELECT add_compression_policy('market_events', INTERVAL '7 days');

SELECT add_retention_policy('price_ticks',   INTERVAL '2 years');
SELECT add_retention_policy('market_events', INTERVAL '2 years');

SELECT add_continuous_aggregate_policy(
    'candles_1m',
    start_offset      => INTERVAL '1 hour',
    end_offset        => INTERVAL '1 minute',
    schedule_interval => INTERVAL '1 minute'
);

SELECT add_continuous_aggregate_policy(
    'candles_5m',
    start_offset      => INTERVAL '6 hours',
    end_offset        => INTERVAL '5 minutes',
    schedule_interval => INTERVAL '5 minutes'
);

SELECT add_continuous_aggregate_policy(
    'candles_1h',
    start_offset      => INTERVAL '2 days',
    end_offset        => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour'
);

SELECT add_continuous_aggregate_policy(
    'candles_1d',
    start_offset      => INTERVAL '30 days',
    end_offset        => INTERVAL '1 day',
    schedule_interval => INTERVAL '1 day'
);
