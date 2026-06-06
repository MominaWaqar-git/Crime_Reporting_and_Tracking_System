CREATE TABLE Criminals (
    CriminalID INT PRIMARY KEY IDENTITY(1,1),
    FullName NVARCHAR(100),
    Alias NVARCHAR(100),
    CrimeHistory NVARCHAR(MAX),
    Status NVARCHAR(50), -- e.g., 'In Custody', 'Wanted', 'Released'
    PhotoPath NVARCHAR(255)
);