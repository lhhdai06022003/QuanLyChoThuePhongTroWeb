using System;
using QuanLyChoThuePhongTroWeb.Infrastructure.ExternalServices.AiAssistants;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Infrastructure.IntegrationTests.ExternalServices
{
    public class YoloMeterScannerTensorTests
    {
        [Fact]
        public void PreprocessLetterbox_MatchesPerPixelReference_ForNonSquareImage()
        {
            // Arrange: ảnh 800x500 nhiễu cố định, thu về 640x400 và đệm xám 120 dòng trên/dưới.
            using var src = new Image<Rgb24>(800, 500);
            var rnd = new Random(7);
            src.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        row[x] = new Rgb24((byte)rnd.Next(256), (byte)rnd.Next(256), (byte)rnd.Next(256));
                    }
                }
            });

            // Act
            var tensor = YoloMeterScannerService.PreprocessLetterbox(src);

            // Assert: tensor [1, 3, 640, 640], từng kênh chia 255, đúng vị trí điểm ảnh của khung letterbox.
            Assert.Equal(new[] { 1, 3, 640, 640 }, tensor.Dimensions.ToArray());

            using var canvas = new Image<Rgb24>(640, 640, new Rgb24(114, 114, 114));
            using var resized = src.Clone(ctx => ctx.Resize(640, 400));
            canvas.Mutate(ctx => ctx.DrawImage(resized, new Point(0, 120), 1f));

            int mismatches = 0;
            canvas.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < 640; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < 640; x++)
                    {
                        var p = row[x];
                        if (tensor[0, 0, y, x] != p.R / 255f) mismatches++;
                        if (tensor[0, 1, y, x] != p.G / 255f) mismatches++;
                        if (tensor[0, 2, y, x] != p.B / 255f) mismatches++;
                    }
                }
            });

            Assert.Equal(0, mismatches);
            Assert.Equal(114 / 255f, tensor[0, 0, 0, 0]);
            Assert.Equal(114 / 255f, tensor[0, 2, 639, 639]);
        }
    }
}
