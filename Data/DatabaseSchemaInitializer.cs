using Microsoft.EntityFrameworkCore;

namespace DentistDB.Data;

public static class DatabaseSchemaInitializer
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        var provider = db.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureColumnAsync(db, "Patients", "Tckn", """ALTER TABLE "Patients" ADD COLUMN "Tckn" TEXT NOT NULL DEFAULT '';""");
            await EnsureColumnAsync(db, "Patients", "PhotoBase64", """ALTER TABLE "Patients" ADD COLUMN "PhotoBase64" TEXT NULL;""");
            await EnsureColumnAsync(db, "Patients", "PhotoContentType", """ALTER TABLE "Patients" ADD COLUMN "PhotoContentType" TEXT NULL;""");
            await EnsureColumnAsync(db, "Appointments", "SelectedTeethData", """ALTER TABLE "Appointments" ADD COLUMN "SelectedTeethData" TEXT NULL;""");
            await EnsureColumnAsync(db, "Payments", "IsPlanned", """ALTER TABLE "Payments" ADD COLUMN "IsPlanned" INTEGER NOT NULL DEFAULT 0;""");
            await EnsureColumnAsync(db, "Payments", "IsSettled", """ALTER TABLE "Payments" ADD COLUMN "IsSettled" INTEGER NOT NULL DEFAULT 0;""");
            await EnsureColumnAsync(db, "Payments", "SettledDate", """ALTER TABLE "Payments" ADD COLUMN "SettledDate" TEXT NULL;""");
            await EnsureColumnAsync(db, "Payments", "InstallmentNumber", """ALTER TABLE "Payments" ADD COLUMN "InstallmentNumber" INTEGER NULL;""");

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "PreviousOperations" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_PreviousOperations" PRIMARY KEY AUTOINCREMENT,
                    "PatientId" INTEGER NOT NULL,
                    "Date" TEXT NOT NULL,
                    "Title" TEXT NOT NULL,
                    "Diagnosis" TEXT NULL,
                    "Procedures" TEXT NULL,
                    "Prescriptions" TEXT NULL,
                    "Notes" TEXT NULL,
                    "SelectedTeethData" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_PreviousOperations_Patients_PatientId" FOREIGN KEY ("PatientId") REFERENCES "Patients" ("Id") ON DELETE CASCADE
                );
                """);

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE INDEX IF NOT EXISTS "IX_PreviousOperations_PatientId_Date"
                ON "PreviousOperations" ("PatientId", "Date");
                """);

            await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Payments_InvoiceId_PaymentDate_IsPlanned" ON "Payments" ("InvoiceId", "PaymentDate", "IsPlanned");""");
        }
        else if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureColumnAsync(db, "Patients", "Tckn", """ALTER TABLE `Patients` ADD COLUMN `Tckn` varchar(11) NOT NULL DEFAULT '';""");
            await EnsureColumnAsync(db, "Patients", "PhotoBase64", """ALTER TABLE `Patients` ADD COLUMN `PhotoBase64` longtext NULL;""");
            await EnsureColumnAsync(db, "Patients", "PhotoContentType", """ALTER TABLE `Patients` ADD COLUMN `PhotoContentType` varchar(100) NULL;""");
            await EnsureColumnAsync(db, "Appointments", "SelectedTeethData", """ALTER TABLE `Appointments` ADD COLUMN `SelectedTeethData` varchar(200) NULL;""");
            await EnsureColumnAsync(db, "Payments", "IsPlanned", """ALTER TABLE `Payments` ADD COLUMN `IsPlanned` tinyint(1) NOT NULL DEFAULT 0;""");
            await EnsureColumnAsync(db, "Payments", "IsSettled", """ALTER TABLE `Payments` ADD COLUMN `IsSettled` tinyint(1) NOT NULL DEFAULT 0;""");
            await EnsureColumnAsync(db, "Payments", "SettledDate", """ALTER TABLE `Payments` ADD COLUMN `SettledDate` date NULL;""");
            await EnsureColumnAsync(db, "Payments", "InstallmentNumber", """ALTER TABLE `Payments` ADD COLUMN `InstallmentNumber` int NULL;""");

            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS `PreviousOperations` (
                    `Id` int NOT NULL AUTO_INCREMENT,
                    `PatientId` int NOT NULL,
                    `Date` date NOT NULL,
                    `Title` varchar(200) NOT NULL,
                    `Diagnosis` varchar(500) NULL,
                    `Procedures` varchar(500) NULL,
                    `Prescriptions` varchar(500) NULL,
                    `Notes` varchar(1000) NULL,
                    `SelectedTeethData` varchar(200) NULL,
                    `CreatedAt` datetime(6) NOT NULL,
                    `UpdatedAt` datetime(6) NOT NULL,
                    PRIMARY KEY (`Id`),
                    KEY `IX_PreviousOperations_PatientId_Date` (`PatientId`, `Date`),
                    CONSTRAINT `FK_PreviousOperations_Patients_PatientId` FOREIGN KEY (`PatientId`) REFERENCES `Patients` (`Id`) ON DELETE CASCADE
                );
                """);

            await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS `IX_Payments_InvoiceId_PaymentDate_IsPlanned` ON `Payments` (`InvoiceId`, `PaymentDate`, `IsPlanned`);""");
        }
    }

    private static async Task EnsureColumnAsync(ApplicationDbContext db, string tableName, string columnName, string alterSql)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(alterSql);
        }
        catch
        {
            // Column already exists on upgraded databases.
        }
    }
}
