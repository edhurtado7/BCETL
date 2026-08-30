using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using BCETL.Authentication;
using BCETL.Configuration;

namespace BCETL.BusinessCentral;

public sealed class BusinessCentralClient
{
    private readonly HttpClient _http;
    private readonly OAuthTokenService _tokens;
    private readonly BusinessCentralOptions _options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public BusinessCentralClient(BusinessCentralOptions options, OAuthTokenService tokens)
    {
        _options = options; _tokens = tokens; _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
    }

    public IAsyncEnumerable<BcCustomer> ReadCustomersAsync(DateTime? from, CancellationToken ct) => ReadEntityAsync<BcCustomer>(_options.CustomerEntitySet, from, ct);
    public IAsyncEnumerable<BcSalesInvoiceHeader> ReadSalesInvoiceHeadersAsync(DateTime? from, CancellationToken ct) => ReadEntityAsync<BcSalesInvoiceHeader>(_options.SalesInvoiceHeaderEntitySet, from, ct);
    public IAsyncEnumerable<BcSalesInvoiceLine> ReadSalesInvoiceLinesAsync(DateTime? from, CancellationToken ct) => ReadEntityAsync<BcSalesInvoiceLine>(_options.SalesInvoiceLineEntitySet, from, ct);
    public IAsyncEnumerable<BcSalesHeader> ReadSalesHeadersAsync(DateTime? from, CancellationToken ct) => ReadEntityAsync<BcSalesHeader>(_options.SalesHeaderEntitySet, from, ct);
    public IAsyncEnumerable<BcSalesLine> ReadSalesLinesAsync(DateTime? from, CancellationToken ct) => ReadEntityAsync<BcSalesLine>(_options.SalesLineEntitySet, from, ct);

    private async IAsyncEnumerable<T> ReadEntityAsync<T>(string entitySet, DateTime? from, [EnumeratorCancellation] CancellationToken ct)
    {
        string url = BuildUrl(entitySet, from);
        while (!string.IsNullOrWhiteSpace(url))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetAccessTokenAsync(ct));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            string body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode) throw new HttpRequestException($"BC API failed: {(int)response.StatusCode} {response.ReasonPhrase}. URL={url}. Body={body}");
            var page = JsonSerializer.Deserialize<ODataResponse<T>>(body, JsonOptions) ?? throw new InvalidOperationException("Unreadable BC OData response.");
            foreach (var item in page.Value) yield return item;
            url = page.NextLink ?? "";
        }
    }

    private string BuildUrl(string entitySet, DateTime? from)
    {
        string url = $"{_options.ApiRoot}/companies({_options.CompanyId})/{entitySet}";
        var query = new List<string>();
        if (from.HasValue)
        {
            string stamp = from.Value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
            query.Add("$filter=" + Uri.EscapeDataString($"systemModifiedAt ge {stamp}"));
        }
        query.Add("$orderby=" + Uri.EscapeDataString("systemModifiedAt asc,systemId asc"));
        return url + "?" + string.Join("&", query);
    }
}
