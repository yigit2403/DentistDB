using Microsoft.EntityFrameworkCore;

namespace DentistDB.Data;

public static class DatabaseSchemaInitializer
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        var provider = db.Database.ProviderName ?? string.Empty;

        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
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
        }
        else if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
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
        }
    }
}
