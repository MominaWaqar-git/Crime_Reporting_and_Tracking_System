CREATE TABLE [dbo].[ChatMessages] (
    [MessageId]   INT            IDENTITY (1, 1) NOT NULL,
    [ChatId]      INT            NOT NULL,
    [SenderType]  NVARCHAR (50)  NOT NULL, -- "Officer", "Citizen", ya "Admin"
    [SenderName]  NVARCHAR (100) NOT NULL,
    [MessageText] NVARCHAR (MAX) NOT NULL,
    [Timestamp]   DATETIME       DEFAULT (getdate()) NULL,
    [IsRead]      BIT            DEFAULT ((0)) NULL,
    [IsDeleted]   BIT            DEFAULT ((0)) NULL,
    PRIMARY KEY CLUSTERED ([MessageId] ASC),
    FOREIGN KEY ([ChatId]) REFERENCES [dbo].[GroupChats] ([ChatId]) ON DELETE CASCADE
);