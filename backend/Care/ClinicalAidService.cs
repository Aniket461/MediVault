using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MediVault.Api.Models;

namespace MediVault.Api.Care;

public sealed class OpenAIOptions
{
    public string ApiKey { get; set; } = "";

    public string Model { get; set; } = "gpt-4o-mini";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
}

public sealed record AidAttachment(string FileName, string ContentType, string Description, byte[] Content);

public sealed class ClinicalAidService(IHttpClientFactory httpFactory, IConfiguration configuration, ILogger<ClinicalAidService> logger)
{
    private readonly OpenAIOptions _options = configuration.GetSection("OpenAI").Get<OpenAIOptions>() ?? new OpenAIOptions();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<ClinicalAidData?> WriteAsync(
        OpinionRequest request,
        IReadOnlyList<AidAttachment> attachments,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var intake = ClinicalIntakeData.Read(request.Intake);
        var attached = attachments
            .Select(file => file.FileName + "\n" + file.Description)
            .ToHashSet(StringComparer.Ordinal);
        var files = request.Files
            .Select(file =>
            {
                var key = file.Record.FileName + "\n" + file.Record.Description;
                var mark = attached.Contains(key) ? "attached" : "not attached";
                return $"{file.Record.FileName}: {file.Record.Description} ({mark})";
            })
            .ToList();
        var user = BuildCase(request.Message, intake, files);
        var payload = JsonSerializer.Serialize(new
        {
            model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4o-mini" : _options.Model,
            temperature = 0.2,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = """
                        You prepare a reading for a doctor who is about to review a second-opinion request.
                        Use only the patient's own words, the symptoms they selected, the file names and descriptions, and the image or PDF files attached with the case.
                        Mention only what you can actually see in an attached file.
                        If a file is named but not attached, do not guess what is inside it.
                        Do not invent test results, measurements, history, or symptoms.
                        Do not prescribe or recommend treatment.
                        You may list short questions the doctor may want to keep in mind while reading the chart.
                        Do not mention money, fees, or payment.
                        Write in plain English.
                        Respond with JSON only, in this shape:
                        {"overview":"","reported":"","earlierCare":"","impression":"","reviewPoints":[""]}
                        overview is two to four sentences on why they asked, in their terms.
                        reported covers the symptoms, how strong it feels, how long, their description, and what the attached files show.
                        earlierCare covers whether they have already asked someone, and what they were told.
                        impression is the model's own possible reading, three to five sentences.
                        In impression, reason from what they actually said and from what is visible in the files, then name two or three conditions that could fit, each as a possibility and not as a fact.
                        Say that nothing in the request confirms them. Do not state a condition as theirs.
                        reviewPoints is three to five short prompts, each grounded in something they actually said or something visible in a file.
                        """
                },
                new { role = "user", content = UserContent(user, attachments) }
            }
        });

        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://api.openai.com/v1" : _options.BaseUrl.TrimEnd('/');
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await httpFactory.CreateClient("openai").SendAsync(httpRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Clinical reading returned {Status}", (int)response.StatusCode);
            throw new InvalidOperationException("The clinical reading could not be prepared.");
        }

        using var document = JsonDocument.Parse(body);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();
        var aid = ClinicalAidData.Read(content);
        if (aid is null)
        {
            throw new InvalidOperationException("The clinical reading could not be prepared.");
        }

        return aid;
    }

    private static object UserContent(string text, IReadOnlyList<AidAttachment> attachments)
    {
        if (attachments.Count == 0)
        {
            return text;
        }

        var content = new List<object> { new { type = "text", text } };
        foreach (var file in attachments)
        {
            var data = Convert.ToBase64String(file.Content);
            if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                content.Add(new
                {
                    type = "image_url",
                    image_url = new { url = $"data:{file.ContentType};base64,{data}" }
                });
                continue;
            }

            var name = string.IsNullOrWhiteSpace(file.FileName) ? "record.pdf" : file.FileName;
            content.Add(new
            {
                type = "file",
                file = new { filename = name, file_data = $"data:{file.ContentType};base64,{data}" }
            });
        }

        return content;
    }

    private static string BuildCase(string message, ClinicalIntakeData? intake, IReadOnlyList<string> files)
    {
        var builder = new StringBuilder();
        builder.AppendLine("What the patient wants the doctor to know:");
        builder.AppendLine(string.IsNullOrWhiteSpace(message) ? "Not given." : message.Trim());
        if (intake is null)
        {
            builder.AppendLine();
            builder.AppendLine("The patient did not fill in a symptom history.");
        }
        else
        {
            builder.AppendLine();
            builder.AppendLine("Symptoms they selected: " + string.Join(", ", intake.Symptoms));
            builder.AppendLine("How it feels, in their words: " + intake.Feeling);
            builder.AppendLine("How long: " + intake.Duration);
            builder.AppendLine("How strong it feels to them: " + intake.Intensity);
            builder.AppendLine("Asked someone before: " + intake.Consulted);
            if (!string.IsNullOrWhiteSpace(intake.ConsultedNote))
            {
                builder.AppendLine("What they were told: " + intake.ConsultedNote);
            }
        }

        builder.AppendLine();
        builder.AppendLine(files.Count == 0
            ? "Files shared: none."
            : "Files shared:\n" + string.Join("\n", files));
        return builder.ToString();
    }
}
