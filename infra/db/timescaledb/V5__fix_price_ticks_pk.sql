ALTER TABLE price_ticks DROP CONSTRAINT IF EXISTS price_ticks_pkey;

ALTER TABLE price_ticks ADD PRIMARY KEY (time, asset_id);
