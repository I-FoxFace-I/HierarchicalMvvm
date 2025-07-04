using Microsoft.CodeAnalysis;

namespace HierarchicalMvvm.Generator;

public static class DiagnosticHelper
{
    public static readonly DiagnosticDescriptor InfoDescriptor = new(
        "HIER001",
        "Info",
        "{0}",
        "HierarchicalMvvmGenerator",
        DiagnosticSeverity.Info,
        true);

    public static readonly DiagnosticDescriptor WarningDescriptor = new(
        "HIER002", 
        "Warning",
        "{0}",
        "HierarchicalMvvmGenerator",
        DiagnosticSeverity.Warning,
        true);

    public static readonly DiagnosticDescriptor ErrorDescriptor = new(
        "HIER003",
        "Error", 
        "{0}",
        "HierarchicalMvvmGenerator",
        DiagnosticSeverity.Error,
        true);

    public static void LogInfo(SourceProductionContext context, string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(InfoDescriptor, Location.None, message));
    }

    public static void LogWarning(SourceProductionContext context, string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(WarningDescriptor, Location.None, message));
    }

    public static void LogError(SourceProductionContext context, string message)
    {
        context.ReportDiagnostic(Diagnostic.Create(ErrorDescriptor, Location.None, message));
    }

    public static void LogInfo(SourceProductionContext context, string format, params object[] args)
    {
        LogInfo(context, string.Format(format, args));
    }

    public static void LogWarning(SourceProductionContext context, string format, params object[] args)
    {
        LogWarning(context, string.Format(format, args));
    }

    public static void LogError(SourceProductionContext context, string format, params object[] args)
    {
        LogError(context, string.Format(format, args));
    }
} 