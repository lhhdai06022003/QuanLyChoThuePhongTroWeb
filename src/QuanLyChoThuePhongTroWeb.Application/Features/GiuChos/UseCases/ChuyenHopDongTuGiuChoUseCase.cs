using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.GiuChos.UseCases;

public sealed class ChuyenHopDongTuGiuChoUseCase(IChuyenHopDongGiuChoStore store)
    : IChuyenHopDongTuGiuChoUseCase
{
    public Task<ContractCandidateDto?> GetCandidateAsync(int reservationId, int actorId) =>
        store.GetCandidateAsync(reservationId, actorId);

    public async Task<ReservationContractResult> ExecuteAsync(int reservationId,
        ReservationContractRequest request, int actorId)
    {
        if (reservationId <= 0 || actorId <= 0 || request.NgayBatDau.Year < 2000 ||
            request.NgayKetThuc < request.NgayBatDau || request.DieuKhoanMauIds is null ||
            request.DieuKhoanMauIds.Any(id => id <= 0))
            return new(false, null, "Ngày hợp đồng hoặc điều khoản không hợp lệ.");
        var contractId = await store.ConvertAsync(reservationId, request with
        { DieuKhoanMauIds = request.DieuKhoanMauIds.Distinct().ToArray() }, actorId);
        return contractId is int id
            ? new(true, id, "Đã lập hợp đồng và chuyển tiền giữ chỗ thành cọc.")
            : new(false, null, "Không thể lập hợp đồng: kiểm tra quyền, hồ sơ khách, phòng, tiền và hạn ký.");
    }
}
