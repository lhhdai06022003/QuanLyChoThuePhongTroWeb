using System;
using System.Linq;
using System.Reflection;
using QuanLyChoThuePhongTroWeb.Application.Common.Files;
using QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class MeterReadingContractTests
    {
        private readonly Assembly _appAssembly = typeof(UploadFile).Assembly;

        [Fact]
        public void UploadMeterImageRequest_MustExist_AndNotContainCustomerProposalOrHttpTypes()
        {
            var type = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs.UploadMeterImageRequest");
            Assert.NotNull(type);

            var propNames = type.GetProperties().Select(p => p.Name).ToList();
            Assert.Contains("PhongTroId", propNames);
            Assert.Contains("Thang", propNames);
            Assert.Contains("Nam", propNames);
            Assert.Contains("LoaiDongHo", propNames);
            Assert.Contains("File", propNames);

            // Chặn trường số khách đề xuất
            Assert.DoesNotContain(propNames, p => p.IndexOf("DeXuat", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(propNames, p => p.IndexOf("Proposal", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.DoesNotContain(propNames, p => p.IndexOf("Khach", StringComparison.OrdinalIgnoreCase) >= 0);

            // Chặn IFormFile và Http types
            foreach (var prop in type.GetProperties())
            {
                var typeName = prop.PropertyType.FullName ?? string.Empty;
                Assert.DoesNotContain("IFormFile", typeName);
                Assert.DoesNotContain("Microsoft.AspNetCore", typeName);
            }
        }

        [Fact]
        public void MeterImageWorkflowResult_MustExist_AndSeparateAiAndConfirmedValues()
        {
            var type = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs.MeterImageWorkflowResult");
            Assert.NotNull(type);

            var propNames = type.GetProperties().Select(p => p.Name).ToList();
            Assert.Contains("AnhChiSoDongHoId", propNames);
            Assert.Contains("DichVuDienNuocCuaPhongId", propNames);
            Assert.Contains("PhongTroId", propNames);
            Assert.Contains("Thang", propNames);
            Assert.Contains("Nam", propNames);
            Assert.Contains("LoaiDongHo", propNames);
            Assert.Contains("Url", propNames);
            Assert.Contains("TrangThaiXuLy", propNames);
            Assert.Contains("GiaTriAIGoiY", propNames);
            Assert.Contains("GiaTriXacNhan", propNames);
            Assert.Contains("DoTinCay", propNames);
            Assert.Contains("ThongBaoLoi", propNames);
            Assert.Contains("NgayGui", propNames);

            // Hai thuộc tính AI gợi ý và xác nhận phải riêng biệt
            var aiProp = type.GetProperty("GiaTriAIGoiY");
            var confirmProp = type.GetProperty("GiaTriXacNhan");
            Assert.NotNull(aiProp);
            Assert.NotNull(confirmProp);
            Assert.NotEqual(aiProp, confirmProp);
        }

        [Fact]
        public void ApproveMeterPeriodsRequest_MustExist_AndOldSingleRequestMustBeRemoved()
        {
            // DTO đơn phòng cũ phải bị xóa
            var oldType = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs.ApproveMeterPeriodRequest");
            Assert.Null(oldType);

            // DTO batch mới và item phải tồn tại và không nhận ChiNhanhId từ client
            var type = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs.ApproveMeterPeriodsRequest");
            Assert.NotNull(type);

            var propNames = type.GetProperties().Select(p => p.Name).ToList();
            Assert.Contains("Thang", propNames);
            Assert.Contains("Nam", propNames);
            Assert.Contains("DanhSachPhong", propNames);
            Assert.DoesNotContain("ChiNhanhId", propNames);

            var itemType = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.DTOs.ApproveMeterPeriodItem");
            Assert.NotNull(itemType);
            var itemPropNames = itemType.GetProperties().Select(p => p.Name).ToList();
            Assert.Contains("PhongTroId", itemPropNames);
            Assert.Contains("DienMode", itemPropNames);
            Assert.Contains("NuocMode", itemPropNames);
            Assert.DoesNotContain("ChiNhanhId", itemPropNames);
        }

        [Fact]
        public void MeterOcrResult_MustValidateInputsCorrectly()
        {
            // 0 là hợp lệ
            var zeroResult = MeterOcrResult.Readable(0m, 0.9);
            Assert.Equal(MeterOcrStatus.Readable, zeroResult.Status);
            Assert.Equal(0m, zeroResult.SuggestedValue);
            Assert.Equal(0.9, zeroResult.Confidence);

            // Số dương scale <= 3 hợp lệ
            var validResult = MeterOcrResult.Readable(123.456m, 0.85);
            Assert.Equal(123.456m, validResult.SuggestedValue);

            // Âm bị từ chối
            Assert.Throws<ArgumentOutOfRangeException>(() => MeterOcrResult.Readable(-1m));

            // Scale > 3 bị từ chối
            Assert.Throws<ArgumentException>(() => MeterOcrResult.Readable(1.2345m));

            // Confidence ngoài 0..1 bị từ chối
            Assert.Throws<ArgumentOutOfRangeException>(() => MeterOcrResult.Readable(10m, -0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeterOcrResult.Readable(10m, 1.1));

            // Unreadable
            var unreadable = MeterOcrResult.Unreadable("Ảnh mờ");
            Assert.Equal(MeterOcrStatus.Unreadable, unreadable.Status);
            Assert.Null(unreadable.SuggestedValue);
            Assert.Equal("Ảnh mờ", unreadable.ErrorMessage);

            // Failed
            var failed = MeterOcrResult.Failed("Timeout khi kết nối AI");
            Assert.Equal(MeterOcrStatus.Failed, failed.Status);
            Assert.Null(failed.SuggestedValue);
            Assert.Equal("Timeout khi kết nối AI", failed.ErrorMessage);
            Assert.Throws<ArgumentException>(() => MeterOcrResult.Failed(" "));
        }

        [Fact]
        public void Interfaces_MustExist_InRespectiveNamespaces()
        {
            var storageIface = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IMeterImageStorageService");
            var ocrIface = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Abstractions.Services.IMeterOcrService");
            var storeIface = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Persistence.IMeterImageStore");
            var workflowIface = _appAssembly.GetType("QuanLyChoThuePhongTroWeb.Application.Features.DienNuocs.Services.IMeterReadingWorkflowService");

            Assert.NotNull(storageIface);
            Assert.NotNull(ocrIface);
            Assert.NotNull(storeIface);
            Assert.NotNull(workflowIface);
        }
    }
}
