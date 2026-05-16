IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'TicketSystem')
BEGIN
    EXEC('CREATE SCHEMA TicketSystem');
END
GO
