using QuanLyChoThuePhongTroWeb.Application.Features.HopDongs.Services;
using Xunit;

namespace QuanLyChoThuePhongTroWeb.Application.UnitTests
{
    public class HopDongCodeChainTests
    {
        [Theory]
        [InlineData("HD-CN-2510-001", "HD-CN-2510-001", false, 0, "Mới")]
        [InlineData("HD-CN-2510-001-GH1", "HD-CN-2510-001", true, 1, "Gia hạn lần 1")]
        [InlineData("HD-CN-2510-001-GH12", "HD-CN-2510-001", true, 12, "Gia hạn lần 12")]
        public void Parses_RootAndIndex(string ma, string goc, bool isRenewal, int index, string loai)
        {
            Assert.Equal(goc, HopDongCodeChain.RootOf(ma));
            Assert.Equal(isRenewal, HopDongCodeChain.IsRenewal(ma));
            Assert.Equal(index, HopDongCodeChain.RenewalIndex(ma));
            Assert.Equal(loai, HopDongCodeChain.LoaiText(ma));
        }

        [Theory]
        [InlineData("HD-GH-2510-001")]
        [InlineData("HD-CN-2510-001-GH0")]
        [InlineData("HD-CN-2510-001-GHx")]
        [InlineData("HD-CN-2510-001-GH")]
        [InlineData("HD-CN-2510-001-GH01")]
        public void NotARenewal_WhenBranchCodeContainsGhOrSuffixIsMalformed(string ma)
        {
            Assert.False(HopDongCodeChain.IsRenewal(ma));
            Assert.Equal(ma, HopDongCodeChain.RootOf(ma));
            Assert.Equal(0, HopDongCodeChain.RenewalIndex(ma));
        }

        [Fact]
        public void BuildRenewal_AppendsSuffix_AndRejectsZero()
        {
            Assert.Equal("HD-X-001-GH3", HopDongCodeChain.BuildRenewal("HD-X-001", 3));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HopDongCodeChain.BuildRenewal("HD-X-001", 0));
        }

        [Fact]
        public void NestedSuffix_TreatsLastOneAsRenewal()
        {
            Assert.Equal("HD-X-001-GH1", HopDongCodeChain.RootOf("HD-X-001-GH1-GH2"));
            Assert.Equal(2, HopDongCodeChain.RenewalIndex("HD-X-001-GH1-GH2"));
        }

        [Fact]
        public void BelongsTo_ComparesRoots_NotPrefixes()
        {
            Assert.True(HopDongCodeChain.BelongsTo("HD-X-001", "HD-X-001"));
            Assert.True(HopDongCodeChain.BelongsTo("HD-X-001-GH2", "HD-X-001"));
            Assert.False(HopDongCodeChain.BelongsTo("HD-X-0010", "HD-X-001"));
            Assert.False(HopDongCodeChain.BelongsTo("HD-X-0010-GH1", "HD-X-001"));
        }
    }
}
