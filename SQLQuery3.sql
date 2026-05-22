CREATE TABLE PublicAlerts (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(150) NOT NULL,
    Description NVARCHAR(MAX) NOT NULL,
    AlertLevel NVARCHAR(50) NOT NULL, -- e.g., High, Medium, Low
    Location NVARCHAR(255) NOT NULL,
    DateCreated DATETIME DEFAULT GETDATE() -- Code mein GETDATE() use hua hai
);