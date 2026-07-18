using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SamplePlugin;

public sealed class RecruitingWindow : Window
{
    private readonly RecruitingService service;
    private string name = "";
    private string world = "";
    private string feedback = "";

    public RecruitingWindow(RecruitingService service) : base("FC Recruiting Assistant")
    {
        this.service = service;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(720, 420),
            MaximumSize = new System.Numerics.Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public override void Draw()
    {
        ImGui.TextWrapped("Blank-FC players are highlighted green in FFXIV's native Player Search. Right-click the green native name and manually choose Invite to Free Company.");
        ImGui.TextWrapped("This assistant never searches, opens menus, sends tells, targets, or invites automatically.");
        ImGui.Separator();
        ImGui.InputText("Character", ref name, 64);
        ImGui.SameLine();
        ImGui.InputText("World", ref world, 32);
        ImGui.SameLine();
        if (ImGui.Button("Add candidate"))
        {
            var result = service.AddManual(name, world);
            feedback = result.Message;
            if (result.Added) { name = ""; world = ""; }
        }
        if (feedback.Length > 0) ImGui.TextWrapped(feedback);

        if (!ImGui.BeginTable("Candidates", 6,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY)) return;
        foreach (var heading in new[] { "Player", "No FC?", "Status", "Eligibility", "Outreach", "Outcome" })
        {
            ImGui.TableSetupColumn(heading);
        }
        ImGui.TableHeadersRow();

        foreach (var c in service.Candidates)
        {
            ImGui.PushID(c.Id.ToString());
            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.TextUnformatted($"{c.CharacterName}@{c.World}");
            ImGui.TableNextColumn();
            var confirmed = c.FcAbsenceManuallyConfirmed;
            if (ImGui.Checkbox("Confirmed", ref confirmed))
            {
                c.FcAbsenceManuallyConfirmed = confirmed;
                c.Status = confirmed ? CandidateStatus.Ready : CandidateStatus.NeedsReview;
                service.Save();
            }
            ImGui.TableNextColumn(); ImGui.TextUnformatted(c.Status.ToString());
            ImGui.TableNextColumn();
            var eligible = service.Check(c, DateTimeOffset.UtcNow);
            ImGui.TextWrapped(eligible.Reason);
            ImGui.TableNextColumn();
            ImGui.BeginDisabled(!eligible.Allowed);
            if (ImGui.Button("Copy /tell")) ImGui.SetClipboardText(service.PrepareTellCommand(c));
            ImGui.SameLine();
            if (ImGui.Button("I sent it")) service.MarkContacted(c, DateTimeOffset.UtcNow);
            ImGui.EndDisabled();
            ImGui.TableNextColumn();
            if (ImGui.Button("Declined")) { c.Status = CandidateStatus.Declined; service.Save(); }
            ImGui.SameLine();
            if (ImGui.Button("DNC")) { c.Status = CandidateStatus.DoNotContact; service.Save(); }
            ImGui.PopID();
        }
        ImGui.EndTable();
    }
}
