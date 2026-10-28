using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models.ApiResponseModels
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string> Errors { get; set; } = new();

        public static ApiResponse<T> Ok(T data, string message = "Success") =>
            new() { Success = true, Message = message, Data = data };

        public static ApiResponse<T> Fail(string message, List<string>? errors = null) =>
            new() { Success = false, Message = message, Errors = errors ?? new() };

        public static ApiResponse<T> Fail(List<string> errors) =>
            new() { Success = false, Message = "Validation failed", Errors = errors };
    }

    // Non-generic version for responses with no data
    public class ApiResponse : ApiResponse<object>
    {
        public static ApiResponse OkNoData(string message = "Success") =>
            new() { Success = true, Message = message };
    }
}
