using Microsoft.EntityFrameworkCore;

namespace BurhanNexus.HMS.Infrastructure.Persistence;

/// <summary>
/// Database-level protections applied (idempotently) at every start-up and after a restore:
/// audit log rows can be inserted but never updated, deleted or truncated – not even by a
/// faulty program or a direct SQL connection using the application account.
/// </summary>
public static class DatabaseHardening
{
    private const string Sql = """
        CREATE OR REPLACE FUNCTION bnhms_audit_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Audit log records cannot be modified or deleted (Burhan Nexus HMS).';
        END;
        $$;

        DROP TRIGGER IF EXISTS trg_auditlogs_immutable ON "AuditLogs";
        CREATE TRIGGER trg_auditlogs_immutable BEFORE UPDATE OR DELETE ON "AuditLogs"
            FOR EACH ROW EXECUTE FUNCTION bnhms_audit_immutable();

        DROP TRIGGER IF EXISTS trg_auditlogs_no_truncate ON "AuditLogs";
        CREATE TRIGGER trg_auditlogs_no_truncate BEFORE TRUNCATE ON "AuditLogs"
            FOR EACH STATEMENT EXECUTE FUNCTION bnhms_audit_immutable();
        """;

    public static Task ApplyAsync(HmsDbContext db, CancellationToken ct = default) => db.Database.ExecuteSqlRawAsync(Sql, ct);
}
