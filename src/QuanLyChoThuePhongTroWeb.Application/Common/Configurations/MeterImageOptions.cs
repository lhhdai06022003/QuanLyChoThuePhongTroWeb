using System.Collections.Generic;

namespace QuanLyChoThuePhongTroWeb.Application.Common.Configurations
{
    public class MeterImageOptions
    {
        public const string SectionName = "MeterImageOptions";

        public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024; // 5 MB

        public List<string> AllowedContentTypes { get; set; } = new()
        {
            "image/jpeg",
            "image/png",
            "image/webp"
        };

        public int DownloadTimeoutSeconds { get; set; } = 15;

        public int StaleProcessingMinutes { get; set; } = 10;

        public void Validate()
        {
            if (MaxFileSizeBytes <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình MeterImageOptions:MaxFileSizeBytes phải lớn hơn 0.");
            }

            if (DownloadTimeoutSeconds <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình MeterImageOptions:DownloadTimeoutSeconds phải lớn hơn 0.");
            }

            if (StaleProcessingMinutes <= 0)
            {
                throw new System.InvalidOperationException("Cấu hình MeterImageOptions:StaleProcessingMinutes phải lớn hơn 0.");
            }

            if (AllowedContentTypes == null || AllowedContentTypes.Count == 0)
            {
                throw new System.InvalidOperationException("Cấu hình MeterImageOptions:AllowedContentTypes không được để trống.");
            }

            AllowedContentTypes = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Distinct(AllowedContentTypes, System.StringComparer.OrdinalIgnoreCase));
        }
    }
}
