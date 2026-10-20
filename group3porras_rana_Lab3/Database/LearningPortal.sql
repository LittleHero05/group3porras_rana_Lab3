--Delete the LearningPortal database if it exists.
IF EXISTS (SELECT * FROM sys.databases WHERE name = 'LearningPortal')
BEGIN
    DROP DATABASE LearningPortal;
END
GO

CREATE DATABASE LearningPortal;
GO

USE LearningPortal;
GO

CREATE TABLE dbo.Users(
    UserID       int IDENTITY(1,1) PRIMARY KEY,
    Username     varchar(50)  NOT NULL UNIQUE,
    Email        varchar(100) NOT NULL UNIQUE,
    FirstName    varchar(50)  NOT NULL,
    LastName     varchar(50)  NOT NULL,
    PasswordHash varchar(255) NOT NULL,
    Role         varchar(20)  NOT NULL CHECK (Role IN ('Instructor','Student')),
    CreatedDate  datetime2    NOT NULL DEFAULT SYSUTCDATETIME());
GO

CREATE TABLE dbo.Categories(
    CategoryID   int IDENTITY(1,1) PRIMARY KEY,
    CategoryName varchar(50) NOT NULL UNIQUE);
GO

CREATE TABLE dbo.LearningResources(
    ResourceID   int IDENTITY(1,1) PRIMARY KEY,
    Title        varchar(150)  NOT NULL,
    Description  varchar(1000) NULL,
    CategoryID   int NOT NULL REFERENCES dbo.Categories(CategoryID),
    AuthorID     int NOT NULL REFERENCES dbo.Users(UserID),
    CreatedDate  datetime2     NOT NULL DEFAULT SYSUTCDATETIME(),
    S3Key        varchar(300)  NOT NULL,
    FileName     varchar(255)  NULL,
    ContentType  varchar(100)  NULL,
    ViewCount    int           NOT NULL DEFAULT 0);
GO

INSERT INTO Categories (CategoryName) VALUES
 ('Cloud Computing'),
 ('Databases'),
 ('Programming'),
 ('DevOps'),
 ('Software Testing');

-- PasswordHash values: register these users once through the app,
-- then copy the generated hashes here so the script logins work.
INSERT INTO Users (Username, Email, FirstName, LastName, PasswordHash, Role) VALUES
 ('jsmith',  'jsmith@centennial.ca',     'John',    'Smith',  '<hash>', 'Instructor'),
 ('alee',    'alee@centennial.ca',       'Anna',    'Lee',    '<hash>', 'Instructor'),
 ('mchen',   'mchen@centennial.ca',      'Michael', 'Chen',   '<hash>', 'Instructor'),
 ('cjones',  'cjones@my.centennial.ca',  'Cindy',   'Jones',  '<hash>', 'Student'),
 ('tlee',    'tlee@my.centennial.ca',    'Trevor',  'Lee',    '<hash>', 'Student'),
 ('hbrowns', 'hbrowns@my.centennial.ca', 'Howard',  'Browns', '<hash>', 'Student');

INSERT INTO LearningResources (Title, Description, CategoryID, AuthorID, CreatedDate, S3Key, FileName, ContentType, ViewCount) VALUES
 ('Intro to AWS S3',           'Buckets, objects and permissions',  1, 1, '2026-10-01', 'resources/intro-s3.pdf',          'intro-s3.pdf',          'application/pdf', 42),
 ('DynamoDB Data Modeling',    'Partition and sort keys explained', 2, 2, '2026-10-05', 'resources/dynamodb-modeling.pdf', 'dynamodb-modeling.pdf', 'application/pdf', 17),
 ('ASP.NET Core MVC Basics',   'Controllers, views and models',     3, 1, '2026-10-08', 'resources/mvc-basics.pptx',       'mvc-basics.pptx',       'application/vnd.openxmlformats-officedocument.presentationml.presentation', 30),
 ('CI/CD with GitHub Actions', 'Build and deploy pipelines',        4, 3, '2026-10-12', 'resources/cicd.pdf',              'cicd.pdf',              'application/pdf', 8),
 ('Unit Testing with xUnit',   'Writing your first tests',          5, 2, '2026-10-15', 'resources/xunit.png',             'xunit.png',             'image/png', 25);
GO
