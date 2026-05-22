CREATE TABLE [dbo].[Admins] (
    [Id]       INT            IDENTITY (1, 1) NOT NULL PRIMARY KEY,
    [Username] NVARCHAR (100) NOT NULL,
    [Password] NVARCHAR (100) NOT NULL
);

INSERT INTO [dbo].[Admins] ([Username], [Password]) 
VALUES ('admin', 'admin@123#');