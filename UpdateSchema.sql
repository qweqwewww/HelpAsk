-- Equipment is now linked directly to Request instead of Service
-- Run this on the AutoAsk database

-- 1. Add FK from Requests to Equipment (column already exists)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Requests_Equipment_EquipmentId')
    ALTER TABLE Requests
        ADD CONSTRAINT FK_Requests_Equipment_EquipmentId
        FOREIGN KEY (EquipmentId) REFERENCES Equipment(Id);

-- 2. Migrate existing EquipmentId from Services to Requests
-- For each pending request that has a service with EquipmentId set,
-- copy that EquipmentId to the request (if not already set)
UPDATE r
SET r.EquipmentId = s.EquipmentId
FROM Requests r
INNER JOIN Services s ON r.ServiceId = s.Id
WHERE r.EquipmentId IS NULL AND s.EquipmentId IS NOT NULL;

-- 3. Remove EquipmentId from Services
ALTER TABLE Services DROP CONSTRAINT FK_Services_Equipment;
ALTER TABLE Services DROP COLUMN EquipmentId;
