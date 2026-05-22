CREATE TABLE Users (
    ID INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    CNIC NVARCHAR(20) NOT NULL UNIQUE,
    PhoneNumber NVARCHAR(20) NULL,
    Password NVARCHAR(255) NOT NULL -- Code comment ke mutabiq (Real life projects mein hash save hota hai)
);
