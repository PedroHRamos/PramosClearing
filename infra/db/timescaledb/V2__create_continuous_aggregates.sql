CREATE MATERIALIZED VIEW candles_1m
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 minute', time) AS bucket,
    asset_id,
    symbol,
    exchange,
    first(last, time)             AS open,
    max(last)                     AS high,
    min(last)                     AS low,
    last(last, time)              AS close,
    sum(volume)                   AS volume
FROM price_ticks
GROUP BY bucket, asset_id, symbol, exchange
WITH NO DATA;


CREATE MATERIALIZED VIEW candles_5m
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('5 minutes', time) AS bucket,
    asset_id,
    symbol,
    exchange,
    first(last, time)              AS open,
    max(last)                      AS high,
    min(last)                      AS low,
    last(last, time)               AS close,
    sum(volume)                    AS volume
FROM price_ticks
GROUP BY bucket, asset_id, symbol, exchange
WITH NO DATA;


CREATE MATERIALIZED VIEW candles_1h
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 hour', time) AS bucket,
    asset_id,
    symbol,
    exchange,
    first(last, time)           AS open,
    max(last)                   AS high,
    min(last)                   AS low,
    last(last, time)            AS close,
    sum(volume)                 AS volume
FROM price_ticks
GROUP BY bucket, asset_id, symbol, exchange
WITH NO DATA;


CREATE MATERIALIZED VIEW candles_1d
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 day', time)  AS bucket,
    asset_id,
    symbol,
    exchange,
    first(last, time)           AS open,
    max(last)                   AS high,
    min(last)                   AS low,
    last(last, time)            AS close,
    sum(volume)                 AS volume
FROM price_ticks
GROUP BY bucket, asset_id, symbol, exchange
WITH NO DATA;
