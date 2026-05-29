namespace WorldplayAMS.Core.Interfaces;

public interface IFallbackCacheService
{
    void SaveFailedSession(string tagString, string actionType);
}
