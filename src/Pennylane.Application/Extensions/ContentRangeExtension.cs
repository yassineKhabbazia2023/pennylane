using System.Net.Http.Headers;

namespace Pennylane.Application.Extensions;

public static class ContentRangeExtension
{
    private static readonly char[] separator = ['-', '/'];

    public static ContentRangeHeaderValue GetContentRange(this HttpResponseMessage httpResponse)
    {
        int from = 0, to = 0, length = 0;

        if (httpResponse.Content.Headers.Contains("Content-Range"))
        {
            var rangeHeaderValue = httpResponse.Content.Headers.GetValues("Content-Range").FirstOrDefault();
            var rangeHeaderParams = rangeHeaderValue?.Split(separator);

            if (rangeHeaderParams.Length == 3)
            {
                from = int.TryParse(rangeHeaderParams[0]?[10..], out var fromValue) ? fromValue : 0;
                to = int.TryParse(rangeHeaderParams[1], out var toValue) ? toValue : 0;
                length = int.TryParse(rangeHeaderParams[2], out var lengthValue) ? lengthValue : to;
            }
        }

        return new ContentRangeHeaderValue(from, to, length);
    }
}