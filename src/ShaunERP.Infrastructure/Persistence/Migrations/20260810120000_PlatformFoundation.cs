using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShaunERP.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class PlatformFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""ALTER TABLE "CompanyModules" ADD COLUMN IF NOT EXISTS "ModuleKey" text NOT NULL DEFAULT '';""");
        migrationBuilder.Sql("""ALTER TABLE "CompanyModules" ADD COLUMN IF NOT EXISTS "IsEnabled" boolean NOT NULL DEFAULT TRUE;""");

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "UserCompanyPreferences" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                "Status" text NULL,
                "UserId" integer NOT NULL,
                "CompanyId" integer NOT NULL,
                "LastModuleKey" text NULL,
                "FavouriteModules" text NULL,
                "RecentModules" text NULL,
                "SidebarCollapsed" boolean NOT NULL DEFAULT FALSE
            );
            """);

        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserCompanyPreferences_UserId_CompanyId"
            ON "UserCompanyPreferences" ("UserId", "CompanyId");
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "AuditLogs" (
                "Id" SERIAL PRIMARY KEY,
                "CreatedDate" timestamp with time zone NOT NULL,
                "UpdatedDate" timestamp with time zone NULL,
                "CreatedBy" text NULL,
                "ModifiedBy" text NULL,
                "Status" text NULL,
                "CompanyId" integer NULL,
                "UserId" integer NULL,
                "Action" text NOT NULL,
                "EntityType" text NULL,
                "EntityId" text NULL,
                "BeforeValues" text NULL,
                "AfterValues" text NULL,
                "CorrelationId" text NULL,
                "IpAddress" text NULL,
                "UserAgent" text NULL,
                "OccurredAt" timestamp with time zone NOT NULL
            );
            """);

        migrationBuilder.Sql("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_Permissions_Name" ON "Permissions" ("Name");""");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "AuditLogs";""");
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "UserCompanyPreferences";""");
        migrationBuilder.Sql("""ALTER TABLE "CompanyModules" DROP COLUMN IF EXISTS "IsEnabled";""");
        migrationBuilder.Sql("""ALTER TABLE "CompanyModules" DROP COLUMN IF EXISTS "ModuleKey";""");
    }
}
