-- Adds the optional Facility ID held against each entry in the Server List tab.
-- The MWL service looks this value up by the querying modality's calling AE title + host and,
-- when it is set, sends it to the CARE worklist API as the facility_id query param.
-- Run once against the existing enabler database (default schema name: plexus_mi2).

ALTER TABLE dcm_servers
  ADD COLUMN facilityid VARCHAR(100) NULL AFTER portnumber;
