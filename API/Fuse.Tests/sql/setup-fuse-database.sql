-- Setup script for Fuse Database
-- Creates database, sample table, and SQL accounts with different permission levels

-- Create the database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'fuseDatabase')
BEGIN
    CREATE DATABASE fuseDatabase;
END
GO

USE fuseDatabase;
GO

-- Create a simple table for demonstration
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Inventory')
BEGIN
    CREATE TABLE Inventory (
        Id INT PRIMARY KEY IDENTITY(1,1),
        ItemName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500),
        Quantity INT DEFAULT 0,
        CreatedDate DATETIME DEFAULT GETDATE(),
        ModifiedDate DATETIME DEFAULT GETDATE()
    );
END
GO

-- Create SQL Server logins (if they don't exist)
-- Using CHECK_POLICY = OFF for testing environments
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'fuseRead')
BEGIN
    CREATE LOGIN fuseRead WITH PASSWORD = 'fuseread', CHECK_POLICY = OFF;
END
GO

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'fuseWrite')
BEGIN
    CREATE LOGIN fuseWrite WITH PASSWORD = 'fusewrite', CHECK_POLICY = OFF;
END
GO

IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'fuseOwn')
BEGIN
    CREATE LOGIN fuseOwn WITH PASSWORD = 'fuseown', CHECK_POLICY = OFF;
END
GO

-- Switch to the fuseDatabase to create users and assign permissions
USE fuseDatabase;
GO

-- Create database users for the logins
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'fuseRead')
BEGIN
    CREATE USER fuseRead FOR LOGIN fuseRead;
END
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'fuseWrite')
BEGIN
    CREATE USER fuseWrite FOR LOGIN fuseWrite;
END
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'fuseOwn')
BEGIN
    CREATE USER fuseOwn FOR LOGIN fuseOwn;
END
GO

-- Assign permissions
-- fuseRead: Read-only access
ALTER ROLE db_datareader ADD MEMBER fuseRead;
GO

-- fuseWrite: Read and Write access
ALTER ROLE db_datareader ADD MEMBER fuseWrite;
ALTER ROLE db_datawriter ADD MEMBER fuseWrite;
GO

-- fuseOwn: Read, Write, and Owner access (db_owner has full control)
ALTER ROLE db_owner ADD MEMBER fuseOwn;
GO

-- Insert sample data
INSERT INTO Inventory (ItemName, Description, Quantity)
VALUES 
    ('Server A', 'Production web server', 2),
    ('Database B', 'PostgreSQL instance', 1),
    ('Load Balancer C', 'HAProxy load balancer', 3);
GO

-- Display summary
PRINT 'Database setup complete!';
PRINT 'Database: fuseDatabase';
PRINT 'Table: Inventory';
PRINT '';
PRINT 'SQL Accounts created:';
PRINT '  - fuseRead: Read-only access (db_datareader)';
PRINT '  - fuseWrite: Read and Write access (db_datareader, db_datawriter)';
PRINT '  - fuseOwn: Full owner access (db_owner)';
GO
