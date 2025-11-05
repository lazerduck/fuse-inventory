using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Interfaces;

public interface ITagService
{
    Task<IReadOnlyList<Tag>> GetTagsAsync();
    Task<Tag?> GetTagByIdAsync(Guid id);
    Task<Result<Tag>> CreateTagAsync(CreateTag command);
    Task<Result<Tag>> UpdateTagAsync(UpdateTag command);
    Task<Result> DeleteTagAsync(DeleteTag command);
}