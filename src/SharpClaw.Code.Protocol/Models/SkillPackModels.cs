using SharpClaw.Code.Protocol.Enums;

namespace SharpClaw.Code.Protocol.Models;

/// <summary>
/// A named command exposed by a skill pack.
/// </summary>
public sealed record SkillCommand(
    string Name,
    string Description,
    string PromptTemplate);

/// <summary>
/// A checklist packaged with a reusable skill workflow.
/// </summary>
public sealed record SkillChecklist(
    string Name,
    IReadOnlyList<string> Items);

/// <summary>
/// A named prompt template packaged with a skill pack.
/// </summary>
public sealed record SkillPromptTemplate(
    string Name,
    string Description,
    string Template);

/// <summary>
/// A tool recommendation surfaced by a skill pack.
/// </summary>
public sealed record SkillToolRecommendation(
    string ToolName,
    string Reason,
    bool Required = false);

/// <summary>
/// Skill pack manifest contract.
/// </summary>
public sealed record SkillPackManifest(
    string Id,
    string Name,
    string Version,
    string Description,
    string? Author,
    string[]? Tags,
    IReadOnlyList<SkillCommand>? Commands,
    IReadOnlyList<SkillPromptTemplate>? Prompts,
    IReadOnlyList<SkillChecklist>? Checklists,
    IReadOnlyList<SkillToolRecommendation>? RecommendedTools,
    IReadOnlyList<ApprovalScope>? RequiredPermissions,
    IReadOnlyList<PrimaryMode>? CompatibleModes,
    string EntryPointPrompt);

/// <summary>
/// Resolved skill pack with source and enabled state.
/// </summary>
public sealed record SkillPack(
    SkillPackManifest Manifest,
    string Source,
    bool IsBuiltIn,
    bool Enabled,
    DateTimeOffset? InstalledAtUtc = null);

/// <summary>
/// Skill-pack install request.
/// </summary>
public sealed record SkillPackInstallRequest(
    string SourcePath,
    bool EnableAfterInstall = true);

/// <summary>
/// Request to invoke a skill pack through the normal runtime.
/// </summary>
public sealed record SkillPackRunRequest(
    string SkillId,
    string? Arguments,
    PrimaryMode? PrimaryMode = null,
    string? CommandName = null);

/// <summary>
/// Configures skill-pack behavior.
/// </summary>
public sealed record SkillPacksConfig(
    bool Enabled = true,
    string[]? AdditionalRoots = null);

/// <summary>
/// Skill-pack inspection payload.
/// </summary>
public sealed record SkillPackInspectionRecord(
    SkillPack Pack,
    string ExpandedEntryPrompt);
