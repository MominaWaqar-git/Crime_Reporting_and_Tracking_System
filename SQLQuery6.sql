Use CrimeVisionDB;
Go
-- 1. Image path add karne ke liye (Optional/NULL allowed)
ALTER TABLE [dbo].[PublicAlerts] 
ADD [AttachmentPath] NVARCHAR(MAX) NULL;

-- 2. Expiry Date add karne ke liye
ALTER TABLE [dbo].[PublicAlerts] 
ADD [ExpiryDate] DATETIME NOT NULL;