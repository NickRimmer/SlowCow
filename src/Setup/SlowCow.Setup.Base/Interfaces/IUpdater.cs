using SlowCow.Setup.Base.Models;
namespace SlowCow.Setup.Base.Interfaces;

public interface IUpdater
{
    SlowCowVersion? GetUpdateInfo();
    ReleaseInfoModel GetCurrentInfo();
    bool InstallLatest(bool force);
}
