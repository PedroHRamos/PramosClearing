CREATE TABLE price_ticks (
    time        TIMESTAMPTZ    NOT NULL,
    asset_id    UUID           NOT NULL,
    symbol      TEXT           NOT NULL,
    exchange    TEXT           NOT NULL,
    bid         NUMERIC(28, 8) NOT NULL,
    ask         NUMERIC(28, 8) NOT NULL,
    last        NUMERIC(28, 8) NOT NULL,
    volume      NUMERIC(28, 8) NOT NULL DEFAULT 0,
    source      TEXT           NOT NULL DEFAULT 'simulator'
);

SELECT create_hypertable('price_ticks', by_range('time'));

ALTER TABLE price_ticks SET (
    timescaledb.compress,
    timescaledb.compress_orderby   = 'time DESC',
    timescaledb.compress_segmentby = 'asset_id'
);

CREATE INDEX ix_price_ticks_asset_id_time
    ON price_ticks (asset_id, time DESC);

CREATE INDEX ix_price_ticks_symbol_exchange_time
    ON price_ticks (symbol, exchange, time DESC);
