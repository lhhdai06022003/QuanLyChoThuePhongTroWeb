using System;

namespace QuanLyChoThuePhongTroWeb.Application.Abstractions.Persistence
{
    // Infrastructure ném exception này khi SaveChanges vi phạm unique index, để Application
    // đổi thành lỗi nghiệp vụ mà không phụ thuộc EF Core hay Npgsql.
    public sealed class UniqueConstraintViolationException : Exception
    {
        public UniqueConstraintViolationException(string? constraintName, Exception innerException)
            : base($"Vi phạm ràng buộc duy nhất {constraintName}.", innerException)
        {
            ConstraintName = constraintName ?? string.Empty;
        }

        public string ConstraintName { get; }
    }
}
