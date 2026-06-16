IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'AutoAsk')
BEGIN
    RESTORE DATABASE AutoAsk
    FROM DISK = '/var/opt/mssql/backup/AutoAsk.bak'
    WITH
        MOVE 'AutoAsk' TO '/var/opt/mssql/data/AutoAsk.mdf',
        MOVE 'AutoAsk_log' TO '/var/opt/mssql/data/AutoAsk_log.ldf',
        REPLACE;
    PRINT 'Database AutoAsk restored successfully';
END
ELSE
BEGIN
    PRINT 'Database AutoAsk already exists';
END
