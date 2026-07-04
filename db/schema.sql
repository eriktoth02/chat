-- =====================================================================
--  Chat application - MySQL schema
--  NOTE: The API creates these tables automatically on first run
--        (EF Core EnsureCreated). This script is provided only for
--        reference or if you prefer to create the database manually.
-- =====================================================================

CREATE DATABASE IF NOT EXISTS chatapp
    CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE chatapp;

CREATE TABLE IF NOT EXISTS Users (
    Id              INT AUTO_INCREMENT PRIMARY KEY,
    Username        VARCHAR(64)  NOT NULL,
    DisplayName     VARCHAR(64)  NULL,
    PasswordHash    VARCHAR(255) NOT NULL,
    Token           VARCHAR(64)  NULL,
    CreatedUtc      DATETIME(6)  NOT NULL,
    LastSeenUtc     DATETIME(6)  NOT NULL,
    CurrentRoomId   INT          NULL,
    TypingUntilUtc  DATETIME(6)  NULL,
    UNIQUE KEY IX_Users_Username (Username),
    KEY        IX_Users_Token    (Token)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Rooms (
    Id          INT AUTO_INCREMENT PRIMARY KEY,
    Name        VARCHAR(64) NOT NULL,
    CreatedUtc  DATETIME(6) NOT NULL,
    UNIQUE KEY IX_Rooms_Name (Name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS Messages (
    Id        INT AUTO_INCREMENT PRIMARY KEY,
    RoomId    INT           NOT NULL,
    UserId    INT           NOT NULL,
    Content   VARCHAR(2000) NOT NULL,
    SentUtc   DATETIME(6)   NOT NULL,
    EditedUtc      DATETIME(6)  NULL,
    AttachmentName VARCHAR(260) NULL,
    AttachmentUrl  VARCHAR(400) NULL,
    KEY IX_Messages_RoomId_Id (RoomId, Id),
    CONSTRAINT FK_Messages_Rooms FOREIGN KEY (RoomId) REFERENCES Rooms(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Messages_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS DirectMessages (
    Id           INT AUTO_INCREMENT PRIMARY KEY,
    SenderId     INT           NOT NULL,
    RecipientId  INT           NOT NULL,
    Content      VARCHAR(2000) NOT NULL,
    SentUtc      DATETIME(6)   NOT NULL,
    DeliveredUtc DATETIME(6)   NULL,
    ReadUtc      DATETIME(6)   NULL,
    KEY IX_DM_Pair (SenderId, RecipientId, Id),
    KEY IX_DM_Recipient_Read (RecipientId, ReadUtc)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Default room
INSERT INTO Rooms (Name, CreatedUtc)
SELECT 'General', UTC_TIMESTAMP(6)
WHERE NOT EXISTS (SELECT 1 FROM Rooms WHERE Name = 'General');
