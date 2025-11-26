-- Step 1: Create database named "test"
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'test')
BEGIN
    CREATE DATABASE test;
END
GO

-- Step 2: Switch to the test database
USE test;
GO

-- Step 3: Create login "test" (server-level) if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'test')
BEGIN
    CREATE LOGIN test WITH PASSWORD = 'test123', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
END
GO

-- Step 4: Create user "test" in the test database
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'test')
BEGIN
    CREATE USER test FOR LOGIN test;
END
GO

-- Step 5: Grant read-only permissions
ALTER ROLE db_datareader ADD MEMBER test;
GO

-- Step 6: Grant CONNECT permission
GRANT CONNECT TO test;
GO

-- Verification
SELECT 'Setup Complete' AS Status;
SELECT DB_NAME() AS CurrentDatabase;
SELECT name AS Username FROM sys.database_principals WHERE name = 'test';
