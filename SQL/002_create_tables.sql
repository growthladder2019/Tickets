IF OBJECT_ID('TicketSystem.Applications', 'U') IS NULL
BEGIN
    CREATE TABLE TicketSystem.Applications
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Code NVARCHAR(40) NOT NULL,
        Name NVARCHAR(150) NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        CONSTRAINT UQ_Applications_Code UNIQUE (Code)
    );
END
GO

IF OBJECT_ID('TicketSystem.Users', 'U') IS NULL
BEGIN
    CREATE TABLE TicketSystem.Users
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Email NVARCHAR(320) NOT NULL,
        [Password] NVARCHAR(512) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        IsSuperAdmin BIT NOT NULL CONSTRAINT DF_Users_IsSuperAdmin DEFAULT(0),
        CreatedUtc DATETIME2 NOT NULL,
        CONSTRAINT UQ_Users_Email UNIQUE (Email)
    );
END
GO

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

IF OBJECT_ID('TicketSystem.Tickets', 'U') IS NULL
BEGIN
    CREATE TABLE TicketSystem.Tickets
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TicketNumber NVARCHAR(40) NOT NULL,
        ApplicationId UNIQUEIDENTIFIER NOT NULL,
        UserAccountId UNIQUEIDENTIFIER NOT NULL,
        RequesterName NVARCHAR(200) NOT NULL,
        Title NVARCHAR(300) NOT NULL,
        Description NVARCHAR(4000) NOT NULL,
        Message NVARCHAR(4000) NOT NULL,
        Category NVARCHAR(120) NOT NULL,
        Priority NVARCHAR(40) NOT NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        AssignedAtUtc DATETIME2 NULL,
        [Status] INT NOT NULL,
        IsDeleted BIT NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        UpdatedUtc DATETIME2 NOT NULL,
        CONSTRAINT FK_Tickets_Application FOREIGN KEY (ApplicationId) REFERENCES TicketSystem.Applications(Id),
        CONSTRAINT FK_Tickets_User FOREIGN KEY (UserAccountId) REFERENCES TicketSystem.Users(Id),
        CONSTRAINT FK_Tickets_AssignedToUser FOREIGN KEY (AssignedToUserId) REFERENCES TicketSystem.Users(Id),
        CONSTRAINT UQ_Tickets_Number UNIQUE (TicketNumber)
    );
END
GO

IF OBJECT_ID('TicketSystem.TicketHistory', 'U') IS NULL
BEGIN
    CREATE TABLE TicketSystem.TicketHistory
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TicketId UNIQUEIDENTIFIER NOT NULL,
        EventType NVARCHAR(100) NOT NULL,
        Description NVARCHAR(4000) NOT NULL,
        ActorEmail NVARCHAR(320) NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        CONSTRAINT FK_TicketHistory_Ticket FOREIGN KEY (TicketId) REFERENCES TicketSystem.Tickets(Id)
    );
END
GO

IF OBJECT_ID('TicketSystem.TicketAttachments', 'U') IS NULL
BEGIN
    CREATE TABLE TicketSystem.TicketAttachments
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        TicketId UNIQUEIDENTIFIER NOT NULL,
        BlobPath NVARCHAR(1000) NOT NULL,
        FileName NVARCHAR(300) NOT NULL,
        ContentType NVARCHAR(150) NOT NULL,
        FileSizeBytes BIGINT NOT NULL,
        IsImage BIT NOT NULL,
        UploadedByEmail NVARCHAR(320) NOT NULL,
        UploadedUtc DATETIME2 NOT NULL,
        CONSTRAINT FK_TicketAttachments_Ticket FOREIGN KEY (TicketId) REFERENCES TicketSystem.Tickets(Id)
    );
END
GO

IF OBJECT_ID('TicketSystem.RefreshTokens', 'U') IS NULL
BEGIN
    CREATE TABLE TicketSystem.RefreshTokens
    (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        UserAccountId UNIQUEIDENTIFIER NOT NULL,
        ApplicationId UNIQUEIDENTIFIER NULL,
        Token NVARCHAR(128) NOT NULL,
        ExpiresUtc DATETIME2 NOT NULL,
        CreatedUtc DATETIME2 NOT NULL,
        RevokedUtc DATETIME2 NULL,
        CONSTRAINT FK_RefreshTokens_User FOREIGN KEY (UserAccountId) REFERENCES TicketSystem.Users(Id),
        CONSTRAINT FK_RefreshTokens_Application FOREIGN KEY (ApplicationId) REFERENCES TicketSystem.Applications(Id),
        CONSTRAINT UQ_RefreshTokens_Token UNIQUE (Token)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ApplicationUsers_AppUser' AND object_id = OBJECT_ID('TicketSystem.ApplicationUsers'))
BEGIN
    CREATE INDEX IX_ApplicationUsers_AppUser
        ON TicketSystem.ApplicationUsers (ApplicationId, UserAccountId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_AppUserStatusCreatedUtc' AND object_id = OBJECT_ID('TicketSystem.Tickets'))
BEGIN
    CREATE INDEX IX_Tickets_AppUserStatusCreatedUtc
        ON TicketSystem.Tickets (ApplicationId, UserAccountId, [Status], CreatedUtc DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_AssignedToUserId' AND object_id = OBJECT_ID('TicketSystem.Tickets'))
BEGIN
    CREATE INDEX IX_Tickets_AssignedToUserId
        ON TicketSystem.Tickets (AssignedToUserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketHistory_TicketIdCreatedUtc' AND object_id = OBJECT_ID('TicketSystem.TicketHistory'))
BEGIN
    CREATE INDEX IX_TicketHistory_TicketIdCreatedUtc
        ON TicketSystem.TicketHistory (TicketId, CreatedUtc);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TicketAttachments_TicketIdUploadedUtc' AND object_id = OBJECT_ID('TicketSystem.TicketAttachments'))
BEGIN
    CREATE INDEX IX_TicketAttachments_TicketIdUploadedUtc
        ON TicketSystem.TicketAttachments (TicketId, UploadedUtc DESC);
END
GO
