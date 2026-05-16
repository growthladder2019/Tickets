IF COL_LENGTH('TicketSystem.Users', 'IsSuperAdmin') IS NULL
BEGIN
    ALTER TABLE TicketSystem.Users ADD IsSuperAdmin BIT NOT NULL CONSTRAINT DF_Users_IsSuperAdmin DEFAULT(0);
END
GO

IF COL_LENGTH('TicketSystem.Tickets', 'Message') IS NULL
BEGIN
    ALTER TABLE TicketSystem.Tickets ADD Message NVARCHAR(4000) NOT NULL CONSTRAINT DF_Tickets_Message DEFAULT('');
END
GO

IF COL_LENGTH('TicketSystem.RefreshTokens', 'ApplicationId') IS NOT NULL
BEGIN
    IF EXISTS
    (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID('TicketSystem.RefreshTokens')
          AND name = 'ApplicationId'
          AND is_nullable = 0
    )
    BEGIN
        ALTER TABLE TicketSystem.RefreshTokens ALTER COLUMN ApplicationId UNIQUEIDENTIFIER NULL;
    END
END
GO
