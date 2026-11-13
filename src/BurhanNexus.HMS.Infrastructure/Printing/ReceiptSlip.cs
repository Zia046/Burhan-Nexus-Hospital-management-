using BurhanNexus.HMS.Core.Domain;

namespace BurhanNexus.HMS.Infrastructure.Printing;

public sealed record ReceiptSlipLine(string Service, string? Doctor, string Fee, string? Discount, string Net, string Paid, string? Balance);

/// <summary>Everything printed on a receipt; the same data drives the HTML preview, browser print and ESC/POS.</summary>
public sealed record ReceiptSlipData(
    string HospitalName, string? Address, string? Contact, string ReceiptNumber, string DateTimeText,
    string? PatientName, string? PatientNumber, string? AgeGender, IReadOnlyList<ReceiptSlipLine> Lines,
    string TotalFee, string? TotalDiscount, string TotalNet, string Paid, string? Balance, string? Refunded,
    string PaymentMethod, string? PaymentReference, string? CashierName, string CounterName, string? Footer,
    string PrintedText, bool IsReprint, bool IsCancelled, string? CancelReason);

public static class ReceiptComposer
{
    public static byte[] Receipt(ReceiptSlipData d, PrinterProfile p)
    {
        var w = p.CharsPerLine;
        var sep = new string('-', w);
        var b = new EscPosBuilder().Initialize();
        b.AlignCenter().Bold(true).Size(2, 2);
        foreach (var l in EscPosBuilder.Wrap(d.HospitalName, w / 2)) b.Line(l);
        b.Size(1, 1).Bold(false);
        if (!string.IsNullOrWhiteSpace(d.Address)) foreach (var l in EscPosBuilder.Wrap(d.Address, w)) b.Line(l);
        if (!string.IsNullOrWhiteSpace(d.Contact)) b.Line(d.Contact);
        b.Line(sep).Bold(true).Line(d.IsCancelled ? "*** CANCELLED RECEIPT ***" : "PAYMENT RECEIPT").Bold(false);
        b.Align(SlipAlignment.Left);
        b.Line(EscPosBuilder.Justify("Receipt No:", d.ReceiptNumber, w));
        b.Line(EscPosBuilder.Justify("Date:", d.DateTimeText, w));
        if (d.PatientName is not null) foreach (var l in EscPosBuilder.Wrap("Patient: " + d.PatientName, w)) b.Line(l);
        if (d.PatientNumber is not null) b.Line(EscPosBuilder.Justify("Patient ID:", d.PatientNumber, w));
        if (d.AgeGender is not null) b.Line(EscPosBuilder.Justify("Age/Gender:", d.AgeGender, w));
        b.Line(sep);
        foreach (var l in d.Lines)
        {
            b.Bold(p.BoldText);
            foreach (var t in EscPosBuilder.Wrap(l.Service, w)) b.Line(t);
            b.Bold(false);
            if (l.Doctor is not null) b.Line("  " + l.Doctor);
            b.Line(EscPosBuilder.Justify("  Fee", l.Fee, w));
            if (l.Discount is not null) b.Line(EscPosBuilder.Justify("  Discount", "-" + l.Discount, w));
            b.Line(EscPosBuilder.Justify("  Paid", l.Paid, w));
            if (l.Balance is not null) b.Line(EscPosBuilder.Justify("  Remaining", l.Balance, w));
        }
        b.Line(sep);
        b.Line(EscPosBuilder.Justify("Total fee", d.TotalFee, w));
        if (d.TotalDiscount is not null) b.Line(EscPosBuilder.Justify("Discount", "-" + d.TotalDiscount, w));
        b.Line(EscPosBuilder.Justify("Net payable", d.TotalNet, w));
        b.Bold(true).Size(1, 2).Line(EscPosBuilder.Justify("PAID", d.Paid, w)).Size(1, 1).Bold(false);
        if (d.Balance is not null) b.Line(EscPosBuilder.Justify("Remaining", d.Balance, w));
        if (d.Refunded is not null) b.Line(EscPosBuilder.Justify("Refunded", "-" + d.Refunded, w));
        b.Line(EscPosBuilder.Justify("Payment:", d.PaymentMethod + (d.PaymentReference is null ? "" : " " + d.PaymentReference), w));
        b.Line(EscPosBuilder.Justify("Counter:", d.CounterName, w));
        if (d.CashierName is not null) b.Line(EscPosBuilder.Justify("Cashier:", d.CashierName, w));
        if (d.IsCancelled && d.CancelReason is not null) foreach (var t in EscPosBuilder.Wrap("Cancelled: " + d.CancelReason, w)) b.Line(t);
        b.Line(sep).AlignCenter();
        if (!string.IsNullOrWhiteSpace(d.Footer)) foreach (var t in EscPosBuilder.Wrap(d.Footer, w)) b.Line(t);
        b.Line(d.PrintedText + (d.IsReprint ? " (REPRINT)" : ""));
        b.Feed(3);
        if (p.AutoCut) b.Cut();
        return b.ToArray();
    }
}
