using System.Diagnostics.CodeAnalysis;

namespace Pennylane.Application.Exceptions;

[Serializable]
[ExcludeFromCodeCoverage]
public class RequestHttpException : HttpRequestException
{
    public System.Net.HttpStatusCode HttpCode { get; }

    public RequestHttpException(System.Net.HttpStatusCode code) : this(code, null, null)
    {
    }

    public RequestHttpException(System.Net.HttpStatusCode code, string message) : this(code, message, null)
    {
    }

    public RequestHttpException(System.Net.HttpStatusCode code, string message, Exception inner) : base(message, inner)
    {
        HttpCode = code;
    }
}