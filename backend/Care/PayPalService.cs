using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace MediVault.Api.Care;

public sealed class PayPalOptions
{
    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public string Mode { get; set; } = "sandbox";

    public string Currency { get; set; } = "EUR";
}

public sealed record PayPalCapture(string OrderId, string CaptureId, decimal Amount, string Currency, string? ReferenceId);

public sealed class PayPalService(
    IHttpClientFactory httpFactory,
    IConfiguration configuration,
    IMemoryCache cache,
    ILogger<PayPalService> logger)
{
    private readonly PayPalOptions _options = configuration.GetSection("PayPal").Get<PayPalOptions>() ?? new PayPalOptions();

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.ClientId) && !string.IsNullOrWhiteSpace(_options.ClientSecret);

    public string ClientId => _options.ClientId;

    public string Currency => string.IsNullOrWhiteSpace(_options.Currency) ? "EUR" : _options.Currency;

    public async Task<string> CreateOrderAsync(Guid requestId, decimal fee, CancellationToken cancellationToken)
    {
        var token = await TokenAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = requestId.ToString(),
                    custom_id = requestId.ToString(),
                    description = "Ask a doctor",
                    amount = new
                    {
                        currency_code = Currency,
                        value = fee.ToString("0.00", CultureInfo.InvariantCulture)
                    }
                }
            }
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl()}/v2/checkout/orders");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("PayPal-Request-Id", $"{requestId:N}-{Guid.NewGuid():N}");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await SendAsync(request, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("PayPal did not return an order id.");
    }

    public async Task RefundAsync(string captureId, decimal amount, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            amount = new
            {
                currency_code = Currency,
                value = amount.ToString("0.00", CultureInfo.InvariantCulture)
            }
        });
        var token = await TokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl()}/v2/payments/captures/{captureId}/refund");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await httpFactory.CreateClient("paypal").SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.IsSuccessStatusCode
            || body.Contains("CAPTURE_FULLY_REFUNDED", StringComparison.OrdinalIgnoreCase)
            || body.Contains("REFUND_ALREADY_DONE", StringComparison.OrdinalIgnoreCase))
        {
            response.Dispose();
            return;
        }

        logger.LogWarning("PayPal refund returned {Status}: {Body}", (int)response.StatusCode, body);
        response.Dispose();
        throw new InvalidOperationException("PayPal could not refund the payment.");
    }

    public async Task<string> PayoutAsync(Guid requestId, string receiverEmail, decimal amount, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new
        {
            sender_batch_header = new
            {
                sender_batch_id = $"opinion-{requestId:N}",
                email_subject = "Payment for an opinion",
                email_message = "MediVault sent your fee for a completed opinion."
            },
            items = new[]
            {
                new
                {
                    recipient_type = "EMAIL",
                    amount = new
                    {
                        value = amount.ToString("0.00", CultureInfo.InvariantCulture),
                        currency = Currency
                    },
                    receiver = receiverEmail,
                    note = "Ask a doctor",
                    sender_item_id = requestId.ToString("N")
                }
            }
        });
        var token = await TokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl()}/v1/payments/payouts");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await SendAsync(request, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.GetProperty("batch_header").GetProperty("payout_batch_id").GetString() ?? "";
    }

    public async Task<PayPalCapture> CaptureAsync(string orderId, CancellationToken cancellationToken)
    {
        var token = await TokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl()}/v2/checkout/orders/{orderId}/capture");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("PayPal-Request-Id", $"capture-{orderId}");
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        using var response = await SendAsync(request, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        var unit = root.GetProperty("purchase_units")[0];
        var capture = unit.GetProperty("payments").GetProperty("captures")[0];
        if (capture.GetProperty("status").GetString() != "COMPLETED")
        {
            throw new InvalidOperationException("PayPal did not complete the payment.");
        }

        var amount = capture.GetProperty("amount");
        var reference = unit.TryGetProperty("reference_id", out var referenceId) ? referenceId.GetString() : null;
        if (string.IsNullOrWhiteSpace(reference) && unit.TryGetProperty("custom_id", out var customId))
        {
            reference = customId.GetString();
        }

        return new PayPalCapture(
            root.GetProperty("id").GetString() ?? orderId,
            capture.GetProperty("id").GetString() ?? "",
            decimal.Parse(amount.GetProperty("value").GetString()!, CultureInfo.InvariantCulture),
            amount.GetProperty("currency_code").GetString() ?? "",
            reference);
    }

    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue("paypal-access-token", out string? cached) && !string.IsNullOrWhiteSpace(cached))
        {
            return cached;
        }

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl()}/v1/oauth2/token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" });
        using var response = await SendAsync(request, cancellationToken);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var token = document.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("PayPal did not return an access token.");
        var expires = document.RootElement.TryGetProperty("expires_in", out var seconds) ? seconds.GetInt32() : 300;
        cache.Set("paypal-access-token", token, TimeSpan.FromSeconds(Math.Max(30, expires - 60)));
        return token;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await httpFactory.CreateClient("paypal").SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("PayPal returned {Status}: {Body}", (int)response.StatusCode, body);
        response.Dispose();
        throw new InvalidOperationException("PayPal could not complete the payment.");
    }

    private string BaseUrl() =>
        _options.Mode.Equals("live", StringComparison.OrdinalIgnoreCase)
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";
}
