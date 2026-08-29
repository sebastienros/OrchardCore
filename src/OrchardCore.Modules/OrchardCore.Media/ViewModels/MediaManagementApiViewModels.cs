using System.Collections.Generic;

namespace OrchardCore.Media.ViewModels;

/// <summary>
/// Describes the query string used to browse media folders.
/// </summary>
public sealed class BrowseFoldersRequest
{
    public string Path { get; set; }

    public int? Skip { get; set; }

    public int? Take { get; set; }
}

/// <summary>
/// Describes the query string used to browse media files.
/// </summary>
public sealed class BrowseFilesRequest
{
    public string Path { get; set; }

    public string Extensions { get; set; }

    /// <summary>
    /// Gets or sets the number of files to skip.
    /// </summary>
    public int? Skip { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of files to return.
    /// </summary>
    public int? Take { get; set; }
}

/// <summary>
/// Describes the query string used to browse folder content.
/// </summary>
public sealed class BrowseDirectoryContentRequest
{
    public string Path { get; set; }

    public string Extensions { get; set; }
}

/// <summary>
/// Describes the query string used to resolve a single media file.
/// </summary>
public sealed class GetMediaFileRequest
{
    public string Path { get; set; }
}

/// <summary>
/// Describes the query string used to resolve multiple media files.
/// </summary>
public sealed class GetMediaFieldItemsRequest
{
    public string[] Paths { get; set; }
}

/// <summary>
/// Describes the query string used to filter all media items.
/// </summary>
public sealed class GetAllMediaItemsRequest
{
    public string Extensions { get; set; }

    /// <summary>
    /// Gets or sets the number of media items to skip.
    /// </summary>
    public int? Skip { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of media items to return.
    /// </summary>
    public int? Take { get; set; }
}

/// <summary>
/// Describes a paged list of media resources.
/// </summary>
public sealed class MediaListResponse
{
    /// <summary>
    /// Gets the number of media resources skipped.
    /// </summary>
    public int Skip { get; init; }

    /// <summary>
    /// Gets the maximum number of media resources requested.
    /// </summary>
    public int Take { get; init; }

    /// <summary>
    /// Gets the total number of matching media resources.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets the media resources in the current page.
    /// </summary>
    public IReadOnlyList<FileStoreEntryDto> Items { get; init; } = [];
}

/// <summary>
/// Describes a copy request for a media file.
/// </summary>
public sealed class CopyMediaRequest
{
    public string OldPath { get; set; }

    public string NewPath { get; set; }
}

/// <summary>
/// Describes a move request for a media file.
/// </summary>
public sealed class MoveMediaRequest
{
    public string OldPath { get; set; }

    public string NewPath { get; set; }
}

/// <summary>
/// Describes a folder creation request.
/// </summary>
public sealed class CreateFolderRequest
{
    public string Path { get; set; }

    public string Name { get; set; }
}

/// <summary>
/// Describes a folder deletion request.
/// </summary>
public sealed class DeleteFolderRequest
{
    public string Path { get; set; }
}

/// <summary>
/// Describes a file deletion request.
/// </summary>
public sealed class DeleteMediaRequest
{
    public string Path { get; set; }
}

/// <summary>
/// Describes a batch file deletion request.
/// </summary>
public sealed class DeleteMediaListRequest
{
    public List<string> Paths { get; set; } = [];
}

/// <summary>
/// Describes a batch file move request.
/// </summary>
public sealed class MoveMediaBatchRequest
{
    public string[] MediaNames { get; set; }

    public string SourceFolder { get; set; }

    public string TargetFolder { get; set; }
}

/// <summary>
/// Describes a raw streaming upload request.
/// </summary>
public sealed class UploadMediaStreamRequest
{
    public string Path { get; set; }

    public string FileName { get; set; }
}

/// <summary>
/// Describes the configured media management constraints.
/// </summary>
public sealed class MediaConstraintsDto : PermittedStorageDto
{
    public IEnumerable<string> AllowedFileExtensions { get; set; } = [];

    public long MaxFileSize { get; set; }

    public int MaxUploadChunkSize { get; set; }

    public string AuthenticationScheme { get; set; }
}

/// <summary>
/// Describes a completed file copy operation.
/// </summary>
public sealed class CopyMediaResultDto
{
    public string OldPath { get; set; }

    public string NewPath { get; set; }

    public FileStoreEntryDto File { get; set; }
}

/// <summary>
/// Describes a completed file move operation.
/// </summary>
public sealed class MoveMediaResultDto
{
    public string OldPath { get; set; }

    public string NewPath { get; set; }

    public FileStoreEntryDto File { get; set; }
}

/// <summary>
/// Describes a completed folder creation operation.
/// </summary>
public sealed class CreateFolderResultDto
{
    public FileStoreEntryDto Folder { get; set; }
}

/// <summary>
/// Describes a completed file deletion operation.
/// </summary>
public sealed class DeleteMediaResultDto
{
    public string Path { get; set; }
}

/// <summary>
/// Describes a completed batch file deletion operation.
/// </summary>
public sealed class DeleteMediaListResultDto
{
    public IReadOnlyList<string> Paths { get; set; } = [];
}

/// <summary>
/// Describes a completed folder deletion operation.
/// </summary>
public sealed class DeleteFolderResultDto
{
    public string Path { get; set; }
}

/// <summary>
/// Describes a completed batch file move operation.
/// </summary>
public sealed class MoveMediaBatchResultDto
{
    public IReadOnlyList<string> MediaNames { get; set; } = [];

    public string SourceFolder { get; set; }

    public string TargetFolder { get; set; }
}
