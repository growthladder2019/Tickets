IF NOT EXISTS (SELECT 1 FROM TicketSystem.Applications WHERE Code = 'MINDWEAVE-CORE')
BEGIN
    INSERT INTO TicketSystem.Applications (Id, Code, Name, CreatedUtc)
    VALUES (NEWID(), 'MINDWEAVE-CORE', 'Mindweave Core App', SYSUTCDATETIME());
END
GO

IF NOT EXISTS (SELECT 1 FROM TicketSystem.Applications WHERE Code = 'MINDWEAVE-ANALYTICS')
BEGIN
    INSERT INTO TicketSystem.Applications (Id, Code, Name, CreatedUtc)
    VALUES (NEWID(), 'MINDWEAVE-ANALYTICS', 'Mindweave Analytics App', SYSUTCDATETIME());
END
GO
