using System.Threading.Tasks;
using QuanLyChoThuePhongTroWeb.Application.Common.Models;
using QuanLyChoThuePhongTroWeb.Application.UnitTests.Fakes;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    // Từ spec thanh toán trực tuyến §8 khách không còn tạo lượt, hủy lượt hay nộp minh chứng; service chỉ còn đọc ảnh
    // minh chứng của các lượt cũ. Quy tắc tạo/hủy/nộp đã gỡ cùng các test của chúng.
    public class InvoicePaymentRequestServiceTests
    {
        private const int Tenant = PaymentTestHarness.TenantId;

        private readonly PaymentTestHarness _h = new();

        [Fact]
        public async Task ProofImage_OnlyOwnerCanRead()
        {
            _h.AddSentInvoice();
            var proof = _h.AddProof(_h.AddRequest());

            Assert.True((await _h.Requests.GetProofImageAsync(proof.MinhChungThanhToanHoaDonId, Tenant)).Success);
            Assert.Equal(ServiceErrorKind.NotFound, (await _h.Requests.GetProofImageAsync(proof.MinhChungThanhToanHoaDonId, 999)).ErrorKind);
        }

        [Fact]
        public async Task ProofImage_UnknownProof_ReturnsNotFound()
        {
            _h.AddSentInvoice();

            var result = await _h.Requests.GetProofImageAsync(4040, Tenant);

            Assert.Equal(ServiceErrorKind.NotFound, result.ErrorKind);
        }

        [Fact]
        public async Task ProofImage_WhenTheStorageFails_ReturnsAFriendlyError()
        {
            _h.AddSentInvoice();
            var proof = _h.AddProof(_h.AddRequest());
            _h.Storage.ThrowOnRead = true;

            var result = await _h.Requests.GetProofImageAsync(proof.MinhChungThanhToanHoaDonId, Tenant);

            Assert.False(result.Success);
            Assert.Equal("Không tải được ảnh minh chứng.", result.Message);
        }
    }
}
