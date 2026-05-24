CREATE TABLE ComplaintAssignments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ComplaintId INT NOT NULL,
    OfficerId INT NOT NULL,
    AssignedDate DATETIME NOT NULL,
    
    -- Foreign Key constraints jo direct integration provide karti hain
    CONSTRAINT FK_Assignments_Complaints FOREIGN KEY (ComplaintId) REFERENCES Complaints(ID) ON DELETE CASCADE,
    CONSTRAINT FK_Assignments_Officers FOREIGN KEY (OfficerId) REFERENCES Officers(Id) ON DELETE CASCADE
);