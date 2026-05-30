using ResourceFinder.Models;

namespace ResourceFinder.Services;

public interface IResourceRepository
{
    long Version { get; }
    Task<IReadOnlyList<Resource>> GetAllAsync();
    Task<Resource?> GetByIdAsync(Guid id);
    Task SaveAsync(Resource resource);
    Task SaveRangeAsync(IEnumerable<Resource> resources);
    Task DeleteAsync(Guid id);
    void InvalidateCache();
}
