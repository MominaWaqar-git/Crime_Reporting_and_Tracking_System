CREATE TABLE Complaints (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    CrimeType NVARCHAR(100) NOT NULL,
    IncidentDate DATETIME NOT NULL,
    Location NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) DEFAULT 'Pending Approval', -- App code logic ke mutabiq
    CitizenName NVARCHAR(100) NOT NULL,             -- Session state name mapping
    CitizenPhone NVARCHAR(20) NOT NULL              -- Session state phone mapping
);