using System.Text.Json;

namespace MediVault.Api.Care;

public sealed class ClinicalIntakeData
{
    public List<string> Symptoms { get; set; } = [];

    public string Feeling { get; set; } = "";

    public string Duration { get; set; } = "";

    public string Intensity { get; set; } = "";

    public string Consulted { get; set; } = "";

    public string ConsultedNote { get; set; } = "";

    public static readonly string[] SymptomsCatalog =
    [
        "Chest pain",
        "Shortness of breath",
        "Cough",
        "Fever",
        "Headache",
        "Dizziness",
        "Fatigue",
        "Nausea",
        "Abdominal pain",
        "Back pain",
        "Joint pain",
        "Rash",
        "Swelling",
        "Palpitations",
        "Weight change",
        "Sleep trouble",
        "Low mood",
        "Numbness or tingling",
        "Vision change",
        "Other"
    ];

    public static readonly string[] Durations =
    [
        "A few days",
        "One to two weeks",
        "Several weeks",
        "A few months",
        "More than a year",
        "Not sure"
    ];

    public static readonly string[] Intensities =
    [
        "Mild",
        "Moderate",
        "Severe",
        "It comes and goes"
    ];

    public static readonly string[] Consults =
    [
        "No",
        "Yes, a general doctor",
        "Yes, a specialist",
        "Yes, urgent care or a hospital"
    ];

    public static string? Validate(ClinicalIntakeData intake)
    {
        var symptoms = intake.Symptoms
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (symptoms.Count is < 1 or > 12 || symptoms.Any(item => !SymptomsCatalog.Contains(item)))
        {
            return "Select the symptoms that fit.";
        }

        intake.Feeling = intake.Feeling.Trim();
        intake.Duration = intake.Duration.Trim();
        intake.Intensity = intake.Intensity.Trim();
        intake.Consulted = intake.Consulted.Trim();
        intake.ConsultedNote = intake.ConsultedNote.Trim();
        intake.Symptoms = symptoms;

        if (intake.Feeling.Length is < 1 or > 2000)
        {
            return "Describe how this feels, in your own words.";
        }

        if (!Durations.Contains(intake.Duration))
        {
            return "Say how long this has been going on.";
        }

        if (!Intensities.Contains(intake.Intensity))
        {
            return "Say how strong this feels.";
        }

        if (!Consults.Contains(intake.Consulted))
        {
            return "Say whether you have asked someone about this before.";
        }

        if (intake.Consulted == "No")
        {
            intake.ConsultedNote = "";
        }
        else if (intake.ConsultedNote.Length is < 1 or > 2000)
        {
            return "Write what you were told.";
        }

        return null;
    }

    public static ClinicalIntakeData? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ClinicalIntakeData>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string Write() => JsonSerializer.Serialize(this, JsonOptions);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

public sealed class ClinicalAidData
{
    public string Overview { get; set; } = "";

    public string Reported { get; set; } = "";

    public string EarlierCare { get; set; } = "";

    public string Impression { get; set; } = "";

    public List<string> ReviewPoints { get; set; } = [];

    public static ClinicalAidData? Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var aid = JsonSerializer.Deserialize<ClinicalAidData>(json, ClinicalIntakeData.JsonOptions);
            if (aid is null || string.IsNullOrWhiteSpace(aid.Overview))
            {
                return null;
            }

            aid.Impression = (aid.Impression ?? "").Trim();
            aid.ReviewPoints = aid.ReviewPoints
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Take(6)
                .ToList();
            return aid;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public string Write() => JsonSerializer.Serialize(this, ClinicalIntakeData.JsonOptions);
}
