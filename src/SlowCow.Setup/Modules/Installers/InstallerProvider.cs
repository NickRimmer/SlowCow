using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using SlowCow.Setup.Modules.Installers.Base;
namespace SlowCow.Setup.Modules.Installers;

internal class InstallerProvider
{
    private readonly IEnumerable<IInstaller> _installers;
    public InstallerProvider(IEnumerable<IInstaller> installers)
    {
        _installers = installers ?? throw new ArgumentNullException(nameof(installers));
    }

    public IInstaller GetInstaller()
    {
        if (OperatingSystem.IsWindows() && TryGetInstaller<WindowsInstaller>(out var windowsInstaller)) return windowsInstaller;

        // platform is not supported
        throw new NotSupportedException("Platform is not supported.");
    }

    private bool TryGetInstaller<T>([NotNullWhen(true)] out IInstaller? installer)
    {
        installer = _installers.FirstOrDefault(x=> x is T);
        return installer != null;
    }
}
