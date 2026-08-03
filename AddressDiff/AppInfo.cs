// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Noël Danjou

using System.Reflection;

namespace AddressDiff;

// Program identity for the startup banner. The values live in the project file
// (Product, Description, Copyright, Version, and a License assembly-metadata
// entry) and are read back from the compiled assembly, so there is a single
// source of truth.
static class AppInfo
{
    static readonly Assembly Asm = typeof(AppInfo).Assembly;

    public static string Name =>
        Attr<AssemblyProductAttribute>()?.Product ?? Asm.GetName().Name ?? "AddressDiff";

    public static string Description =>
        Attr<AssemblyDescriptionAttribute>()?.Description ?? "";

    public static string Copyright =>
        Attr<AssemblyCopyrightAttribute>()?.Copyright ?? "";

    public static string License =>
        Asm.GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "License")?.Value ?? "";

    // Prefer the informational version (supports semantic tags); drop the
    // "+<source-revision>" suffix the SDK appends in a git checkout.
    public static string Version
    {
        get
        {
            var info = Attr<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrEmpty(info))
                return Asm.GetName().Version?.ToString(3) ?? "0.0.0";
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
    }

    static T? Attr<T>() where T : Attribute => Asm.GetCustomAttribute<T>();

    public static void PrintBanner()
    {
        Console.WriteLine(
            $"{Name} Version {Version}\n" +
            $"{Description}\n" +
            $"{ Copyright} - {License}\n");
    }
}
