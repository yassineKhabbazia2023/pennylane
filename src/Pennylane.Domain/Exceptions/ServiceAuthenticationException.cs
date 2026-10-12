using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Pennylane.Application.Exceptions;

[Serializable]
[ExcludeFromCodeCoverage]
public class ServiceAuthenticationException : Exception
{
    public string Content { get; init; } = string.Empty; // Ensure Content is initialized to avoid nullability issues

    public ServiceAuthenticationException()
    {
    }

    public ServiceAuthenticationException(string content) : base(content)
    {
        Content = content;
    }

    // Constructor required for deserialization
    protected ServiceAuthenticationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
        Content = info.GetString(nameof(Content)) ?? string.Empty; // Ensure Content is not null after deserialization
    }
}