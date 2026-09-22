using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;
using QuanLyChoThuePhongTroWeb.Domain.Entities;
using QuanLyChoThuePhongTroWeb.Domain.Enums;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class InvoicePaymentConfirmationService : IInvoicePaymentConfirmationService
    {
        private readonly IInvoicePaymentStore _store;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<InvoicePaymentConfirmationService> _logger;

        public InvoicePaymentConfirmationService(
            IInvoicePaymentStore store,
            IUnitOfWork unitOfWork,
            ILogger<InvoicePaymentConfirmationService> logger)
        {
            _store = store;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<YeuCauThanhToanRes> TaoYeuCauThanhToanAsync(
            int hoaDonId,
            decimal soTien,
            string noiDung,
            int? nguoiTaoId = null,
            CancellationToken cancellationToken = default)
        {
            var hoaDon = await _store.GetHoaDonByIdAsync(hoaDonId, cancellationToken);
            if (hoaDon == null)
            {
                throw new InvalidOperationException("Không tìm thấy hóa đơn.");
            }

            hoaDon.KiemTraDuDieuKienYeuCauThanhToan();

            var tongDaThanhToan = await _store.GetTongTienDaThanhToanHoaDonAsync(hoaDonId, cancellationToken);
            var conLai = hoaDon.TongTien - tongDaThanhToan;

            if (soTien <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(soTien), "Số tiền yêu cầu thanh toán phải lớn hơn 0.");
            }

            if (soTien > conLai)
            {
                throw new InvalidOperationException("Số tiền yêu cầu thanh toán vượt quá số tiền còn lại của hóa đơn.");
            }

            if (soTien < conLai)
            {
                if (!hoaDon.ChoPhepThanhToanMotPhan)
                {
                    throw new InvalidOperationException("Hóa đơn này không cho phép thanh toán một phần.");
                }

                if (hoaDon.SoTienThanhToanToiThieu.HasValue && soTien < hoaDon.SoTienThanhToanToiThieu.Value)
                {
                    throw new InvalidOperationException($"Số tiền thanh toán phải lớn hơn hoặc bằng mức tối thiểu ({hoaDon.SoTienThanhToanToiThieu.Value:#,##0} VNĐ).");
                }
            }

            var yeuCau = new YeuCauThanhToanHoaDon
            {
                HoaDonId = hoaDonId,
                MaYeuCau = $"REQ-{DateTime.UtcNow:yyyyMMddHHmmss}-{hoaDonId}",
                SoTien = soTien,
                NoiDungChuyenKhoan = noiDung,
                HanThanhToan = hoaDon.HanThanhToan,
                TrangThai = TrangThaiYeuCauThanhToan.ChoThanhToan,
                NguoiTaoId = nguoiTaoId,
                NgayTao = DateTime.UtcNow
            };

            await _store.AddYeuCauThanhToanAsync(yeuCau, cancellationToken);
            await _store.AddLichSuTrangThaiYeuCauAsync(new LichSuTrangThaiYeuCauThanhToanHoaDon
            {
                YeuCauThanhToanHoaDon = yeuCau,
                TrangThaiCu = null,
                TrangThaiMoi = TrangThaiYeuCauThanhToan.ChoThanhToan,
                NguoiThucHienId = nguoiTaoId,
                NgayThucHien = DateTime.UtcNow,
                LyDo = "Tạo yêu cầu thanh toán mới"
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new YeuCauThanhToanRes
            {
                YeuCauThanhToanHoaDonId = yeuCau.YeuCauThanhToanHoaDonId,
                HoaDonId = yeuCau.HoaDonId,
                MaYeuCau = yeuCau.MaYeuCau,
                SoTien = yeuCau.SoTien,
                NoiDungChuyenKhoan = yeuCau.NoiDungChuyenKhoan,
                HanThanhToan = yeuCau.HanThanhToan,
                TrangThai = yeuCau.TrangThai,
                NgayTao = yeuCau.NgayTao
            };
        }

        public async Task<MinhChungThanhToanRes> NopMinhChungAsync(
            int yeuCauId,
            string hinhAnhUrl,
            decimal soTienKhaiBao,
            DateTime ngayChuyen,
            string? maGiaoDich = null,
            string? publicId = null,
            CancellationToken cancellationToken = default)
        {
            var yeuCau = await _store.GetYeuCauThanhToanByIdAsync(yeuCauId, cancellationToken);
            if (yeuCau == null)
            {
                throw new InvalidOperationException("Không tìm thấy yêu cầu thanh toán.");
            }

            if (yeuCau.TrangThai == TrangThaiYeuCauThanhToan.DaHoanTat ||
                yeuCau.TrangThai == TrangThaiYeuCauThanhToan.DaHuy ||
                yeuCau.TrangThai == TrangThaiYeuCauThanhToan.HetHan)
            {
                throw new InvalidOperationException($"Yêu cầu thanh toán ở trạng thái {yeuCau.TrangThai}, không thể nộp thêm minh chứng.");
            }

            var minhChung = new MinhChungThanhToanHoaDon
            {
                YeuCauThanhToanHoaDonId = yeuCauId,
                HinhAnhUrl = hinhAnhUrl,
                PublicId = publicId,
                MaGiaoDichNganHang = maGiaoDich,
                SoTienKhaiBao = soTienKhaiBao,
                NgayChuyenKhaiBao = ngayChuyen,
                TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.ChoXacNhan,
                NgayTao = DateTime.UtcNow
            };

            var trangThaiCu = yeuCau.TrangThai;
            yeuCau.TrangThai = TrangThaiYeuCauThanhToan.DangDoiChieu;
            yeuCau.NgayCapNhat = DateTime.UtcNow;
            _store.UpdateYeuCauThanhToan(yeuCau);

            await _store.AddMinhChungThanhToanAsync(minhChung, cancellationToken);
            await _store.AddLichSuTrangThaiYeuCauAsync(new LichSuTrangThaiYeuCauThanhToanHoaDon
            {
                YeuCauThanhToanHoaDonId = yeuCauId,
                TrangThaiCu = trangThaiCu,
                TrangThaiMoi = TrangThaiYeuCauThanhToan.DangDoiChieu,
                NgayThucHien = DateTime.UtcNow,
                LyDo = "Khách thuê nộp minh chứng chuyển khoản"
            }, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new MinhChungThanhToanRes
            {
                MinhChungThanhToanHoaDonId = minhChung.MinhChungThanhToanHoaDonId,
                YeuCauThanhToanHoaDonId = minhChung.YeuCauThanhToanHoaDonId,
                HinhAnhUrl = minhChung.HinhAnhUrl,
                PublicId = minhChung.PublicId,
                MaGiaoDichNganHang = minhChung.MaGiaoDichNganHang,
                SoTienKhaiBao = minhChung.SoTienKhaiBao,
                NgayChuyenKhaiBao = minhChung.NgayChuyenKhaiBao,
                TrangThaiDoiChieu = minhChung.TrangThaiDoiChieu,
                LyDoTuChoi = minhChung.LyDoTuChoi
            };
        }

        public async Task<XacNhanThanhToanRes> XacNhanMinhChungAsync(
            int minhChungId,
            int nguoiXacNhanId,
            string? ghiChu = null,
            CancellationToken cancellationToken = default)
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var minhChung = await _store.GetMinhChungByIdWithDetailsAsync(minhChungId, cancellationToken);
                if (minhChung == null)
                {
                    throw new InvalidOperationException("Không tìm thấy minh chứng thanh toán.");
                }

                if (minhChung.TrangThaiDoiChieu == TrangThaiMinhChungThanhToan.DaXacNhan)
                {
                    throw new InvalidOperationException("Minh chứng thanh toán này đã được xác nhận trước đó.");
                }

                if (minhChung.TrangThaiDoiChieu == TrangThaiMinhChungThanhToan.TuChoi)
                {
                    throw new InvalidOperationException("Không thể xác nhận minh chứng đã bị từ chối.");
                }

                var yeuCau = minhChung.YeuCauThanhToanHoaDon;
                if (yeuCau == null)
                {
                    throw new InvalidOperationException("Không tìm thấy yêu cầu thanh toán của minh chứng.");
                }

                var hoaDon = yeuCau.HoaDon;
                if (hoaDon == null)
                {
                    throw new InvalidOperationException("Không tìm thấy hóa đơn của yêu cầu thanh toán.");
                }

                var tongDaThanhToan = await _store.GetTongTienDaThanhToanHoaDonAsync(hoaDon.HoaDonId, cancellationToken);
                if (tongDaThanhToan + minhChung.SoTienKhaiBao > hoaDon.TongTien)
                {
                    throw new InvalidOperationException("Tổng số tiền thanh toán không được vượt quá tổng tiền hóa đơn.");
                }

                // 1. Cập nhật minh chứng
                minhChung.TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.DaXacNhan;
                minhChung.NguoiDoiChieuId = nguoiXacNhanId;
                minhChung.NgayDoiChieu = DateTime.UtcNow;
                _store.UpdateMinhChungThanhToan(minhChung);

                // 2. Tạo bản ghi LichSuThanhToan
                var maGiaoDich = !string.IsNullOrWhiteSpace(minhChung.MaGiaoDichNganHang)
                    ? minhChung.MaGiaoDichNganHang
                    : $"PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{minhChung.MinhChungThanhToanHoaDonId}";

                var lichSu = new LichSuThanhToan
                {
                    HoaDonId = hoaDon.HoaDonId,
                    MaGiaoDich = maGiaoDich,
                    SoTienThanhToan = minhChung.SoTienKhaiBao,
                    PhuongThucThanhToan = PhuongThucThanhToan.ChuyenKhoan,
                    NguoiXacNhanId = nguoiXacNhanId,
                    NgayThanhToan = minhChung.NgayChuyenKhaiBao,
                    NgayXacNhan = DateTime.UtcNow,
                    MinhChungThanhToanHoaDonId = minhChung.MinhChungThanhToanHoaDonId,
                    GhiChu = ghiChu ?? "Xác nhận chuyển khoản VietQR"
                };
                await _store.AddLichSuThanhToanAsync(lichSu, cancellationToken);

                // 3. Cập nhật yêu cầu thanh toán
                var oldYcStatus = yeuCau.TrangThai;
                yeuCau.TrangThai = TrangThaiYeuCauThanhToan.DaHoanTat;
                yeuCau.NgayCapNhat = DateTime.UtcNow;
                _store.UpdateYeuCauThanhToan(yeuCau);

                await _store.AddLichSuTrangThaiYeuCauAsync(new LichSuTrangThaiYeuCauThanhToanHoaDon
                {
                    YeuCauThanhToanHoaDonId = yeuCau.YeuCauThanhToanHoaDonId,
                    TrangThaiCu = oldYcStatus,
                    TrangThaiMoi = TrangThaiYeuCauThanhToan.DaHoanTat,
                    NguoiThucHienId = nguoiXacNhanId,
                    NgayThucHien = DateTime.UtcNow,
                    LyDo = "Đối soát minh chứng thành công"
                }, cancellationToken);

                // 4. Cập nhật trạng thái hóa đơn
                var oldHdStatus = hoaDon.TrangThaiHoaDon;
                var tongSauThanhToan = tongDaThanhToan + minhChung.SoTienKhaiBao;
                var newHdStatus = (tongSauThanhToan >= hoaDon.TongTien)
                    ? TrangThaiHoaDon.DaThanhToan
                    : TrangThaiHoaDon.ThanhToanMotPhan;

                hoaDon.TrangThaiHoaDon = newHdStatus;
                hoaDon.NgayCapNhat = DateTime.UtcNow;
                _store.UpdateHoaDon(hoaDon);

                await _store.AddLichSuTrangThaiHoaDonAsync(new LichSuTrangThaiHoaDon
                {
                    HoaDonId = hoaDon.HoaDonId,
                    TrangThaiPhatHanhCu = hoaDon.TrangThaiPhatHanh,
                    TrangThaiPhatHanhMoi = hoaDon.TrangThaiPhatHanh,
                    TrangThaiThanhToanCu = oldHdStatus,
                    TrangThaiThanhToanMoi = newHdStatus,
                    NguoiThucHienId = nguoiXacNhanId,
                    NgayThucHien = DateTime.UtcNow,
                    LyDo = $"Xác nhận thanh toán minh chứng {minhChung.MinhChungThanhToanHoaDonId}"
                }, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                return new XacNhanThanhToanRes
                {
                    LichSuThanhToanId = lichSu.LichSuThanhToanId,
                    HoaDonId = hoaDon.HoaDonId,
                    MaGiaoDich = lichSu.MaGiaoDich,
                    SoTienThanhToan = lichSu.SoTienThanhToan,
                    MinhChungThanhToanHoaDonId = lichSu.MinhChungThanhToanHoaDonId,
                    TrangThaiHoaDon = newHdStatus,
                    NgayThanhToan = lichSu.NgayThanhToan
                };
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Lỗi khi xác nhận minh chứng thanh toán ID: {MinhChungId}", minhChungId);
                throw;
            }
        }

        public async Task TuChoiMinhChungAsync(
            int minhChungId,
            int nguoiDoiChieuId,
            string lyDo,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(lyDo))
            {
                throw new ArgumentException("Lý do từ chối không được để trống.", nameof(lyDo));
            }

            await using var tx = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var minhChung = await _store.GetMinhChungByIdWithDetailsAsync(minhChungId, cancellationToken);
                if (minhChung == null)
                {
                    throw new InvalidOperationException("Không tìm thấy minh chứng thanh toán.");
                }

                if (minhChung.TrangThaiDoiChieu != TrangThaiMinhChungThanhToan.ChoXacNhan)
                {
                    throw new InvalidOperationException($"Không thể từ chối minh chứng ở trạng thái {minhChung.TrangThaiDoiChieu}.");
                }

                minhChung.TrangThaiDoiChieu = TrangThaiMinhChungThanhToan.TuChoi;
                minhChung.LyDoTuChoi = lyDo;
                minhChung.NguoiDoiChieuId = nguoiDoiChieuId;
                minhChung.NgayDoiChieu = DateTime.UtcNow;
                _store.UpdateMinhChungThanhToan(minhChung);

                var yeuCau = minhChung.YeuCauThanhToanHoaDon;
                if (yeuCau != null)
                {
                    var oldYcStatus = yeuCau.TrangThai;
                    yeuCau.TrangThai = TrangThaiYeuCauThanhToan.TuChoi;
                    yeuCau.NgayCapNhat = DateTime.UtcNow;
                    _store.UpdateYeuCauThanhToan(yeuCau);

                    await _store.AddLichSuTrangThaiYeuCauAsync(new LichSuTrangThaiYeuCauThanhToanHoaDon
                    {
                        YeuCauThanhToanHoaDonId = yeuCau.YeuCauThanhToanHoaDonId,
                        TrangThaiCu = oldYcStatus,
                        TrangThaiMoi = TrangThaiYeuCauThanhToan.TuChoi,
                        NguoiThucHienId = nguoiDoiChieuId,
                        NgayThucHien = DateTime.UtcNow,
                        LyDo = lyDo
                    }, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Lỗi khi từ chối minh chứng thanh toán ID: {MinhChungId}", minhChungId);
                throw;
            }
        }
    }
}
