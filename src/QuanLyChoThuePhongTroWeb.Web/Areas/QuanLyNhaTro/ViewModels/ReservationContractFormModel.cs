using QuanLyChoThuePhongTroWeb.Application.Features.ReservationSettlement;

namespace QuanLyChoThuePhongTroWeb.Areas.QuanLyNhaTro.ViewModels;

public sealed record ReservationContractFormModel(ReservationContractDraft Draft,
    ReservationContractInput Input);
