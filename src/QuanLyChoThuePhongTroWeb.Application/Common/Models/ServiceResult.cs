namespace QuanLyChoThuePhongTroWeb.Application.Common.Models
{
    public class ServiceResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }

        public static ServiceResult Ok(string message = "Thành công", object? data = null)
        {
            return new ServiceResult { Success = true, Message = message, Data = data };
        }

        public static ServiceResult Fail(string message)
        {
            return new ServiceResult { Success = false, Message = message };
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
    }
}
