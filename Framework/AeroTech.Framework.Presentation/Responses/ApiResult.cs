namespace AeroTech.Framework.Presentation.Responses
{
    public class ApiResult
    {
        public ApiErrorItem[]? Errors { get; set; }
    }

    public sealed class ApiResult<T> : ApiResult
    {
        public ApiResult()
        {
        }

        public ApiResult(T data) => Data = data;

        public T? Data { get; set; }
    }

    public sealed class ApiErrorItem
    {
        public int? Code { get; set; }

        public string? Title { get; set; }

        public string? Detail { get; set; }

        public IReadOnlyDictionary<string, string[]>? Metadata { get; set; }
    }
}
