IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Applications_LoginUrl')
BEGIN
    ALTER TABLE TicketSystem.Applications DROP CONSTRAINT UQ_Applications_LoginUrl;
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Applications_LoginUrl' AND object_id = OBJECT_ID('TicketSystem.Applications'))
BEGIN
    DROP INDEX IX_Applications_LoginUrl ON TicketSystem.Applications;
END
GO

IF COL_LENGTH('TicketSystem.Applications', 'LoginUrl') IS NOT NULL
BEGIN
    ALTER TABLE TicketSystem.Applications DROP COLUMN LoginUrl;
END
GO
