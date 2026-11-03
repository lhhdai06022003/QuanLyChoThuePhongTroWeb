using System;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime.Tensors;
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

        [Fact]
        public void ParseYoloOutput_MatchesPerAnchorReference()
        {
            // Arrange: đầu ra [1, 17, 8400] ngẫu nhiên cố định, khoảng 1/4 anchor vượt ngưỡng 0,25.
            const int anchors = 8400;
            var output = new DenseTensor<float>(new[] { 1, 17, anchors });
            var rnd = new Random(11);
            for (int r = 0; r < 17; r++)
            {
                for (int i = 0; i < anchors; i++)
                {
                    output[0, r, i] = r < 4 ? (float)rnd.NextDouble() * 640f : (float)Math.Pow(rnd.NextDouble(), 60);
                }
            }

            // Act
            var detections = YoloMeterScannerService.ParseYoloOutput(output);

            // Assert: lớp điểm cao nhất (lấy lớp đầu khi bằng điểm), bỏ anchor dưới 0,25, giữ thứ tự anchor.
            var expected = new List<YoloMeterScannerService.BoundingBox>();
            for (int i = 0; i < anchors; i++)
            {
                float maxScore = 0f;
                int best = -1;
                for (int c = 0; c < 13; c++)
                {
                    if (output[0, 4 + c, i] > maxScore)
                    {
                        maxScore = output[0, 4 + c, i];
                        best = c;
                    }
                }

                if (maxScore >= 0.25f && best >= 0)
                {
                    expected.Add(new YoloMeterScannerService.BoundingBox(
                        best, maxScore, output[0, 0, i], output[0, 1, i], output[0, 2, i], output[0, 3, i]));
                }
            }

            Assert.InRange(expected.Count, 1000, anchors - 1000);
            Assert.Equal(expected, detections);
        }
    }
}
