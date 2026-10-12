using System.Net.Http.Headers;
using System.Text.Json;
using Pennylane.Application.Extensions;
using Moq;
using Moq.Language.Flow;
using Moq.Protected;
using System.Net;

namespace Pennylane.Common.Tests;

public static class HttpMessageHandlerMocker
{
    public static ISetup<HttpMessageHandler, Task<HttpResponseMessage>> SetupSendAsync(
        this Mock<HttpMessageHandler> handler
    ) => handler.Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        );

    private static IReturnsResult<HttpMessageHandler> ReturnsHttpResponseAsync<TResult>(
        this ISetup<HttpMessageHandler, Task<HttpResponseMessage>> moqSetup,
        TResult responseBody,
        HttpStatusCode responseCode
    )
    {
        string serializedResponse;

        // If the expected return type is a primitive or string, just trim quotes
        if (typeof(TResult).IsPrimitive || typeof(TResult) == typeof(string))
        {
            serializedResponse = responseBody?.ToString()?.Trim('\"') ?? string.Empty;
        }
        else
        {
            // *** Use System.Text.Json with the same write options your real handler uses ***
            serializedResponse = JsonSerializer.Serialize(
                responseBody,
                SerializerExtensions.s_writeOptions
            );
        }

        var stringContent = new StringContent(serializedResponse);
        // Ensure the content‐type is JSON, so GetAsync<T> knows how to read it
        stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var responseMessage = new HttpResponseMessage
        {
            StatusCode = responseCode,
            Content = stringContent
        };

        return moqSetup.ReturnsAsync(responseMessage);
    }

    private static HttpClient GetFakeHttpClient(this HttpMessageHandler httpMessageHandler, string baseAddress)
        => new(httpMessageHandler)
        {
            BaseAddress = new Uri(baseAddress)
        };

    public static HttpClient MockHttpClient<TResult>(
        this Mock<HttpMessageHandler> httpMessageHandler,
        string baseAddress,
        TResult responseBody,
        HttpStatusCode responseCode
    )
    {
        httpMessageHandler
            .SetupSendAsync()
            .ReturnsHttpResponseAsync(responseBody, responseCode);

        return httpMessageHandler.Object.GetFakeHttpClient(baseAddress);
    }
    public static HttpClient MockHttpClientForPost<TResult>(
        this Mock<HttpMessageHandler> httpMessageHandler,
        string baseAddress,
        TResult responseBody,
        HttpStatusCode responseCode
    )
    {
        httpMessageHandler
            .SetupSendAsync()
            .ReturnsHttpResponseAsync(responseBody, responseCode);

        return httpMessageHandler.Object.GetFakeHttpClient(baseAddress);
    }

}