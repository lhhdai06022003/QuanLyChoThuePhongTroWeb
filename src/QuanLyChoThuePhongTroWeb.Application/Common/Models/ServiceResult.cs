namespace QuanLyChoThuePhongTroWeb.Application.Common.Models
{
    // Loại lỗi để tầng Web chọn mã HTTP; Fail() mặc định là Validation nên code cũ không đổi.
    public enum ServiceErrorKind
    {
        Validation = 0,
        Forbidden = 1,
        NotFound = 2
    }

    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
        public ServiceErrorKind ErrorKind { get; set; }

        public static ServiceResult Ok(string message = "Thành công", object? data = null)
        {
            return new ServiceResult { Success = true, Message = message, Data = data };
        }

        public static ServiceResult Fail(string message)
        {
            return new ServiceResult { Success = false, Message = message };
        }

        public static ServiceResult Forbidden(string message)
        {
            return new ServiceResult { Success = false, Message = message, ErrorKind = ServiceErrorKind.Forbidden };
        }

        public static ServiceResult NotFound(string message)
        {
            return new ServiceResult { Success = false, Message = message, ErrorKind = ServiceErrorKind.NotFound };
        }

        // Chuyển kết quả dạng tuple của các service cũ sang ServiceResult (lỗi nghiệp vụ = Validation).
        public static ServiceResult FromTuple((bool IsSuccess, string? ErrorMessage) result)
        {
            return result.IsSuccess ? Ok() : Fail(result.ErrorMessage ?? "Có lỗi xảy ra.");
        }
    }

    public class ServiceResult<T> : ServiceResult
    {
        public new T? Data
        {
            get => base.Data is T typed ? typed : default;
            set => base.Data = value;
        }

        public static ServiceResult<T> Ok(T data, string message = "Thành công")
        {
            return new ServiceResult<T> { Success = true, Message = message, Data = data };
        }

        public static new ServiceResult<T> Fail(string message)
        {
            return new ServiceResult<T> { Success = false, Message = message, Data = default };
        }

        public static new ServiceResult<T> Forbidden(string message)
        {
            return new ServiceResult<T> { Success = false, Message = message, ErrorKind = ServiceErrorKind.Forbidden };
        }

        public static new ServiceResult<T> NotFound(string message)
        {
            return new ServiceResult<T> { Success = false, Message = message, ErrorKind = ServiceErrorKind.NotFound };
        }

        // Chuyển một kết quả thất bại sang kiểu khác, giữ nguyên thông báo và loại lỗi.
        public static ServiceResult<T> FailFrom(ServiceResult failure)
        {
            return new ServiceResult<T> { Success = false, Message = failure.Message, ErrorKind = failure.ErrorKind };
        }
    }
}
