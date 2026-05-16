IF OBJECT_ID('TicketSystem.ApplicationUsers', 'U') IS NULL
BEGIN
    CREATE TABLE TicketSystem.ApplicationUsers
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ApplicationId UNIQUEIDENTIFIER NOT NULL,
        UserAccountId UNIQUEIDENTIFIER NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        CONSTRAINT FK_ApplicationUsers_Application FOREIGN KEY (ApplicationId) REFERENCES TicketSystem.Applications(Id),
        CONSTRAINT FK_ApplicationUsers_User FOREIGN KEY (UserAccountId) REFERENCES TicketSystem.Users(Id),
        CONSTRAINT UQ_ApplicationUsers_App_User UNIQUE (ApplicationId, UserAccountId)
    );
END
GO

IF COL_LENGTH('TicketSystem.Users', 'ApplicationId') IS NOT NULL
BEGIN
    INSERT INTO TicketSystem.ApplicationUsers (Id, ApplicationId, UserAccountId, CreatedUtc)
    SELECT NEWID(), u.ApplicationId, u.Id, SYSUTCDATETIME()
    FROM TicketSystem.Users u
    LEFT JOIN TicketSystem.ApplicationUsers au
        ON au.ApplicationId = u.ApplicationId AND au.UserAccountId = u.Id
    WHERE au.Id IS NULL;

    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Users_App_Email')
    BEGIN
        ALTER TABLE TicketSystem.Users DROP CONSTRAINT UQ_Users_App_Email;
    END

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Application')
    BEGIN
        ALTER TABLE TicketSystem.Users DROP CONSTRAINT FK_Users_Application;
    END

    ALTER TABLE TicketSystem.Users DROP COLUMN ApplicationId;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Users_Email')
BEGIN
    ALTER TABLE TicketSystem.Users ADD CONSTRAINT UQ_Users_Email UNIQUE (Email);
END
GO

IF COL_LENGTH('TicketSystem.RefreshTokens', 'ApplicationId') IS NULL
BEGIN
    ALTER TABLE TicketSystem.RefreshTokens ADD ApplicationId UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH('TicketSystem.Users', 'ApplicationId') IS NULL
BEGIN
    ;WITH FirstLink AS
    (
        SELECT
            au.UserAccountId,
            au.ApplicationId,
            ROW_NUMBER() OVER (PARTITION BY au.UserAccountId ORDER BY au.CreatedUtc, au.Id) AS rn
        FROM TicketSystem.ApplicationUsers au
    )
    UPDATE rt
    SET rt.ApplicationId = fl.ApplicationId
    FROM TicketSystem.RefreshTokens rt
    INNER JOIN FirstLink fl
        ON fl.UserAccountId = rt.UserAccountId
    WHERE rt.ApplicationId IS NULL
      AND fl.rn = 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RefreshTokens_Application')
BEGIN
    ALTER TABLE TicketSystem.RefreshTokens
    ADD CONSTRAINT FK_RefreshTokens_Application
        FOREIGN KEY (ApplicationId) REFERENCES TicketSystem.Applications(Id);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApplicationUsers_AppUser' AND object_id = OBJECT_ID('TicketSystem.ApplicationUsers'))
BEGIN
    CREATE INDEX IX_ApplicationUsers_AppUser
        ON TicketSystem.ApplicationUsers (ApplicationId, UserAccountId);
END
GO
