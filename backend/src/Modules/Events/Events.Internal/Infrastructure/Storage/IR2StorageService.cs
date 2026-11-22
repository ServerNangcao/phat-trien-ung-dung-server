using Ticketing.Infrastructure.Core.Storage;

namespace Events.Internal.Infrastructure.Storage;

/// <summary>
/// Interface abstraction cho R2/S3 object storage trong Events module.
/// Kế thừa trực tiếp từ abstraction dùng chung IFileStorage trong BuildingBlocks.
/// </summary>
internal interface IR2StorageService : IFileStorage
{
}
