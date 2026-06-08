CREATE TABLE ChatMessages (
    MessageId INT IDENTITY(1,1) PRIMARY KEY,
    ChatId INT FOREIGN KEY REFERENCES GroupChats(ChatId) ON DELETE CASCADE,
    SenderType VARCHAR(50), -- 'Admin', 'Officer', 'Citizen', 'System'
    SenderName NVARCHAR(100),
    MessageText NVARCHAR(MAX),
    Timestamp DATETIME DEFAULT GETDATE(),
    IsDeleted BIT DEFAULT 0,  -- [ADD KIYA] Taake individual message delete ho sake
    IsRead BIT DEFAULT 0      -- [ADD KIYA] Blue ticks / Seen status ke liye

);