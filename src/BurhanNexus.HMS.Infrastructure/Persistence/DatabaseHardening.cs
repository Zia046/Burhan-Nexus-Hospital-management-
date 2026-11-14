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

        -- Phase 3: financial history can only grow. Ledgers, settlements, adjustments, closings and print history
        -- are insert-only; receipts, charges, refunds and deposits can change status but are never deleted,
        -- and the amounts/numbers of an issued receipt or charge can never be changed.
        CREATE OR REPLACE FUNCTION bnhms_finance_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Financial records cannot be deleted or modified (%). Use a cancellation, refund or adjustment.', TG_TABLE_NAME;
        END;
        $$;

        CREATE OR REPLACE FUNCTION bnhms_receipt_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."ReceiptNumber" <> OLD."ReceiptNumber" OR NEW."AmountPaid" <> OLD."AmountPaid" OR NEW."TotalNet" <> OLD."TotalNet"
               OR NEW."TotalFee" <> OLD."TotalFee" OR NEW."TotalDiscount" <> OLD."TotalDiscount"
               OR NEW."CashierUserId" <> OLD."CashierUserId" OR NEW."CounterId" <> OLD."CounterId" OR NEW."PaymentMethodId" <> OLD."PaymentMethodId"
               OR NEW."PatientId" IS DISTINCT FROM OLD."PatientId" OR NEW."BusinessDate" <> OLD."BusinessDate" OR NEW."CreatedUtc" <> OLD."CreatedUtc"
               OR NEW."IdempotencyKey" <> OLD."IdempotencyKey" OR (OLD."Status" = 'Cancelled' AND NEW."Status" <> 'Cancelled') THEN
                RAISE EXCEPTION 'Issued receipt % cannot be altered (only cancelled or refunded).', OLD."ReceiptNumber";
            END IF;
            RETURN NEW;
        END;
        $$;

        CREATE OR REPLACE FUNCTION bnhms_charge_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."ChargeNumber" <> OLD."ChargeNumber" OR NEW."Fee" <> OLD."Fee" OR NEW."Discount" <> OLD."Discount" OR NEW."NetAmount" <> OLD."NetAmount"
               OR NEW."ServiceItemId" <> OLD."ServiceItemId" OR NEW."DoctorShareAmount" <> OLD."DoctorShareAmount"
               OR NEW."PatientId" IS DISTINCT FROM OLD."PatientId" THEN
                RAISE EXCEPTION 'Charge % amounts cannot be altered.', OLD."ChargeNumber";
            END IF;
            RETURN NEW;
        END;
        $$;

        -- Phase 4: a verified / released laboratory result can only change after a withdrawal (which first stores a revision
        -- and moves the test back to "In progress"). Direct edits of verified values are refused by the database.
        CREATE OR REPLACE FUNCTION bnhms_lab_value_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE st text;
        BEGIN
            SELECT "Status" INTO st FROM "LabOrderItems" WHERE "Id" = NEW."OrderItemId";
            IF st IN ('Verified','Released') AND (NEW."Value" IS DISTINCT FROM OLD."Value" OR NEW."NumericValue" IS DISTINCT FROM OLD."NumericValue"
               OR NEW."RefLow" IS DISTINCT FROM OLD."RefLow" OR NEW."RefHigh" IS DISTINCT FROM OLD."RefHigh" OR NEW."RefText" IS DISTINCT FROM OLD."RefText"
               OR NEW."Unit" IS DISTINCT FROM OLD."Unit" OR NEW."Name" IS DISTINCT FROM OLD."Name") THEN
                RAISE EXCEPTION 'Verified laboratory result cannot be changed directly. Withdraw it with a reason first.';
            END IF;
            RETURN NEW;
        END;
        $$;

        DO $$
        DECLARE t text;
        BEGIN
            FOREACH t IN ARRAY ARRAY['CashierLedger','DoctorLedger','DoctorSettlements','FinancialAdjustments','DailyCashClosings','ReceiptPrints','ServiceFees',
                                       'LabItemEvents','LabResultRevisions','LabReportPrints'] LOOP
                IF to_regclass(format('%I', t)) IS NOT NULL THEN
                    EXECUTE format('DROP TRIGGER IF EXISTS trg_%s_immutable ON %I', lower(t), t);
                    EXECUTE format('CREATE TRIGGER trg_%s_immutable BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION bnhms_finance_immutable()', lower(t), t);
                    EXECUTE format('DROP TRIGGER IF EXISTS trg_%s_no_truncate ON %I', lower(t), t);
                    EXECUTE format('CREATE TRIGGER trg_%s_no_truncate BEFORE TRUNCATE ON %I FOR EACH STATEMENT EXECUTE FUNCTION bnhms_finance_immutable()', lower(t), t);
                END IF;
            END LOOP;
            FOREACH t IN ARRAY ARRAY['Receipts','ReceiptLines','Charges','Refunds','CashDeposits','LabOrders','LabOrderItems','LabResultValues','LabSamples'] LOOP
                IF to_regclass(format('%I', t)) IS NOT NULL THEN
                    EXECUTE format('DROP TRIGGER IF EXISTS trg_%s_no_delete ON %I', lower(t), t);
                    EXECUTE format('CREATE TRIGGER trg_%s_no_delete BEFORE DELETE ON %I FOR EACH ROW EXECUTE FUNCTION bnhms_finance_immutable()', lower(t), t);
                    EXECUTE format('DROP TRIGGER IF EXISTS trg_%s_no_truncate ON %I', lower(t), t);
                    EXECUTE format('CREATE TRIGGER trg_%s_no_truncate BEFORE TRUNCATE ON %I FOR EACH STATEMENT EXECUTE FUNCTION bnhms_finance_immutable()', lower(t), t);
                END IF;
            END LOOP;
            IF to_regclass('"Receipts"') IS NOT NULL THEN
                DROP TRIGGER IF EXISTS trg_receipts_guard ON "Receipts";
                CREATE TRIGGER trg_receipts_guard BEFORE UPDATE ON "Receipts" FOR EACH ROW EXECUTE FUNCTION bnhms_receipt_guard();
            END IF;
            IF to_regclass('"LabResultValues"') IS NOT NULL THEN
                DROP TRIGGER IF EXISTS trg_labresultvalues_guard ON "LabResultValues";
                CREATE TRIGGER trg_labresultvalues_guard BEFORE UPDATE ON "LabResultValues" FOR EACH ROW EXECUTE FUNCTION bnhms_lab_value_guard();
            END IF;
            IF to_regclass('"Charges"') IS NOT NULL THEN
                DROP TRIGGER IF EXISTS trg_charges_guard ON "Charges";
                CREATE TRIGGER trg_charges_guard BEFORE UPDATE ON "Charges" FOR EACH ROW EXECUTE FUNCTION bnhms_charge_guard();
            END IF;
        END $$;
        """;

    public static Task ApplyAsync(HmsDbContext db, CancellationToken ct = default) => db.Database.ExecuteSqlRawAsync(Sql, ct);
}
