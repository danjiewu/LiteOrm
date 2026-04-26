using System;

namespace LiteOrm.CodeGen;

public enum CodeGenDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed class CodeGenDiagnostic
{
    public CodeGenDiagnostic(CodeGenDiagnosticSeverity severity, string message, string? hint = null, string? sqlFragment = null)
    {
        Severity = severity;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Hint = hint;
        SqlFragment = sqlFragment;
    }

    public CodeGenDiagnosticSeverity Severity { get; }

    public string Message { get; }

    public string? Hint { get; }

    public string? SqlFragment { get; }
}
