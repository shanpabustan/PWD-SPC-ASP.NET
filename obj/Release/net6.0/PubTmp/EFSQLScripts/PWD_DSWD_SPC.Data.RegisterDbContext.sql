IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [Accounts] (
        [Id] uniqueidentifier NOT NULL,
        [ApplicantType] nvarchar(max) NOT NULL,
        [DisabilityNumber] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastName] nvarchar(max) NOT NULL,
        [FirstName] nvarchar(max) NOT NULL,
        [MiddleName] nvarchar(max) NOT NULL,
        [suffix] nvarchar(max) NULL,
        [DateOfBirth] datetime2 NOT NULL,
        [Gender] nvarchar(max) NOT NULL,
        [CivilStatus] nvarchar(max) NOT NULL,
        [FatherLastName] nvarchar(max) NOT NULL,
        [FatherFirstName] nvarchar(max) NOT NULL,
        [FatherMiddleName] nvarchar(max) NOT NULL,
        [MotherLastName] nvarchar(max) NOT NULL,
        [MotherFirstName] nvarchar(max) NOT NULL,
        [MotherMiddleName] nvarchar(max) NOT NULL,
        [GuardianLastName] nvarchar(max) NOT NULL,
        [GuardianFirstName] nvarchar(max) NOT NULL,
        [GuardianMiddleName] nvarchar(max) NOT NULL,
        [TypeOfDisability] nvarchar(max) NOT NULL,
        [CauseOfDisability] nvarchar(max) NOT NULL,
        [HouseNoAndStreet] nvarchar(max) NOT NULL,
        [AD] nvarchar(max) NOT NULL,
        [Barangay] nvarchar(max) NOT NULL,
        [Municipality] nvarchar(max) NOT NULL,
        [Province] nvarchar(max) NOT NULL,
        [Region] nvarchar(max) NOT NULL,
        [LandlineNo] nvarchar(max) NULL,
        [MobileNo] nvarchar(max) NOT NULL,
        [EmailAddress] nvarchar(max) NOT NULL,
        [EducationalAttainment] nvarchar(max) NOT NULL,
        [StatusOfEmployment] nvarchar(max) NULL,
        [CategoryOfEmployment] nvarchar(max) NULL,
        [TypeOfEmployment] nvarchar(max) NULL,
        [Occupation] nvarchar(max) NULL,
        [OtherOccupation] nvarchar(max) NULL,
        [OrganizationAffiliated] nvarchar(max) NULL,
        [ContactPerson] nvarchar(max) NULL,
        [OfficeAddress] nvarchar(max) NULL,
        [OfficeTelNo] nvarchar(max) NULL,
        [SSSNo] nvarchar(max) NULL,
        [GSISNo] nvarchar(max) NULL,
        [PagIBIGNo] nvarchar(max) NULL,
        [PSNNo] nvarchar(max) NULL,
        [PhilHealthNo] nvarchar(max) NULL,
        [AccomplishByLastName] nvarchar(max) NOT NULL,
        [AccomplishByFirstName] nvarchar(max) NOT NULL,
        [AccomplishByMiddleName] nvarchar(max) NOT NULL,
        [ReferenceNumber] nvarchar(max) NOT NULL,
        [ValidUntil] datetime2 NULL,
        [DisapprovalDate] datetime2 NULL,
        CONSTRAINT [PK_Accounts] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [AdminCredential] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(50) NOT NULL,
        [Password] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_AdminCredential] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [QrCodes] (
        [QrCodeId] uniqueidentifier NOT NULL,
        [EstablishmentName] nvarchar(max) NOT NULL,
        [Branch] nvarchar(max) NOT NULL,
        [TypeOfQRCode] nvarchar(max) NOT NULL,
        [QrCodeBase64] nvarchar(max) NOT NULL,
        [RegistrationUrl] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_QrCodes] PRIMARY KEY ([QrCodeId])
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [CommodityTransactions] (
        [TransactionId] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [RemainingDiscount] decimal(18,2) NOT NULL,
        [EstablishmentName] nvarchar(max) NOT NULL,
        [BranchName] nvarchar(max) NOT NULL,
        [Signature] nvarchar(max) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [ModifiedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_CommodityTransactions] PRIMARY KEY ([TransactionId]),
        CONSTRAINT [FK_CommodityTransactions_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [MedicineTransactionLedgers] (
        [LedgerId] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_MedicineTransactionLedgers] PRIMARY KEY ([LedgerId]),
        CONSTRAINT [FK_MedicineTransactionLedgers_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [Reports] (
        [ReportId] int NOT NULL IDENTITY,
        [AccountId] uniqueidentifier NOT NULL,
        [ProblemDescription] nvarchar(max) NOT NULL,
        [Establishment] nvarchar(max) NOT NULL,
        [Branch] nvarchar(max) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [Acknowledged] bit NOT NULL,
        CONSTRAINT [PK_Reports] PRIMARY KEY ([ReportId]),
        CONSTRAINT [FK_Reports_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [Status] (
        [Id] int NOT NULL IDENTITY,
        [AccountId] uniqueidentifier NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Requirement1] bit NOT NULL,
        [Requirement2] bit NOT NULL,
        [Requirement3] bit NOT NULL,
        [Requirement4] bit NOT NULL,
        [IsApproved] bit NOT NULL,
        CONSTRAINT [PK_Status] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Status_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [UserCredential] (
        [Id] int NOT NULL IDENTITY,
        [AccountId] uniqueidentifier NOT NULL,
        [Username] nvarchar(max) NULL,
        [Password] nvarchar(max) NULL,
        [Avatar] nvarchar(max) NULL,
        [Role] nvarchar(max) NULL,
        CONSTRAINT [PK_UserCredential] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_UserCredential_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [CommodityItems] (
        [CommodityItemId] uniqueidentifier NOT NULL,
        [TransactionId] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [Quantity] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [TotalPrice] decimal(18,2) NOT NULL,
        [DiscountedPrice] decimal(18,2) NOT NULL,
        [CreatedDate] datetime2 NOT NULL,
        [ModifiedDate] datetime2 NOT NULL,
        CONSTRAINT [PK_CommodityItems] PRIMARY KEY ([CommodityItemId]),
        CONSTRAINT [FK_CommodityItems_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CommodityItems_CommodityTransactions_TransactionId] FOREIGN KEY ([TransactionId]) REFERENCES [CommodityTransactions] ([TransactionId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE TABLE [MedicineTransactions] (
        [MedTransactionId] uniqueidentifier NOT NULL,
        [AccountId] uniqueidentifier NOT NULL,
        [MedicineName] nvarchar(max) NOT NULL,
        [MedicineBrand] nvarchar(max) NOT NULL,
        [PrescribedQuantity] int NOT NULL,
        [PurchasedQuantity] int NOT NULL,
        [RemainingBalance] int NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [TotalPrice] decimal(18,2) NOT NULL,
        [DiscountedPrice] decimal(18,2) NOT NULL,
        [DatePurchased] datetime2 NOT NULL,
        [AttendingPhysician] nvarchar(max) NOT NULL,
        [PTRNo] nvarchar(max) NOT NULL,
        [Signature] nvarchar(max) NOT NULL,
        [EstablishmentName] nvarchar(max) NOT NULL,
        [Branch] nvarchar(max) NOT NULL,
        [TransactionStatus] nvarchar(max) NOT NULL,
        [LastModified] datetime2 NOT NULL,
        [LedgerId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_MedicineTransactions] PRIMARY KEY ([MedTransactionId]),
        CONSTRAINT [FK_MedicineTransactions_Accounts_AccountId] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_MedicineTransactions_MedicineTransactionLedgers_LedgerId] FOREIGN KEY ([LedgerId]) REFERENCES [MedicineTransactionLedgers] ([LedgerId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE INDEX [IX_CommodityItems_AccountId] ON [CommodityItems] ([AccountId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE INDEX [IX_CommodityItems_TransactionId] ON [CommodityItems] ([TransactionId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE INDEX [IX_CommodityTransactions_AccountId] ON [CommodityTransactions] ([AccountId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE INDEX [IX_MedicineTransactionLedgers_AccountId] ON [MedicineTransactionLedgers] ([AccountId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE INDEX [IX_MedicineTransactions_AccountId] ON [MedicineTransactions] ([AccountId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE INDEX [IX_MedicineTransactions_LedgerId] ON [MedicineTransactions] ([LedgerId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE INDEX [IX_Reports_AccountId] ON [Reports] ([AccountId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE UNIQUE INDEX [IX_Status_AccountId] ON [Status] ([AccountId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    CREATE UNIQUE INDEX [IX_UserCredential_AccountId] ON [UserCredential] ([AccountId]);
END;
GO

IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20250617142122_arnold')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20250617142122_arnold', N'6.0.29');
END;
GO

COMMIT;
GO

