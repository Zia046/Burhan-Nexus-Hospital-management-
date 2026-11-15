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

        -- Phase 5: commercial pharmacy records (retail sales, supplier accounts) cannot exist in a Government hospital.
        CREATE OR REPLACE FUNCTION bnhms_pharmacy_private_only() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF EXISTS (SELECT 1 FROM "HospitalConfiguration" WHERE "HospitalType" = 'Government') THEN
                RAISE EXCEPTION 'Pharmacy % are only available in Private hospital mode.', TG_TABLE_NAME;
            END IF;
            RETURN NEW;
        END;
        $$;

        -- Phase 6: a verified / released radiology report can only change after an amendment (which first stores a revision
        -- and moves the examination back to "Report pending"). Direct edits of a verified report are refused by the database.
        CREATE OR REPLACE FUNCTION bnhms_radiology_report_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF OLD."Status" IN ('Verified','Released') AND NEW."Status" IN ('Verified','Released') AND (
                   NEW."Findings" IS DISTINCT FROM OLD."Findings" OR NEW."Impression" IS DISTINCT FROM OLD."Impression"
                OR NEW."Technique" IS DISTINCT FROM OLD."Technique" OR NEW."Recommendations" IS DISTINCT FROM OLD."Recommendations"
                OR NEW."ClinicalIndication" IS DISTINCT FROM OLD."ClinicalIndication" OR NEW."ReportingDoctorName" IS DISTINCT FROM OLD."ReportingDoctorName"
                OR NEW."ReportingDoctorDesignation" IS DISTINCT FROM OLD."ReportingDoctorDesignation" OR NEW."VerifiedByUserId" IS DISTINCT FROM OLD."VerifiedByUserId"
                OR NEW."VerifiedUtc" IS DISTINCT FROM OLD."VerifiedUtc") THEN
                RAISE EXCEPTION 'A verified radiology report cannot be changed directly. Amend it with a reason first.';
            END IF;
            IF OLD."Status" = 'Cancelled' AND NEW."Status" <> 'Cancelled' THEN
                RAISE EXCEPTION 'A cancelled examination cannot be re-opened. Book it again.';
            END IF;
            RETURN NEW;
        END;
        $$;

        -- Phase 6: attachment records keep their file link forever; only the "removed" flag (with a reason) may be set once.
        CREATE OR REPLACE FUNCTION bnhms_radiology_attachment_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."StoredPath" IS DISTINCT FROM OLD."StoredPath" OR NEW."Sha256" IS DISTINCT FROM OLD."Sha256" OR NEW."ExamId" IS DISTINCT FROM OLD."ExamId"
               OR NEW."PatientId" IS DISTINCT FROM OLD."PatientId" OR NEW."SizeBytes" IS DISTINCT FROM OLD."SizeBytes"
               OR NEW."OriginalFileName" IS DISTINCT FROM OLD."OriginalFileName" OR NEW."UploadedUtc" IS DISTINCT FROM OLD."UploadedUtc"
               OR (OLD."IsRemoved" AND NOT NEW."IsRemoved") THEN
                RAISE EXCEPTION 'Radiology attachment records cannot be changed.';
            END IF;
            RETURN NEW;
        END;
        $$;

        -- Phase 7: a closed bed allocation is never re-opened or moved; a discharged / cancelled admission is never re-opened;
        -- a recorded medication dose (administered / missed / withheld) is never changed.
        CREATE OR REPLACE FUNCTION bnhms_ipd_allocation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."BedId" IS DISTINCT FROM OLD."BedId" OR NEW."AdmissionId" IS DISTINCT FROM OLD."AdmissionId" OR NEW."FromUtc" IS DISTINCT FROM OLD."FromUtc"
               OR (OLD."ToUtc" IS NOT NULL AND NEW."ToUtc" IS DISTINCT FROM OLD."ToUtc") THEN
                RAISE EXCEPTION 'Bed occupancy history cannot be changed.';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE OR REPLACE FUNCTION bnhms_ipd_admission_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF OLD."Status" IN ('Discharged','Cancelled','Referred') AND NEW."Status" IS DISTINCT FROM OLD."Status" THEN
                RAISE EXCEPTION 'A closed admission cannot be re-opened. Register a new admission.';
            END IF;
            IF NEW."PatientId" IS DISTINCT FROM OLD."PatientId" OR NEW."AdmissionNumber" IS DISTINCT FROM OLD."AdmissionNumber" THEN
                RAISE EXCEPTION 'The patient / number of an admission cannot be changed.';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE OR REPLACE FUNCTION bnhms_mar_dose_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF OLD."Status" IN ('Administered','Missed','Withheld') THEN
                RAISE EXCEPTION 'A recorded medication dose cannot be changed.';
            END IF;
            RETURN NEW;
        END;
        $$;
        CREATE OR REPLACE FUNCTION bnhms_newborn_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."MotherPatientId" IS DISTINCT FROM OLD."MotherPatientId" OR NEW."DeliveryId" IS DISTINCT FROM OLD."DeliveryId"
               OR (OLD."NewbornPatientId" IS NOT NULL AND NEW."NewbornPatientId" IS DISTINCT FROM OLD."NewbornPatientId") THEN
                RAISE EXCEPTION 'The mother–baby link cannot be changed.';
            END IF;
            RETURN NEW;
        END;
        $$;

        DO $$
        DECLARE t text;
        BEGIN
            FOREACH t IN ARRAY ARRAY['CashierLedger','DoctorLedger','DoctorSettlements','FinancialAdjustments','DailyCashClosings','ReceiptPrints','ServiceFees',
                                       'LabItemEvents','LabResultRevisions','LabReportPrints',
                                       'RadiologyExamEvents','RadiologyReportRevisions','RadiologyReportPrints',
                                       'IpdBedStatusChanges','IpdTransfers','IpdEvents','ClinicalRecords','IpdBedCharges','DeliveryRecords',
                                       'InventoryTransactions','SupplierLedger','SupplierPayments','MedicinePriceChanges','StockReceipts','StockReceiptItems',
                                       'StockTransfers','StockTransferItems','MedicineReturns','MedicineReturnItems','SupplierReturns','SupplierReturnItems'] LOOP
                IF to_regclass(format('%I', t)) IS NOT NULL THEN
                    EXECUTE format('DROP TRIGGER IF EXISTS trg_%s_immutable ON %I', lower(t), t);
                    EXECUTE format('CREATE TRIGGER trg_%s_immutable BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION bnhms_finance_immutable()', lower(t), t);
                    EXECUTE format('DROP TRIGGER IF EXISTS trg_%s_no_truncate ON %I', lower(t), t);
                    EXECUTE format('CREATE TRIGGER trg_%s_no_truncate BEFORE TRUNCATE ON %I FOR EACH STATEMENT EXECUTE FUNCTION bnhms_finance_immutable()', lower(t), t);
                END IF;
            END LOOP;
            FOREACH t IN ARRAY ARRAY['Receipts','ReceiptLines','Charges','Refunds','CashDeposits','LabOrders','LabOrderItems','LabResultValues','LabSamples',
                                       'Medicines','MedicineBatches','StockBalances','PatientMedicineIssues','PatientMedicineIssueItems','PharmacySales',
                                       'PharmacySaleItems','StockAdjustments','Suppliers','StockLocations',
                                       'RadiologyServices','RadiologyOrders','RadiologyExams','RadiologyAttachments',
                                       'IpdWards','IpdRooms','IpdBeds','IpdAdmissions','IpdBedAllocations','Pregnancies','LabourRecords','NewbornRecords',
                                       'NicuDetails','NursingTasks','ShiftHandovers','MedicationOrders','MedicationDoses'] LOOP
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
            FOREACH t IN ARRAY ARRAY['PharmacySales','SupplierPayments','SupplierLedger'] LOOP
                IF to_regclass(format('%I', t)) IS NOT NULL THEN
                    EXECUTE format('DROP TRIGGER IF EXISTS trg_%s_private_only ON %I', lower(t), t);
                    EXECUTE format('CREATE TRIGGER trg_%s_private_only BEFORE INSERT ON %I FOR EACH ROW EXECUTE FUNCTION bnhms_pharmacy_private_only()', lower(t), t);
                END IF;
            END LOOP;
            IF to_regclass('"RadiologyExams"') IS NOT NULL THEN
                DROP TRIGGER IF EXISTS trg_radiologyexams_guard ON "RadiologyExams";
                CREATE TRIGGER trg_radiologyexams_guard BEFORE UPDATE ON "RadiologyExams" FOR EACH ROW EXECUTE FUNCTION bnhms_radiology_report_guard();
            END IF;
            IF to_regclass('"RadiologyAttachments"') IS NOT NULL THEN
                DROP TRIGGER IF EXISTS trg_radiologyattachments_guard ON "RadiologyAttachments";
                CREATE TRIGGER trg_radiologyattachments_guard BEFORE UPDATE ON "RadiologyAttachments" FOR EACH ROW EXECUTE FUNCTION bnhms_radiology_attachment_guard();
            END IF;
            IF to_regclass('"IpdBedAllocations"') IS NOT NULL THEN
                DROP TRIGGER IF EXISTS trg_ipdbedallocations_guard ON "IpdBedAllocations";
                CREATE TRIGGER trg_ipdbedallocations_guard BEFORE UPDATE ON "IpdBedAllocations" FOR EACH ROW EXECUTE FUNCTION bnhms_ipd_allocation_guard();
                DROP TRIGGER IF EXISTS trg_ipdadmissions_guard ON "IpdAdmissions";
                CREATE TRIGGER trg_ipdadmissions_guard BEFORE UPDATE ON "IpdAdmissions" FOR EACH ROW EXECUTE FUNCTION bnhms_ipd_admission_guard();
                DROP TRIGGER IF EXISTS trg_medicationdoses_guard ON "MedicationDoses";
                CREATE TRIGGER trg_medicationdoses_guard BEFORE UPDATE ON "MedicationDoses" FOR EACH ROW EXECUTE FUNCTION bnhms_mar_dose_guard();
                DROP TRIGGER IF EXISTS trg_newbornrecords_guard ON "NewbornRecords";
                CREATE TRIGGER trg_newbornrecords_guard BEFORE UPDATE ON "NewbornRecords" FOR EACH ROW EXECUTE FUNCTION bnhms_newborn_guard();
            END IF;
            IF to_regclass('"Charges"') IS NOT NULL THEN
                DROP TRIGGER IF EXISTS trg_charges_guard ON "Charges";
                CREATE TRIGGER trg_charges_guard BEFORE UPDATE ON "Charges" FOR EACH ROW EXECUTE FUNCTION bnhms_charge_guard();
            END IF;
        END $$;
        """;

    public static Task ApplyAsync(HmsDbContext db, CancellationToken ct = default) => db.Database.ExecuteSqlRawAsync(Sql, ct);
}
