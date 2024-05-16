using SlowCow.Setup.Base.Models;
namespace SlowCow.Setup.Base.Interfaces;

public interface IUpdater
{
    SlowCowVersion? GetVersion();
    bool InstallLatest(bool force);
}
