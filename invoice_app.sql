
CREATE TABLE Invoices (
    Id SERIAL PRIMARY KEY,
    InvoiceNumber VARCHAR(100),
    InvoiceDate DATE,
    CustomerName VARCHAR(200),
    TotalAmount DECIMAL(10,2),
    VAT DECIMAL(10,2)
);

CREATE TABLE InvoiceDetails (
    Id SERIAL PRIMARY KEY,
    InvoiceId INT REFERENCES Invoices(Id) ON DELETE CASCADE,
    Description TEXT,
    Quantity INT,
    UnitPrice DECIMAL(10,2),
    LineTotal DECIMAL(10,2)
);