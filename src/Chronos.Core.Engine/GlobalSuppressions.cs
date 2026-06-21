// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "<Pending>", Scope = "member", Target = "~M:Chronos.Core.Engine.Communication.CloudConnector.RunAsync(System.Threading.CancellationToken)~System.Threading.Tasks.Task")]

// Suppress CA1303 for all console output in CloudConnector – CLI output does not require localization.
[assembly: SuppressMessage("Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Console output for CLI; no localization required.", Scope = "type", Target = "Chronos.Core.Engine.Communication.CloudConnector")]
