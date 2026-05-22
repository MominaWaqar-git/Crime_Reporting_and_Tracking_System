CREATE TABLE ComplaintAssignments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ComplaintId INT NOT NULL,
    OfficerId INT NOT NULL,
    AssignedDate DATETIME NOT NULL,
    FOREIGN KEY (ComplaintId) REFERENCES Complaints(ID) ON DELETE CASCADE,
    FOREIGN KEY (OfficerId) REFERENCES Officers(Id) ON DELETE CASCADE
);