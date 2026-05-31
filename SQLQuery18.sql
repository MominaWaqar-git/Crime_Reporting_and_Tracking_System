CREATE TABLE ChatMessages (
    MessageId INT IDENTITY(1,1) PRIMARY KEY,
    ChatId INT FOREIGN KEY REFERENCES GroupChats(ChatId),
    SenderType VARCHAR(50), -- 'System', 'Admin', 'Officer', 'Citizen'
    SenderName NVARCHAR(100),
    MessageText NVARCHAR(MAX),
    Timestamp DATETIME DEFAULT GETDATE()
);