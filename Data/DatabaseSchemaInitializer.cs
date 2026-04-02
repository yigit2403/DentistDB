using Microsoft.EntityFrameworkCore;
using System.Data.Common;

namespace DentistDB.Data;

public static class DatabaseSchemaInitializer
{
    public static async Task EnsureAsync(ApplicationDbContext db)
    {
        await EnsureColumnAsync(db, "Patients", "Tckn", """ALTER TABLE "Patients" ADD COLUMN "Tckn" TEXT NOT NULL DEFAULT '';""");
        await EnsureColumnAsync(db, "Patients", "PhotoBase64", """ALTER TABLE "Patients" ADD COLUMN "PhotoBase64" TEXT NULL;""");
        await EnsureColumnAsync(db, "Patients", "PhotoContentType", """ALTER TABLE "Patients" ADD COLUMN "PhotoContentType" TEXT NULL;""");

        await EnsureColumnAsync(db, "Appointments", "SelectedTeethData", """ALTER TABLE "Appointments" ADD COLUMN "SelectedTeethData" TEXT NULL;""");
        await EnsureColumnAsync(db, "Appointments", "InvoiceId", """ALTER TABLE "Appointments" ADD COLUMN "InvoiceId" INTEGER NULL;""");

        await EnsureColumnAsync(db, "Payments", "IsPlanned", """ALTER TABLE "Payments" ADD COLUMN "IsPlanned" INTEGER NOT NULL DEFAULT 0;""");
        await EnsureColumnAsync(db, "Payments", "IsSettled", """ALTER TABLE "Payments" ADD COLUMN "IsSettled" INTEGER NOT NULL DEFAULT 0;""");
        await EnsureColumnAsync(db, "Payments", "SettledDate", """ALTER TABLE "Payments" ADD COLUMN "SettledDate" TEXT NULL;""");
        await EnsureColumnAsync(db, "Payments", "InstallmentNumber", """ALTER TABLE "Payments" ADD COLUMN "InstallmentNumber" INTEGER NULL;""");

        await EnsureColumnAsync(db, "PreviousOperations", "PriceAmount", """ALTER TABLE "PreviousOperations" ADD COLUMN "PriceAmount" TEXT NOT NULL DEFAULT '0';""");
        await EnsureColumnAsync(db, "PreviousOperations", "InvoiceId", """ALTER TABLE "PreviousOperations" ADD COLUMN "InvoiceId" INTEGER NULL;""");

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "PreviousOperations" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PreviousOperations" PRIMARY KEY AUTOINCREMENT,
                "PatientId" INTEGER NOT NULL,
                "Date" TEXT NOT NULL,
                "PriceAmount" TEXT NOT NULL,
                "InvoiceId" INTEGER NULL,
                "Title" TEXT NOT NULL,
                "Diagnosis" TEXT NULL,
                "Procedures" TEXT NULL,
                "Prescriptions" TEXT NULL,
                "Notes" TEXT NULL,
                "SelectedTeethData" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_PreviousOperations_Patients_PatientId" FOREIGN KEY ("PatientId") REFERENCES "Patients" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_PreviousOperations_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE SET NULL
            );
            """);

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_PreviousOperations_PatientId_Date"
            ON "PreviousOperations" ("PatientId", "Date");
            """);

        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_PreviousOperations_InvoiceId" ON "PreviousOperations" ("InvoiceId");""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Appointments_InvoiceId" ON "Appointments" ("InvoiceId");""");
        await db.Database.ExecuteSqlRawAsync("""CREATE INDEX IF NOT EXISTS "IX_Payments_InvoiceId_PaymentDate_IsPlanned" ON "Payments" ("InvoiceId", "PaymentDate", "IsPlanned");""");
    }

    private static async Task EnsureColumnAsync(ApplicationDbContext db, string tableName, string columnName, string alterSql)
    {
        if (await ColumnExistsAsync(db, tableName, columnName))
        {
            return;
        }

        await db.Database.ExecuteSqlRawAsync(alterSql);
    }

    private static async Task<bool> ColumnExistsAsync(ApplicationDbContext db, string tableName, string columnName)
    {
        var connection = db.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != System.Data.ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT COUNT(*)
                FROM pragma_table_info(@tableName)
                WHERE name = @columnName;
                """;

            AddParameter(command, "@tableName", tableName);
            AddParameter(command, "@columnName", columnName);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
