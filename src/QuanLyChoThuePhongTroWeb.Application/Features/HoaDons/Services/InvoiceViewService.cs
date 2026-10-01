using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Security;
using QuanLyChoThuePhongTroWeb.Application.Abstractions.Services;
using QuanLyChoThuePhongTroWeb.Application.Common.Configurations;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.Common.Security;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.DTOs;
using QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Persistence;

namespace QuanLyChoThuePhongTroWeb.Application.Features.HoaDons.Services
{
    public class InvoiceViewService : IInvoiceViewService
    {
        private readonly IInvoiceViewStore _invoiceViewStore;
        private readonly IHoaDonStore _hoaDonStore;
        private readonly IEmployeeAccessService _employeeAccessService;
        private readonly IInvoiceDocumentExporter _exporter;
        private readonly IVietQRService _vietQrService;
        private readonly VietQrSettings _vietQrSettings;

        public InvoiceViewService(
            IInvoiceViewStore invoiceViewStore,
            IHoaDonStore hoaDonStore,
            IEmployeeAccessService employeeAccessService,
            IInvoiceDocumentExporter exporter,
            IVietQRService vietQrService,
            VietQrSettings vietQrSettings)
        {
            _invoiceViewStore = invoiceViewStore;
            _hoaDonStore = hoaDonStore;
            _employeeAccessService = employeeAccessService;
            _exporter = exporter;
            _vietQrService = vietQrService;
            _vietQrSettings = vietQrSettings;
        }

        public async Task<DataTableResponse<HoaDonRes>> GetEmployeeListAsync(
            DataTableRequest request,
            InvoiceListFilter filter,
            int actorId,
            CancellationToken ct = default)
        {
            var denied = new DataTableResponse<HoaDonRes>
            {
                draw = request.Draw,
                recordsTotal = 0,
                recordsFiltered = 0,
                data = new List<HoaDonRes>()
            };

            if (actorId <= 0)
            {
                return denied;
            }

            var scope = await _employeeAccessService.GetScopeAsync(actorId, ct);
            if (scope == null)
            {
                return denied;
            }

            var normalizedFilter = new InvoiceListFilter
            {
                ChiNhanhId = filter.ChiNhanhId,
                Thang = filter.Thang,
                Nam = filter.Nam,
                TrangThaiThanhToan = (filter.TrangThaiThanhToan >= 0 && filter.TrangThaiThanhToan <= 2)
                    ? filter.TrangThaiThanhToan
                    : -1,
                TrangThaiPhatHanh = (filter.TrangThaiPhatHanh >= 0 && filter.TrangThaiPhatHanh <= 4)
                    ? filter.TrangThaiPhatHanh
                    : -1
            };

            if (!scope.IsAdmin)
            {
                if (scope.ActiveBranchIds.Count == 0)
                {
                    return denied;
                }

                if (normalizedFilter.ChiNhanhId > 0)
                {
                    if (!scope.CanAccessBranch(normalizedFilter.ChiNhanhId))
                    {
                        return denied;
                    }

                    return await _invoiceViewStore.GetEmployeeListAsync(request, normalizedFilter, null, ct);
                }

                return await _invoiceViewStore.GetEmployeeListAsync(request, normalizedFilter, scope.ActiveBranchIds.ToList(), ct);
            }

            return await _invoiceViewStore.GetEmployeeListAsync(request, normalizedFilter, null, ct);
        }

        public async Task<HoaDonChiTietRes?> GetEmployeeDetailAsync(int hoaDonId, int actorId, CancellationToken ct = default)
        {
            if (actorId <= 0)
            {
                return null;
            }

            var branchId = await _invoiceViewStore.GetViewableBranchIdAsync(hoaDonId, ct);
            if (!branchId.HasValue)
            {
                return null;
            }

            var canRead = await _employeeAccessService.CanPerformAsync(actorId, branchId.Value, EmployeeActionCodes.InvoiceRead, ct);
            if (!canRead)
            {
                return null;
            }

            return await _invoiceViewStore.GetDetailAsync(hoaDonId, includeHistory: true, ct);
        }

        public async Task<byte[]?> ExportEmployeePdfAsync(int hoaDonId, int actorId, CancellationToken ct = default)
        {
            var detail = await GetEmployeeDetailAsync(hoaDonId, actorId, ct);
            if (detail == null)
            {
                return null;
            }

            return InvoicePdfComposer.Compose(detail, _vietQrService, _vietQrSettings, _exporter);
        }

        public async Task<byte[]?> ExportEmployeeExcelAsync(int hoaDonId, int actorId, CancellationToken ct = default)
        {
            var detail = await GetEmployeeDetailAsync(hoaDonId, actorId, ct);
            if (detail == null)
            {
                return null;
            }

            return _exporter.ExportExcel(detail);
        }

        public async Task<HoaDonChiTietRes?> GetTenantDetailAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default)
        {
            if (nguoiThueId <= 0)
            {
                return null;
            }

            var isOwner = await _hoaDonStore.CheckHoaDonOwnershipAsync(hoaDonId, nguoiThueId, ct);
            if (!isOwner)
            {
                return null;
            }

            return await _invoiceViewStore.GetDetailAsync(hoaDonId, includeHistory: false, ct);
        }

        public async Task<byte[]?> ExportTenantPdfAsync(int hoaDonId, int nguoiThueId, CancellationToken ct = default)
        {
            var detail = await GetTenantDetailAsync(hoaDonId, nguoiThueId, ct);
            if (detail == null)
            {
                return null;
            }

            return InvoicePdfComposer.Compose(detail, _vietQrService, _vietQrSettings, _exporter);
        }
    }
}
