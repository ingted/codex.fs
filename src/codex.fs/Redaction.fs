namespace CodexFs

open System
open System.Text.RegularExpressions

/// Deterministic text redaction primitives for display logs and MessageFabric replies.
module Redaction =

    /// Severity of one redaction rule match.
    type RedactionSeverity =
        /// Informational match that may still be useful for diagnostics.
        | Info
        /// High-risk match that should be treated as a secret-like value.
        | High

    /// Regex-based redaction rule.
    type RedactionRule =
        { /// Stable rule name.
          Name: string
          /// Regular expression pattern used to find sensitive text.
          Pattern: string
          /// Replacement text used in the redacted output.
          Replacement: string
          /// Severity assigned to every hit produced by this rule.
          Severity: RedactionSeverity }

    /// One redaction hit in the pre-replacement text for the rule pass that found it.
    type RedactionHit =
        { /// Rule that produced the hit.
          RuleName: string
          /// Severity assigned by the rule.
          Severity: RedactionSeverity
          /// Start index in the text seen by the rule.
          Index: int
          /// Matched length.
          Length: int }

    /// Redacted text plus traceable hit metadata.
    type RedactionResult =
        { /// Text after all redaction rules have been applied.
          Text: string
          /// Redaction hits collected while applying rules.
          Hits: RedactionHit list }

    /// Replacement used by built-in high-risk rules.
    let highRiskReplacement = "[REDACTED]"

    /// Default high-risk token-like redaction rules.
    let defaultHighRiskRules =
        [ { Name = "github-token"
            Pattern = @"ghp_[A-Za-z0-9_]{20,}|github_pat_[A-Za-z0-9_]+"
            Replacement = highRiskReplacement
            Severity = High }
          { Name = "openai-like-api-key"
            Pattern = @"sk-[A-Za-z0-9]{20,}"
            Replacement = highRiskReplacement
            Severity = High }
          { Name = "aws-access-key"
            Pattern = @"AKIA[0-9A-Z]{16}"
            Replacement = highRiskReplacement
            Severity = High }
          { Name = "private-key-header"
            Pattern = @"BEGIN (RSA|OPENSSH|EC|DSA) PRIVATE KEY"
            Replacement = highRiskReplacement
            Severity = High } ]

    /// Builds a regex for a redaction rule with a bounded evaluation timeout.
    let createRegex rule =
        Regex(rule.Pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds 2.0)

    /// Applies one redaction rule to text.
    let redactWithRule (rule: RedactionRule) (text: string) =
        if isNull text then
            nullArg (nameof text)

        let regex = createRegex rule

        let hits =
            regex.Matches text
            |> Seq.cast<Match>
            |> Seq.map (fun m ->
                { RuleName = rule.Name
                  Severity = rule.Severity
                  Index = m.Index
                  Length = m.Length })
            |> Seq.toList

        let redacted = regex.Replace(text, rule.Replacement)
        { Text = redacted; Hits = hits }

    /// Applies redaction rules in order.
    let redact (rules: RedactionRule list) (text: string) =
        if isNull text then
            nullArg (nameof text)

        let folder (currentText, allHits) rule =
            let result = redactWithRule rule currentText
            result.Text, allHits @ result.Hits

        let redactedText, hits = rules |> List.fold folder (text, [])
        { Text = redactedText; Hits = hits }

    /// Applies built-in high-risk token-like redaction rules.
    let redactHighRisk text = redact defaultHighRiskRules text
