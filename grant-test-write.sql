-- Grant write permissions to test account in test database

USE test;
GO

-- Add to db_datawriter role for INSERT, UPDATE, DELETE on all tables
ALTER ROLE db_datawriter ADD MEMBER test;
PRINT 'User "test" added to db_datawriter role (write access).';
GO

-- Alternatively, grant explicit permissions if you prefer granular control:
-- GRANT INSERT, UPDATE, DELETE ON SCHEMA::dbo TO test;
-- PRINT 'INSERT, UPDATE, DELETE permissions granted on dbo schema.';
-- GO

-- Verification
SELECT 
    dp.name AS UserName,
    r.name AS RoleName
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members drm ON dp.principal_id = drm.member_principal_id
LEFT JOIN sys.database_principals r ON drm.role_principal_id = r.principal_id
WHERE dp.name = 'test'
ORDER BY RoleName;

PRINT 'Setup complete! User "test" now has read and write permissions.';
