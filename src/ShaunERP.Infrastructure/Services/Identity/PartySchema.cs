using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services.Identity;

/// <summary>
/// Additive Party identity schema. Mirrors ProjectsSchema / AccountingSchema pattern.
/// </summary>
internal static class PartySchema
{
    public static async Task EnsureAsync(ShaunErpDbContext db, ILogger logger, CancellationToken ct)
    {
        foreach (var sql in TableSql.Concat(AlterSql).Concat(IndexSql))
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Party schema statement skipped");
            }
        }
    }

    private static readonly string[] TableSql =
    [
        """
        CREATE TABLE IF NOT EXISTS "Parties" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "PartyType" text NOT NULL DEFAULT 'Person',
            "DisplayName" text NOT NULL DEFAULT '',
            "CrmAccountId" integer NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "PartyPersons" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "PartyId" integer NOT NULL,
            "FirstName" text NULL,
            "LastName" text NULL,
            "Title" text NULL,
            "Gender" text NULL,
            "Language" text NULL,
            CONSTRAINT "FK_PartyPersons_Parties_PartyId" FOREIGN KEY ("PartyId") REFERENCES "Parties" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "PartyPhones" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "PartyId" integer NOT NULL,
            "OriginalPhone" text NULL,
            "NormalizedPhone" text NULL,
            "E164Phone" text NULL,
            "PhoneType" text NOT NULL DEFAULT 'Mobile',
            "IsPrimary" boolean NOT NULL DEFAULT FALSE,
            "IsActive" boolean NOT NULL DEFAULT TRUE,
            CONSTRAINT "FK_PartyPhones_Parties_PartyId" FOREIGN KEY ("PartyId") REFERENCES "Parties" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "PartyEmails" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "PartyId" integer NOT NULL,
            "OriginalEmail" text NULL,
            "NormalizedEmail" text NULL,
            "EmailType" text NOT NULL DEFAULT 'Work',
            "IsPrimary" boolean NOT NULL DEFAULT FALSE,
            "IsActive" boolean NOT NULL DEFAULT TRUE,
            CONSTRAINT "FK_PartyEmails_Parties_PartyId" FOREIGN KEY ("PartyId") REFERENCES "Parties" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ImportBatches" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "SourceName" text NOT NULL DEFAULT '',
            "FileName" text NULL,
            "StartedAt" timestamp with time zone NOT NULL,
            "CompletedAt" timestamp with time zone NULL,
            "ImportedBy" text NULL,
            "BatchStatus" text NOT NULL DEFAULT 'Running',
            "TotalRows" integer NOT NULL DEFAULT 0,
            "MatchedRows" integer NOT NULL DEFAULT 0,
            "CreatedRows" integer NOT NULL DEFAULT 0,
            "ReviewRows" integer NOT NULL DEFAULT 0,
            "FailedRows" integer NOT NULL DEFAULT 0,
            "Notes" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ImportSourceRows" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "ImportBatchId" integer NOT NULL,
            "SourceRowNumber" integer NOT NULL DEFAULT 0,
            "ExternalId" text NULL,
            "RawDataJson" text NULL,
            "MatchedPartyId" integer NULL,
            "MatchStatus" text NOT NULL DEFAULT 'NoMatch',
            "MatchReason" text NULL,
            "ProcessedAt" timestamp with time zone NULL,
            CONSTRAINT "FK_ImportSourceRows_ImportBatches_ImportBatchId" FOREIGN KEY ("ImportBatchId") REFERENCES "ImportBatches" ("Id") ON DELETE CASCADE
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ImportMatchReviews" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "ImportBatchId" integer NULL,
            "ImportSourceRowId" integer NULL,
            "CandidatePartyId" integer NULL,
            "ReviewStatus" text NOT NULL DEFAULT 'Pending',
            "MatchReason" text NULL,
            "IncomingName" text NULL,
            "IncomingPhone" text NULL,
            "IncomingEmail" text NULL,
            "CandidateName" text NULL,
            "Resolution" text NULL,
            "ResolvedBy" text NULL,
            "ResolvedAt" timestamp with time zone NULL
        );
        """
    ];

    private static readonly string[] AlterSql =
    [
        """ALTER TABLE "Leads" ADD COLUMN IF NOT EXISTS "PartyId" integer NULL;""",
        """ALTER TABLE "Prospects" ADD COLUMN IF NOT EXISTS "PartyId" integer NULL;""",
        """ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "PartyId" integer NULL;""",
        """ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "PartyId" integer NULL;""",
        """ALTER TABLE "Opportunities" ADD COLUMN IF NOT EXISTS "PartyId" integer NULL;"""
    ];

    private static readonly string[] IndexSql =
    [
        """CREATE INDEX IF NOT EXISTS "IX_Parties_CompanyId_PartyType" ON "Parties" ("CompanyId", "PartyType");""",
        """CREATE INDEX IF NOT EXISTS "IX_Parties_CompanyId_DisplayName" ON "Parties" ("CompanyId", "DisplayName");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_PartyPersons_PartyId" ON "PartyPersons" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_PartyPhones_CompanyLookup" ON "PartyPhones" ("NormalizedPhone");""",
        """CREATE INDEX IF NOT EXISTS "IX_PartyPhones_PartyId" ON "PartyPhones" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_PartyPhones_E164" ON "PartyPhones" ("E164Phone");""",
        """CREATE INDEX IF NOT EXISTS "IX_PartyEmails_Normalized" ON "PartyEmails" ("NormalizedEmail");""",
        """CREATE INDEX IF NOT EXISTS "IX_PartyEmails_PartyId" ON "PartyEmails" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_Leads_PartyId" ON "Leads" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_Prospects_PartyId" ON "Prospects" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_Contacts_PartyId" ON "Contacts" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_Customers_PartyId" ON "Customers" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_Opportunities_PartyId" ON "Opportunities" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ImportBatches_CompanyId" ON "ImportBatches" ("CompanyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ImportSourceRows_Batch" ON "ImportSourceRows" ("ImportBatchId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ImportMatchReviews_Company_Status" ON "ImportMatchReviews" ("CompanyId", "ReviewStatus");""",
        // Non-unique: shared phones/emails are legitimate; matching service + review queue handle collisions.
        """CREATE INDEX IF NOT EXISTS "IX_PartyPhones_Party_Normalized" ON "PartyPhones" ("PartyId", "NormalizedPhone");""",
        """CREATE INDEX IF NOT EXISTS "IX_PartyEmails_Party_Normalized" ON "PartyEmails" ("PartyId", "NormalizedEmail");"""
    ];
}
