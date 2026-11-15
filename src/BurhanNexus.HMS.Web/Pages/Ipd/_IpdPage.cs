using BurhanNexus.HMS.Core.Domain;
using BurhanNexus.HMS.Core.Security;
using BurhanNexus.HMS.Infrastructure.Ipd;
using BurhanNexus.HMS.Infrastructure.Patients;

namespace BurhanNexus.HMS.Web.Pages.Ipd;

/// <summary>Shared helpers of the IPD pages: services, ward-level access and record permissions.</summary>
public abstract class IpdPageModel : HmsPageModel
{
    protected IIpdService Ipd => HttpContext.RequestServices.GetRequiredService<IIpdService>();
    protected IIpdSetupService Setup => HttpContext.RequestServices.GetRequiredService<IIpdSetupService>();
    protected IIpdQueryService Query => HttpContext.RequestServices.GetRequiredService<IIpdQueryService>();
    protected IMaternityService Maternity => HttpContext.RequestServices.GetRequiredService<IMaternityService>();
    protected HospitalClock Clock => HttpContext.RequestServices.GetRequiredService<HospitalClock>();

    protected void Nav(string nav, string tab)
    {
        ViewData["Nav"] = nav;
        ViewData["IpdTab"] = tab;
    }

    /// <summary>Wards the user may work in (null = all wards).</summary>
    protected async Task<IReadOnlyList<int>?> WardsAsync() => await Query.VisibleWardIdsAsync(CurrentUserId, await CanAsync(Perm.IpdViewAllWards));

    /// <summary>Users only open patients of their own wards (unless they may see all wards).</summary>
    protected async Task<bool> CanAccessAsync(IpdAdmission a)
    {
        var wards = await WardsAsync();
        return wards is null || a.CurrentWardId is null || wards.Contains(a.CurrentWardId.Value)
               || (a.MotherAdmissionId is long m && (await Query.AdmissionAsync(m))?.CurrentWardId is int mw && wards.Contains(mw));
    }

    /// <summary>Permission needed to enter each kind of clinical record (checked on the server for every entry).</summary>
    public static string WritePermission(RecordKind k) => k switch
    {
        RecordKind.ProgressNote => Perm.IpdProgressNotes,
        RecordKind.Vitals or RecordKind.NursingAssessment => Perm.IpdManageNursing,
        RecordKind.NursingNote => Perm.IpdNursingNotes,
        RecordKind.GynaeHistory or RecordKind.GynaeAssessment => Perm.IpdEnterGynae,
        RecordKind.AncVisit => Perm.IpdManageAnc,
        RecordKind.LabourAssessment or RecordKind.LabourObservation => Perm.IpdLabourObservations,
        RecordKind.PostnatalAssessment => Perm.IpdPostnatal,
        RecordKind.PreOpAssessment => Perm.OtEnterPreOp,
        RecordKind.AnesthesiaAssessment or RecordKind.IntraOpMonitoring => Perm.OtEnterAnesthesia,
        RecordKind.PacuObservation => Perm.OtManageRecovery,
        RecordKind.PostOpRecord => Perm.OtEnterPostOp,
        _ => Perm.IpdNicuObservations,
    };

    /// <summary>Reproductive / obstetric records are only visible with "View Gynae Records".</summary>
    public static bool IsSensitive(RecordKind k) => k is RecordKind.GynaeHistory or RecordKind.GynaeAssessment or RecordKind.AncVisit;

    public static string Title(RecordKind k) => k switch
    {
        RecordKind.ProgressNote => "Daily progress note",
        RecordKind.Vitals => "Vital signs",
        RecordKind.NursingAssessment => "Nursing assessment",
        RecordKind.NursingNote => "Nursing note",
        RecordKind.GynaeHistory => "Gynae / obstetric history",
        RecordKind.GynaeAssessment => "Gynae assessment",
        RecordKind.AncVisit => "ANC visit",
        RecordKind.LabourAssessment => "Labour initial assessment",
        RecordKind.LabourObservation => "Labour observation / partograph entry",
        RecordKind.PostnatalAssessment => "Postnatal assessment",
        RecordKind.NicuAssessment => "Newborn clinical assessment",
        RecordKind.NicuObservation => "NICU observations",
        RecordKind.NicuFeeding => "Feeding & intake / output",
        RecordKind.NicuCare => "NICU care (oxygen, phototherapy, incubator)",
        RecordKind.NicuProgress => "NICU daily progress",
        RecordKind.PreOpAssessment => "Pre-operative assessment",
        RecordKind.AnesthesiaAssessment => "Pre-anesthesia assessment",
        RecordKind.IntraOpMonitoring => "Intra-operative monitoring",
        RecordKind.PacuObservation => "Recovery (PACU) observations",
        RecordKind.PostOpRecord => "Post-operative record",
        _ => k.ToString(),
    };
}
