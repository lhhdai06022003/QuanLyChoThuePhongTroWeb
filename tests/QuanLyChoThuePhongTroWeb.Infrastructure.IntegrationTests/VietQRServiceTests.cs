using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.DocumentExporters;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests
{
    public class VietQRServiceTests
    {
        private readonly VietQRService _vietQRService;

        public VietQRServiceTests()
        {
            _vietQRService = new VietQRService();
        }

        [Fact]
        public void GenerateVietQRString_ShouldReturnValidPayload_WhenParametersAreValid()
        {
            // Arrange
            string bankId = "MB";
            string accountNumber = "0123456789";
            decimal amount = 1500000m;
            string memo = "THANH TOAN HD001";

            // Act
            string qrString = _vietQRService.GenerateVietQRString(bankId, accountNumber, amount, memo);

            // Assert
            Assert.NotNull(qrString);
            Assert.StartsWith("000201", qrString);
            Assert.Contains("970422", qrString);
            Assert.Contains("0123456789", qrString);
            Assert.Contains("1500000", qrString);
        }

        [Fact]
        public void GenerateQRCodePNGBytes_ShouldReturnValidPngHeader()
        {
            // Arrange
            string payload = "00020101021238540014A0000000720011011800069704220108123456780208QRIBFTTA5303704540710000005802VN62180814THANH TOAN HD16304ABCD";

            // Act
            byte[] bytes = _vietQRService.GenerateQRCodePNGBytes(payload);

            // Assert
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
            Assert.Equal(0x89, bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'N', bytes[2]);
            Assert.Equal((byte)'G', bytes[3]);
        }
    }
}
