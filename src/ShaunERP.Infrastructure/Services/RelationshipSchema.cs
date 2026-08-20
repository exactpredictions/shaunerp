using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShaunERP.Infrastructure.Persistence;

namespace ShaunERP.Infrastructure.Services;

/// <summary>Additive schema for Courses, Subscriptions, Consultations, Events — PartyId linked.</summary>
internal static class RelationshipSchema
{
    public static async Task EnsureAsync(ShaunErpDbContext db, ILogger logger, CancellationToken ct)
    {
        foreach (var sql in TableSql.Concat(IndexSql).Concat(SeedSql))
        {
            try
            {
                await db.Database.ExecuteSqlRawAsync(sql, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Relationship schema statement skipped");
            }
        }
    }

    private static readonly string[] TableSql =
    [
        """
        CREATE TABLE IF NOT EXISTS "TrainingPrograms" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Name" text NOT NULL,
            "Description" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "CourseEnrollments" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "PartyId" integer NOT NULL,
            "ProgramId" integer NOT NULL,
            "EnrollmentStatus" text NOT NULL DEFAULT 'Active',
            "StartDate" timestamp with time zone NULL,
            "EndDate" timestamp with time zone NULL,
            "Source" text NULL,
            "SourceReference" text NULL,
            "ImportBatchId" integer NULL,
            "Notes" text NULL,
            "SalesOrderId" integer NULL,
            "InvoiceId" integer NULL,
            CONSTRAINT "FK_CourseEnrollments_Parties_PartyId" FOREIGN KEY ("PartyId") REFERENCES "Parties" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_CourseEnrollments_TrainingPrograms_ProgramId" FOREIGN KEY ("ProgramId") REFERENCES "TrainingPrograms" ("Id") ON DELETE RESTRICT
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "SubscriptionPlans" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Name" text NOT NULL,
            "DurationMonths" integer NOT NULL,
            "Description" text NULL,
            "ProductSku" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "SubscriptionMemberships" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "PartyId" integer NOT NULL,
            "PlanId" integer NOT NULL,
            "StartDate" timestamp with time zone NULL,
            "EndDate" timestamp with time zone NULL,
            "MembershipStatus" text NOT NULL DEFAULT 'Active',
            "Source" text NULL,
            "ExternalReference" text NULL,
            "ImportBatchId" integer NULL,
            "Notes" text NULL,
            "SalesOrderId" integer NULL,
            "InvoiceId" integer NULL,
            CONSTRAINT "FK_SubscriptionMemberships_Parties_PartyId" FOREIGN KEY ("PartyId") REFERENCES "Parties" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_SubscriptionMemberships_SubscriptionPlans_PlanId" FOREIGN KEY ("PlanId") REFERENCES "SubscriptionPlans" ("Id") ON DELETE RESTRICT
        );
        """,
        """ALTER TABLE "SubscriptionMemberships" ADD COLUMN IF NOT EXISTS "LegacyDate" timestamp with time zone NULL;""",
        """ALTER TABLE "SubscriptionMemberships" ADD COLUMN IF NOT EXISTS "LegacyDateSource" text NULL;""",
        """
        CREATE TABLE IF NOT EXISTS "ConsultationServices" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Name" text NOT NULL,
            "Description" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ConsultationEngagements" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "PartyId" integer NOT NULL,
            "ConsultationServiceId" integer NULL,
            "EnquiryDate" timestamp with time zone NULL,
            "ScheduledDate" timestamp with time zone NULL,
            "ConsultationDate" timestamp with time zone NULL,
            "SourceCreatedAt" timestamp with time zone NULL,
            "EngagementStatus" text NOT NULL DEFAULT 'Unclassified',
            "ConsultantUserId" integer NULL,
            "Source" text NULL,
            "ExternalReference" text NULL,
            "ImportBatchId" integer NULL,
            "Notes" text NULL,
            "SourceEvidence" text NULL,
            "InvoiceId" integer NULL,
            CONSTRAINT "FK_ConsultationEngagements_Parties_PartyId" FOREIGN KEY ("PartyId") REFERENCES "Parties" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_ConsultationEngagements_ConsultationServices_ConsultationServiceId" FOREIGN KEY ("ConsultationServiceId") REFERENCES "ConsultationServices" ("Id") ON DELETE RESTRICT
        );
        """,
        """ALTER TABLE "ConsultationEngagements" ALTER COLUMN "ConsultationServiceId" DROP NOT NULL;""",
        """ALTER TABLE "ConsultationEngagements" ADD COLUMN IF NOT EXISTS "EnquiryDate" timestamp with time zone NULL;""",
        """ALTER TABLE "ConsultationEngagements" ADD COLUMN IF NOT EXISTS "ScheduledDate" timestamp with time zone NULL;""",
        """ALTER TABLE "ConsultationEngagements" ADD COLUMN IF NOT EXISTS "SourceCreatedAt" timestamp with time zone NULL;""",
        """ALTER TABLE "ConsultationEngagements" ADD COLUMN IF NOT EXISTS "SourceEvidence" text NULL;""",
        """ALTER TABLE "ConsultationEngagements" ALTER COLUMN "EngagementStatus" SET DEFAULT 'Unclassified';""",
        """
        CREATE TABLE IF NOT EXISTS "Events" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Name" text NOT NULL,
            "StartDate" timestamp with time zone NULL,
            "EndDate" timestamp with time zone NULL,
            "Location" text NULL,
            "Description" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "EventParticipations" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "PartyId" integer NOT NULL,
            "EventId" integer NOT NULL,
            "RegistrationStatus" text NOT NULL DEFAULT 'Registered',
            "AttendanceStatus" text NULL,
            "Source" text NULL,
            "ExternalReference" text NULL,
            "ImportBatchId" integer NULL,
            "Notes" text NULL,
            "InvoiceId" integer NULL,
            CONSTRAINT "FK_EventParticipations_Parties_PartyId" FOREIGN KEY ("PartyId") REFERENCES "Parties" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_EventParticipations_Events_EventId" FOREIGN KEY ("EventId") REFERENCES "Events" ("Id") ON DELETE RESTRICT
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ReportProducts" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "Name" text NOT NULL,
            "ReportType" text NOT NULL,
            "Description" text NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS "ReportPurchases" (
            "Id" SERIAL PRIMARY KEY,
            "CreatedDate" timestamp with time zone NOT NULL,
            "UpdatedDate" timestamp with time zone NULL,
            "CreatedBy" text NULL,
            "ModifiedBy" text NULL,
            "Status" text NULL,
            "CompanyId" integer NOT NULL,
            "PartyId" integer NOT NULL,
            "ReportProductId" integer NOT NULL,
            "SourceSystem" text NULL,
            "SourceFile" text NULL,
            "SourceId" text NULL,
            "ExternalReference" text NULL,
            "AmountPaid" numeric NULL,
            "Currency" text NULL,
            "PaymentStatus" text NOT NULL DEFAULT 'Completed',
            "ReportStatus" text NULL,
            "GeneratedAt" timestamp with time zone NULL,
            "DeliveredAt" timestamp with time zone NULL,
            "PaymentDate" timestamp with time zone NULL,
            "DeliveryEmailStatus" text NULL,
            "DeliveryWhatsappStatus" text NULL,
            "RazorpayOrderId" text NULL,
            "RazorpayPaymentId" text NULL,
            "RazorpayStatus" text NULL,
            "SubjectName" text NULL,
            "SubjectGender" text NULL,
            "BirthDay" integer NULL,
            "BirthMonth" integer NULL,
            "BirthYear" integer NULL,
            "BirthHours" integer NULL,
            "BirthMinutes" integer NULL,
            "BirthSeconds" integer NULL,
            "Place" text NULL,
            "ImportBatchId" integer NULL,
            "Notes" text NULL,
            "SalesOrderId" integer NULL,
            "InvoiceId" integer NULL,
            CONSTRAINT "FK_ReportPurchases_Parties_PartyId" FOREIGN KEY ("PartyId") REFERENCES "Parties" ("Id") ON DELETE RESTRICT,
            CONSTRAINT "FK_ReportPurchases_ReportProducts_ReportProductId" FOREIGN KEY ("ReportProductId") REFERENCES "ReportProducts" ("Id") ON DELETE RESTRICT
        );
        """
    ];

    private static readonly string[] IndexSql =
    [
        """CREATE INDEX IF NOT EXISTS "IX_TrainingPrograms_Company_Name" ON "TrainingPrograms" ("CompanyId", "Name");""",
        """CREATE INDEX IF NOT EXISTS "IX_CourseEnrollments_PartyId" ON "CourseEnrollments" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_CourseEnrollments_ProgramId" ON "CourseEnrollments" ("ProgramId");""",
        """CREATE INDEX IF NOT EXISTS "IX_CourseEnrollments_Company_Status" ON "CourseEnrollments" ("CompanyId", "EnrollmentStatus");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_CourseEnrollments_Unique" ON "CourseEnrollments" ("CompanyId", "PartyId", "ProgramId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubscriptionPlans_Company_Name" ON "SubscriptionPlans" ("CompanyId", "Name");""",
        """CREATE INDEX IF NOT EXISTS "IX_SubscriptionMemberships_PartyId" ON "SubscriptionMemberships" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_SubscriptionMemberships_PlanId" ON "SubscriptionMemberships" ("PlanId");""",
        """CREATE INDEX IF NOT EXISTS "IX_SubscriptionMemberships_Company_Status" ON "SubscriptionMemberships" ("CompanyId", "MembershipStatus");""",
        """CREATE INDEX IF NOT EXISTS "IX_SubscriptionMemberships_EndDate" ON "SubscriptionMemberships" ("EndDate");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubscriptionMemberships_Company_ExternalReference" ON "SubscriptionMemberships" ("CompanyId", "ExternalReference") WHERE "ExternalReference" IS NOT NULL;""",
        """CREATE INDEX IF NOT EXISTS "IX_ConsultationServices_Company_Name" ON "ConsultationServices" ("CompanyId", "Name");""",
        """CREATE INDEX IF NOT EXISTS "IX_ConsultationEngagements_PartyId" ON "ConsultationEngagements" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ConsultationEngagements_ServiceId" ON "ConsultationEngagements" ("ConsultationServiceId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ConsultationEngagements_Company_Status" ON "ConsultationEngagements" ("CompanyId", "EngagementStatus");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConsultationEngagements_Company_ExternalReference" ON "ConsultationEngagements" ("CompanyId", "ExternalReference") WHERE "ExternalReference" IS NOT NULL;""",
        """CREATE INDEX IF NOT EXISTS "IX_Events_Company_Name" ON "Events" ("CompanyId", "Name");""",
        """CREATE INDEX IF NOT EXISTS "IX_EventParticipations_PartyId" ON "EventParticipations" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_EventParticipations_EventId" ON "EventParticipations" ("EventId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_EventParticipations_Unique" ON "EventParticipations" ("CompanyId", "PartyId", "EventId");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReportProducts_Company_Name" ON "ReportProducts" ("CompanyId", "Name");""",
        """CREATE INDEX IF NOT EXISTS "IX_ReportPurchases_PartyId" ON "ReportPurchases" ("PartyId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ReportPurchases_ReportProductId" ON "ReportPurchases" ("ReportProductId");""",
        """CREATE INDEX IF NOT EXISTS "IX_ReportPurchases_Company_PaymentDate" ON "ReportPurchases" ("CompanyId", "PaymentDate");""",
        """CREATE INDEX IF NOT EXISTS "IX_ReportPurchases_Company_ReportStatus" ON "ReportPurchases" ("CompanyId", "ReportStatus");""",
        """CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReportPurchases_Company_ExternalReference" ON "ReportPurchases" ("CompanyId", "ExternalReference") WHERE "ExternalReference" IS NOT NULL;"""
    ];

    /// <summary>Seed default subscription plans per company that has none yet — done in C# seeder, not SQL.</summary>
    private static readonly string[] SeedSql = [];
}
