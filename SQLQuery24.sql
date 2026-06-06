CREATE TABLE Criminals (
    CriminalID INT PRIMARY KEY IDENTITY(1,1),
    FullName NVARCHAR(100) NOT NULL,
    CrimeHistory NVARCHAR(MAX),
    Status NVARCHAR(50)
);