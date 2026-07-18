using System;
namespace SamplePlugin;

public enum CandidateStatus
{
    NeedsReview,
    Ready,
    Contacted,
    Interested,
    Invited,
    Joined,
    Declined,
    Ineligible,
    DoNotContact
}

public sealed class Candidate
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string CharacterName { get; set; } = "";
    public string World { get; set; } = "";
    public bool FcAbsenceManuallyConfirmed { get; set; }
    public CandidateStatus Status { get; set; } = CandidateStatus.NeedsReview;
    public DateTimeOffset? LastContactUtc { get; set; }
    public string Notes { get; set; } = "";

    public string Key => $"{CharacterName.Trim()}@{World.Trim()}".ToUpperInvariant();
}

public sealed class RecruitingConfig
{
    public int ContactCooldownDays { get; set; } = 30;
    public int SessionSoftCap { get; set; } = 10;
    public int DailySoftCap { get; set; } = 20;
    public string TellTemplate { get; set; } =
        "Hi {first}! I help run Geuno. We're a friendly FC with events, 24/7 EXP buffs, and free food/materials. Are you currently looking for an FC? No pressure either way!";
}

public sealed record Eligibility(bool Allowed, string Reason);

