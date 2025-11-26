-- Remove test account permissions from master database

USE master;
GO

-- Remove from db_datareader role if member
IF IS_ROLEMEMBER('db_datareader', 'test') = 1
BEGIN
    ALTER ROLE db_datareader DROP MEMBER test;
    PRINT 'Removed test from db_datareader role in master.';
END
GO

-- Revoke any explicit permissions
REVOKE ALL FROM test;
PRINT 'Revoked all explicit permissions from test in master.';
GO

-- Drop the user from master database
IF EXISTS (SELECT * FROM sys.database_principals WHERE name = 'test')
BEGIN
    DROP USER test;
    PRINT 'Dropped user test from master database.';
END
ELSE
BEGIN
    PRINT 'User test does not exist in master database.';
END
GO

-- Verification
SELECT 'Cleanup Complete' AS Status;
SELECT name AS RemainingUsers FROM sys.database_principals WHERE name = 'test';
