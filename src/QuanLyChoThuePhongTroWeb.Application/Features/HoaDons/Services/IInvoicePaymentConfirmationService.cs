using System;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public interface IInvoicePaymentConfirmationService
    {
        Task<YeuCauThanhToanRes> TaoYeuCauThanhToanAsync(int hoaDonId, decimal soTien, string noiDung, int? nguoiTaoId = null, CancellationToken cancellationToken = default);
        Task<MinhChungThanhToanRes> NopMinhChungAsync(int yeuCauId, string hinhAnhUrl, decimal soTienKhaiBao, DateTime ngayChuyen, string? maGiaoDich = null, string? publicId = null, CancellationToken cancellationToken = default);
        Task<XacNhanThanhToanRes> XacNhanMinhChungAsync(int minhChungId, int nguoiXacNhanId, string? ghiChu = null, CancellationToken cancellationToken = default);
        Task TuChoiMinhChungAsync(int minhChungId, int nguoiDoiChieuId, string lyDo, CancellationToken cancellationToken = default);
    }
}
