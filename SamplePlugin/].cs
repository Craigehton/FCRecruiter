using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SamplePlugin;

public sealed class RecruitingService
{
    private readonly string dataPath;
    private readonly RecruitingConfig config;
    private readonly List<Candidate> candidates = [];
    private int sessionContacts;

    public RecruitingService(string configDirectory, RecruitingConfig config)
    {
        Directory.CreateDirectory(configDirectory);
        dataPath = Path.Combine(configDirectory, "recruiting-candidates.json");
        this.config = config;
        Load();
    }

    public IReadOnlyList<Candidate> Candidates => candidates;

    public (bool Added, string Message) AddManual(string characterName, string world)
    {
        var candidate = new Candidate { CharacterName = characterName.Trim(), World = world.Trim() };
        if (candidate.CharacterName.Length == 0 || candidate.World.Length == 0)
            return (false, "Enter both a character name and world.");
        if (candidates.Any(x => x.Key == candidate.Key))
            return (false, "That character is already in the queue/history.");

        candidates.Add(candidate);
        Save();
        return (true, "Added for manual review.");
    }

    public int AddCaptured(IEnumerable<PlayerSearchResult> results)
    {
        var added = 0;
        foreach (var result in results.Where(x => string.IsNullOrWhiteSpace(x.FreeCompany)))
        {
            var candidate = new Candidate
            {
                CharacterName = result.CharacterName.Trim(),
                World = result.World.Trim(),
                FcAbsenceManuallyConfirmed = true,
                Status = CandidateStatus.Ready,
                Notes = "Captured from the visible Player Search results; re-check before outreach."
            };
            if (candidate.CharacterName.Length == 0 || candidate.World.Length == 0) continue;
            if (candidates.Any(x => x.Key == candidate.Key)) continue;
            candidates.Add(candidate);
            added++;
        }
        if (added > 0) Save();
        return added;
    }

    public Eligibility Check(Candidate candidate, DateTimeOffset now)
    {
        if (candidate.Status is CandidateStatus.DoNotContact or CandidateStatus.Joined or CandidateStatus.Declined)
            return new(false, $"Status is {candidate.Status}.");
        if (!candidate.FcAbsenceManuallyConfirmed)
            return new(false, "Re-check and manually confirm the player has no FC.");
        if (candidate.LastContactUtc is { } last && last.AddDays(config.ContactCooldownDays) > now)
            return new(false, $"Cooldown ends {last.AddDays(config.ContactCooldownDays):d}.");
        if (sessionContacts >= config.SessionSoftCap)
            return new(false, "Session soft cap reached.");
        var today = candidates.Count(x => x.LastContactUtc?.UtcDateTime.Date == now.UtcDateTime.Date);
        if (today >= config.DailySoftCap)
            return new(false, "Daily soft cap reached.");
        return new(true, "Ready for one manually sent, personalized tell.");
    }

    public string PrepareTellCommand(Candidate candidate)
    {
        var first = candidate.CharacterName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                    ?? candidate.CharacterName;
        var message = config.TellTemplate.Replace("{first}", first, StringComparison.OrdinalIgnoreCase);
        return $"/tell {candidate.CharacterName}@{candidate.World} {message}";
    }

    public void MarkContacted(Candidate candidate, DateTimeOffset now)
    {
        var eligibility = Check(candidate, now);
        if (!eligibility.Allowed) throw new InvalidOperationException(eligibility.Reason);
        candidate.LastContactUtc = now;
        candidate.Status = CandidateStatus.Contacted;
        sessionContacts++;
        Save();
    }

    public void Save() => File.WriteAllText(dataPath,
        JsonSerializer.Serialize(candidates, new JsonSerializerOptions { WriteIndented = true }));

    private void Load()
    {
        if (!File.Exists(dataPath)) return;
        var loaded = JsonSerializer.Deserialize<List<Candidate>>(File.ReadAllText(dataPath));
        if (loaded is not null) candidates.AddRange(loaded);
    }
}
