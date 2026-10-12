using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Pennylane.Application.Exceptions;
using Pennylane.Application.Extensions;
using Pennylane.Application.Interfaces;
using Pennylane.Application.Models;
using Pennylane.Application.Models.Pagers;
using Pennylane.Domain.ExternalApis.CommandParams;
using Pennylane.Domain.ExternalApis.Models.GroupAPI;
using Pulse.ExceptionMiddleware.Exceptions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;

namespace Pennylane.Application.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<(TResult?, ContentRangeHeaderValue?)> GetAndParseAsync<TResult>(this HttpClient httpClient, string uri) where TResult : new()
        {
            var response = await httpClient.GetAsync(uri);
            await HandleResponse(response);
            var result = await response.Content.ReadFromJsonAsync<TResult>();
            return (result, response.GetContentRange());
        }

        public static async Task<TResult?> PostJsonAndParseAsync<TData, TResult>(this HttpClient httpClient, string uri, TData data)
            where TData : new()
            where TResult : new()
        {
            var response = await httpClient.PostAsync(uri, JsonContent.Create(data));
            await HandleResponse(response);
            return await response.Content.ReadFromJsonAsync<TResult?>();
        }

        public static async Task<TResult?> PutJsonAndParseAsync<TData, TResult>(this HttpClient httpClient, string uri, TData data)
            where TData : new()
            where TResult : new()
        {
            var response = await httpClient.PutAsync(uri, JsonContent.Create(data));
            await HandleResponse(response);
            return await response.Content.ReadFromJsonAsync<TResult?>();
        }

        public static async Task DeleteWithValidationAsync(this HttpClient httpClient, string uri)
        {
            var response = await httpClient.DeleteAsync(uri);
            await HandleResponse(response);
        }
        private static async Task HandleResponse(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            throw response.StatusCode switch
            {
                HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => new ServiceAuthenticationException(content),
                HttpStatusCode.BadRequest => new BadRequestException(response.StatusCode.ToString(), content),
                HttpStatusCode.NotFound => new RequestHttpException(response.StatusCode, content),
                HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout => new RequestHttpException(response.StatusCode, content),
                _ => new RequestHttpException(response.StatusCode, content),
            };
        }

    }
}
