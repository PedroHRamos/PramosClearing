CREATE TABLE market_events (
    time        TIMESTAMPTZ NOT NULL,
    asset_id    UUID        NOT NULL,
    symbol      TEXT        NOT NULL,
    exchange    TEXT        NOT NULL,
    event_type  TEXT        NOT NULL,
    details     JSONB
);

SELECT create_hypertable('market_events', by_range('time'));

ALTER TABLE market_events SET (
    timescaledb.compress,
    timescaledb.compress_orderby   = 'time DESC',
    timescaledb.compress_segmentby = 'asset_id'
);

CREATE INDEX ix_market_events_asset_id_time
    ON market_events (asset_id, time DESC);

CREATE INDEX ix_market_events_event_type_time
    ON market_events (event_type, time DESC);
