-- Fuse Inventory SQL integration lab, deterministic fixture version 1.
-- Run ONLY inside the dedicated lab SQL Server, with Fuse stopped during reset.
-- Rerunning restores the two marked databases and the exact fixture logins below.
-- No other database or login is dropped. A same-named unmarked database aborts reset.
-- SQLCMD variables: LabInspectorPassword, LabManagerPassword, LabUserPassword.
-- The runner generates passwords without apostrophes or SQLCMD substitution syntax.
-- Passwords are never committed or printed. GrantOnly shares the manager fixture password.
:on error exit
SET NOCOUNT ON;
SET XACT_ABORT ON;
USE [master];
GO

-- Validate every existing target before dropping either database.
DECLARE @DatabaseName sysname, @Sql nvarchar(max), @Marked bit;
DECLARE TargetDatabases CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM (VALUES (N'FuseLab_App'), (N'FuseLab_Reporting')) AS targets(name);
OPEN TargetDatabases;
FETCH NEXT FROM TargetDatabases INTO @DatabaseName;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF DB_ID(@DatabaseName) IS NOT NULL
    BEGIN
        SET @Marked = 0;
        SET @Sql = N'SELECT @Marked = 1 FROM ' + QUOTENAME(@DatabaseName)
            + N'.sys.extended_properties WHERE class = 0 AND name = N''FuseInventoryIntegrationLab'''
            + N' AND CONVERT(nvarchar(128), value) = N''sql-lab-v1'';';
        EXEC sys.sp_executesql @Sql, N'@Marked bit OUTPUT', @Marked OUTPUT;
        IF @Marked = 0
        BEGIN
            DECLARE @Message nvarchar(2048) = N'Refusing to reset unmarked database ' + @DatabaseName;
            THROW 51000, @Message, 1;
        END;
    END;
    FETCH NEXT FROM TargetDatabases INTO @DatabaseName;
END;
CLOSE TargetDatabases;
DEALLOCATE TargetDatabases;
GO

IF DB_ID(N'FuseLab_App') IS NOT NULL
BEGIN
    ALTER DATABASE [FuseLab_App] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [FuseLab_App];
END;
IF DB_ID(N'FuseLab_Reporting') IS NOT NULL
BEGIN
    ALTER DATABASE [FuseLab_Reporting] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [FuseLab_Reporting];
END;
GO
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_Inspector' AND type = 'S') DROP LOGIN [FuseLab_Inspector];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_Manager' AND type = 'S') DROP LOGIN [FuseLab_Manager];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_GrantOnly' AND type = 'S') DROP LOGIN [FuseLab_GrantOnly];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_InSync' AND type = 'S') DROP LOGIN [FuseLab_InSync];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_MissingGrant' AND type = 'S') DROP LOGIN [FuseLab_MissingGrant];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_ExtraGrant' AND type = 'S') DROP LOGIN [FuseLab_ExtraGrant];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_MissingUser' AND type = 'S') DROP LOGIN [FuseLab_MissingUser];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_Orphan' AND type = 'S') DROP LOGIN [FuseLab_Orphan];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_CustomRole' AND type = 'S') DROP LOGIN [FuseLab_CustomRole];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_ObjectReader' AND type = 'S') DROP LOGIN [FuseLab_ObjectReader];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_SchemaReader' AND type = 'S') DROP LOGIN [FuseLab_SchemaReader];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_ReportingReader' AND type = 'S') DROP LOGIN [FuseLab_ReportingReader];
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'FuseLab_MissingLogin' AND type = 'S') DROP LOGIN [FuseLab_MissingLogin];
GO
CREATE DATABASE [FuseLab_App];
GO
CREATE DATABASE [FuseLab_Reporting];
GO
ALTER DATABASE [FuseLab_App] SET RECOVERY SIMPLE;
ALTER DATABASE [FuseLab_Reporting] SET RECOVERY SIMPLE;
GO

-- No integration identity is sysadmin. Only Manager may create server logins.
-- Metadata/CONNECT ANY DATABASE rights apply only inside this lab SQL server;
-- data modification and database ownership are limited to the two lab databases.
CREATE LOGIN [FuseLab_Inspector] WITH PASSWORD = '$(LabInspectorPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
GRANT VIEW ANY DATABASE TO [FuseLab_Inspector];
GRANT VIEW ANY DEFINITION TO [FuseLab_Inspector];
GRANT CONNECT ANY DATABASE TO [FuseLab_Inspector];
CREATE LOGIN [FuseLab_Manager] WITH PASSWORD = '$(LabManagerPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
GRANT VIEW ANY DATABASE TO [FuseLab_Manager];
GRANT VIEW ANY DEFINITION TO [FuseLab_Manager];
GRANT CONNECT ANY DATABASE TO [FuseLab_Manager];
CREATE LOGIN [FuseLab_GrantOnly] WITH PASSWORD = '$(LabManagerPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
GRANT VIEW ANY DATABASE TO [FuseLab_GrantOnly];
GRANT VIEW ANY DEFINITION TO [FuseLab_GrantOnly];
GRANT CONNECT ANY DATABASE TO [FuseLab_GrantOnly];
GRANT ALTER ANY LOGIN TO [FuseLab_Manager];
GO
CREATE LOGIN [FuseLab_InSync] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
CREATE LOGIN [FuseLab_MissingGrant] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
CREATE LOGIN [FuseLab_ExtraGrant] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
CREATE LOGIN [FuseLab_MissingUser] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
CREATE LOGIN [FuseLab_Orphan] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
CREATE LOGIN [FuseLab_CustomRole] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
CREATE LOGIN [FuseLab_ObjectReader] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
CREATE LOGIN [FuseLab_SchemaReader] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
CREATE LOGIN [FuseLab_ReportingReader] WITH PASSWORD = '$(LabUserPassword)', CHECK_POLICY = OFF, CHECK_EXPIRATION = OFF;
-- FuseLab_MissingLogin intentionally remains absent until account-creation validation.
GO

USE [FuseLab_App];
GO
EXEC sys.sp_addextendedproperty @name = N'FuseInventoryIntegrationLab', @value = N'sql-lab-v1';
GO
CREATE SCHEMA [app] AUTHORIZATION [dbo];
GO
CREATE TABLE [dbo].[Inventory] (
    [Id] int NOT NULL PRIMARY KEY,
    [Name] nvarchar(100) NOT NULL,
    [Quantity] int NOT NULL
);
CREATE TABLE [app].[Orders] (
    [Id] int NOT NULL PRIMARY KEY,
    [ItemId] int NOT NULL REFERENCES [dbo].[Inventory]([Id]),
    [Quantity] int NOT NULL,
    [Status] nvarchar(20) NOT NULL
);
INSERT [dbo].[Inventory] ([Id], [Name], [Quantity]) VALUES
    (1, N'Lab widget', 12), (2, N'Lab cable', 30), (3, N'Lab adapter', 7);
INSERT [app].[Orders] ([Id], [ItemId], [Quantity], [Status]) VALUES
    (1, 1, 2, N'Open'), (2, 2, 5, N'Closed'), (3, 3, 1, N'Open');
GO
CREATE VIEW [app].[OpenOrders] AS
    SELECT [Id], [ItemId], [Quantity] FROM [app].[Orders] WHERE [Status] = N'Open';
GO
CREATE PROCEDURE [app].[GetOrders]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Id], [ItemId], [Quantity], [Status] FROM [app].[Orders] ORDER BY [Id];
END;
GO
CREATE USER [FuseLab_Manager] FOR LOGIN [FuseLab_Manager];
GRANT CONNECT TO [FuseLab_Manager];
CREATE USER [FuseLab_GrantOnly] FOR LOGIN [FuseLab_GrantOnly];
GRANT CONNECT TO [FuseLab_GrantOnly];
CREATE USER [FuseLab_InSync] FOR LOGIN [FuseLab_InSync];
GRANT CONNECT TO [FuseLab_InSync];
CREATE USER [FuseLab_MissingGrant] FOR LOGIN [FuseLab_MissingGrant];
GRANT CONNECT TO [FuseLab_MissingGrant];
CREATE USER [FuseLab_ExtraGrant] FOR LOGIN [FuseLab_ExtraGrant];
GRANT CONNECT TO [FuseLab_ExtraGrant];
CREATE USER [FuseLab_Orphan] FOR LOGIN [FuseLab_Orphan];
GRANT CONNECT TO [FuseLab_Orphan];
CREATE USER [FuseLab_CustomRole] FOR LOGIN [FuseLab_CustomRole];
GRANT CONNECT TO [FuseLab_CustomRole];
CREATE USER [FuseLab_ObjectReader] FOR LOGIN [FuseLab_ObjectReader];
GRANT CONNECT TO [FuseLab_ObjectReader];
CREATE USER [FuseLab_SchemaReader] FOR LOGIN [FuseLab_SchemaReader];
GRANT CONNECT TO [FuseLab_SchemaReader];
ALTER ROLE [db_owner] ADD MEMBER [FuseLab_Manager];
ALTER ROLE [db_owner] ADD MEMBER [FuseLab_GrantOnly];

-- Direct database grants keep ordinary drift cases unambiguous.
GRANT SELECT TO [FuseLab_InSync];
GRANT SELECT TO [FuseLab_MissingGrant];
GRANT SELECT, UPDATE TO [FuseLab_ExtraGrant];
GRANT SELECT TO [FuseLab_Orphan];
-- Desired MissingGrant: CONNECT+SELECT+INSERT (INSERT is missing).
-- Desired ExtraGrant: CONNECT+SELECT (UPDATE is extra).
-- MissingUser has a server login but intentionally no user in either lab DB.

-- Boundary cases deliberately retain native scope and inherited privileges.
-- Do not create matching schemas in master or flatten custom-role permissions.
CREATE ROLE [FuseLab_Reader] AUTHORIZATION [dbo];
GRANT SELECT ON SCHEMA::[app] TO [FuseLab_Reader];
ALTER ROLE [FuseLab_Reader] ADD MEMBER [FuseLab_CustomRole];
GRANT SELECT ON OBJECT::[dbo].[Inventory] TO [FuseLab_ObjectReader];
GRANT SELECT ON SCHEMA::[app] TO [FuseLab_SchemaReader];
GO

USE [FuseLab_Reporting];
GO
EXEC sys.sp_addextendedproperty @name = N'FuseInventoryIntegrationLab', @value = N'sql-lab-v1';
GO
CREATE SCHEMA [reporting] AUTHORIZATION [dbo];
GO
CREATE TABLE [reporting].[DailyTotals] (
    [Day] date NOT NULL PRIMARY KEY,
    [OrderCount] int NOT NULL,
    [GrossAmount] decimal(12,2) NOT NULL
);
INSERT [reporting].[DailyTotals] ([Day], [OrderCount], [GrossAmount]) VALUES
    ('20260101', 4, 120.00), ('20260102', 7, 250.50);
GO
CREATE VIEW [reporting].[Totals] AS
    SELECT [Day], [OrderCount], [GrossAmount] FROM [reporting].[DailyTotals];
GO
CREATE PROCEDURE [reporting].[GetTotals]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT [Day], [OrderCount], [GrossAmount] FROM [reporting].[DailyTotals] ORDER BY [Day];
END;
GO
CREATE USER [FuseLab_Manager] FOR LOGIN [FuseLab_Manager];
CREATE USER [FuseLab_GrantOnly] FOR LOGIN [FuseLab_GrantOnly];
CREATE USER [FuseLab_ReportingReader] FOR LOGIN [FuseLab_ReportingReader];
ALTER ROLE [db_owner] ADD MEMBER [FuseLab_Manager];
ALTER ROLE [db_owner] ADD MEMBER [FuseLab_GrantOnly];
GRANT CONNECT TO [FuseLab_Manager];
GRANT CONNECT TO [FuseLab_GrantOnly];
GRANT CONNECT, SELECT TO [FuseLab_ReportingReader];
GO

-- Publish completion only after every fixture has been created successfully.
-- A single transaction ensures a failed final batch marks neither database ready.
BEGIN TRANSACTION;
EXEC [FuseLab_App].sys.sp_addextendedproperty @name = N'FuseInventorySeedComplete', @value = N'sql-lab-v1';
EXEC [FuseLab_Reporting].sys.sp_addextendedproperty @name = N'FuseInventorySeedComplete', @value = N'sql-lab-v1';
COMMIT TRANSACTION;
GO

USE [master];
GO
PRINT 'Fuse SQL lab restored: two databases, three integration identities and nine synthetic workload logins.';
PRINT 'Missing-user and missing-login fixtures remain absent at their respective scopes.';
