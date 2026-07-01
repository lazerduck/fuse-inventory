using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Models;

namespace Fuse.Core.Areas.Tag;

public interface ITagService
{
    Task<IReadOnlyList<Models.Tag>> GetTagsAsync();
    Task<Models.Tag?> GetTagByIdAsync(Guid id);
    Task<Result<Models.Tag>> CreateTagAsync(CreateTag command);
    Task<Result<Models.Tag>> UpdateTagAsync(UpdateTag command);
    Task<Result> DeleteTagAsync(DeleteTag command);
}
