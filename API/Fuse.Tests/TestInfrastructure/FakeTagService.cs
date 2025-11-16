using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fuse.Core.Commands;
using Fuse.Core.Helpers;
using Fuse.Core.Interfaces;
using Fuse.Core.Models;

namespace Fuse.Tests.TestInfrastructure;

public sealed class FakeTagService : ITagService
{
    private readonly IFuseStore _store;
    public FakeTagService(IFuseStore store) => _store = store;

    public Task<IReadOnlyList<Tag>> GetTagsAsync() => Task.FromResult((IReadOnlyList<Tag>)_store.Current!.Tags);

    public Task<Tag?> GetTagByIdAsync(Guid id)
        => Task.FromResult(_store.Current!.Tags.FirstOrDefault(t => t.Id == id));

    public Task<Result<Tag>> CreateTagAsync(CreateTag command)
        => Task.FromResult(Result<Tag>.Failure("Not implemented in tests."));

    public Task<Result<Tag>> UpdateTagAsync(UpdateTag command)
        => Task.FromResult(Result<Tag>.Failure("Not implemented in tests."));

    public Task<Result> DeleteTagAsync(DeleteTag command)
        => Task.FromResult(Result.Failure("Not implemented in tests."));
}
