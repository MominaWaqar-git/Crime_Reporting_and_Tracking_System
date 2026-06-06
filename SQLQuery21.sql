CREATE TABLE Messages (
    MessageID INT PRIMARY KEY IDENTITY(1,1),
    ComplaintID INT, -- Kis case ke liye baat ho rahi hai
    SenderID INT,    -- Message bhejne wale ki ID
    SenderType NVARCHAR(20), -- 'Citizen' ya 'Officer'
    MessageContent NVARCHAR(MAX),
    Timestamp DATETIME DEFAULT GETDATE()
);