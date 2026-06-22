using System.IO.Compression;
using System.Net.Http.Headers;

namespace MlNFL.Downloader.Services;

internal sealed class NflverseClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public NflverseClient(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30),
            };
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
        }

        _httpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("MlNFL.Downloader", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("*/*"));
    }

    public async Task<Stream> OpenCsvStreamAsync(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        if (url.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            return new GZipStream(stream, CompressionMode.Decompress, leaveOpen: false);
        }

        return stream;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
