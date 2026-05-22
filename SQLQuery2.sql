CREATE TABLE Complaints (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    CrimeType NVARCHAR(100) NOT NULL,
    IncidentDate DATETIME NOT NULL,
    Location NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    Status NVARCHAR(50) DEFAULT 'Pending Approval', -- Code ke mutabiq default status
    CitizenName NVARCHAR(100) NOT NULL,             -- Session se fetch ho raha hai
    CitizenPhone NVARCHAR(20) NOT NULL              -- Session se fetch ho raha hai
);