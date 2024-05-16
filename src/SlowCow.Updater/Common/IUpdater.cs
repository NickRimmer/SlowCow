namespace SlowCow.Updater.Common;

public interface IUpdater
{
    SlowCowVersion? GetVersion();
}
