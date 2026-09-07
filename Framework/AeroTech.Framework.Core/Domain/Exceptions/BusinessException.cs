namespace AeroTech.Framework.Core.Domain.Exceptions
{
    public class BusinessException : Exception
    {
        public int Code { get; }

       
        public int HttpStatus { get; init; } = 400;

        public BusinessException(string message) : base(message)
        {
        }

        public BusinessException(int code, string message) : base(message)
        {
            Code = code;
        }

        public BusinessException(int code, string message, params object?[] args) : base(string.Format(message, args))
        {
            Code = code;
        }
    }
}
