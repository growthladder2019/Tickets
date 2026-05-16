IF COL_LENGTH('TicketSystem.Tickets', 'AssignedToUserId') IS NULL
BEGIN
    ALTER TABLE TicketSystem.Tickets ADD AssignedToUserId UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH('TicketSystem.Tickets', 'AssignedAtUtc') IS NULL
BEGIN
    ALTER TABLE TicketSystem.Tickets ADD AssignedAtUtc DATETIME2 NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Tickets_AssignedToUser')
BEGIN
    ALTER TABLE TicketSystem.Tickets
    ADD CONSTRAINT FK_Tickets_AssignedToUser
        FOREIGN KEY (AssignedToUserId) REFERENCES TicketSystem.Users(Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_AssignedToUserId' AND object_id = OBJECT_ID('TicketSystem.Tickets'))
BEGIN
    CREATE INDEX IX_Tickets_AssignedToUserId
        ON TicketSystem.Tickets (AssignedToUserId);
END
GO
