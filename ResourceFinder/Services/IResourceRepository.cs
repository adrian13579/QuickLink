using ResourceFinder.Models;

namespace ResourceFinder.Services;

public interface IResourceRepository
{
    Task<IReadOnlyList<Resource>> GetAllAsync();
    Task<Resource?> GetByIdAsync(Guid id);
    Task SaveAsync(Resource resource);
    Task DeleteAsync(Guid id);
}
