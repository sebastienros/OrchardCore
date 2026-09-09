using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace OrchardCore.Cli;

internal sealed class CacheService
{
    private readonly CliPaths _paths;

    public CacheService(CliPaths paths)
    {
        _paths = paths;
    }

    public async Task<CachedContentRecord?> ReadAsync(string tenantUrl, CacheKind kind, CancellationToken cancellationToken)
    {
        var path = _paths.GetCacheFilePath(tenantUrl, kind);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = AtomicFile.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, CliJsonContext.Default.CachedContentRecord, cancellationToken);
    }

    public async Task WriteAsync(string tenantUrl, CacheKind kind, CachedContentRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var path = _paths.GetCacheFilePath(tenantUrl, kind);
        var temporaryPath = $"{path}.{Guid.NewGuid():n}.tmp";
        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, record, CliJsonContext.Default.CachedContentRecord, cancellationToken);
            }

            CliPaths.SetOwnerOnlyFile(temporaryPath);
            await AtomicFile.ReplaceAsync(temporaryPath, path, cancellationToken);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public async Task InvalidateAsync(string tenantUrl, CancellationToken cancellationToken)
    {
        foreach (var kind in new[] { CacheKind.Manifest, CacheKind.OpenApi })
        {
            var record = await ReadAsync(tenantUrl, kind, cancellationToken);
            if (record is not null)
            {
                record.ExpiresAt = DateTimeOffset.MinValue;
                await WriteAsync(tenantUrl, kind, record, cancellationToken);
            }
        }
    }

    public async Task<CachedContentResult> GetOrRefreshAsync(
        string tenantUrl,
        CacheKind kind,
        Uri sourceUrl,
        TimeSpan ttl,
        bool force,
        Func<string?, CancellationToken, Task<HttpResponseMessage>> fetchAsync,
        CancellationToken cancellationToken)
    {
        var cached = await ReadAsync(tenantUrl, kind, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (!force && cached is not null && cached.ExpiresAt > now)
        {
            return new CachedContentResult
            {
                CacheRecord = cached,
                FromCache = true,
                IsStale = false,
            };
        }

        try
        {
            using var response = await fetchAsync(cached?.ETag, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
            {
                cached.FetchedAt = now;
                cached.ExpiresAt = now.Add(ttl);
                await WriteAsync(tenantUrl, kind, cached, cancellationToken);
                return new CachedContentResult
                {
                    CacheRecord = cached,
                    FromCache = true,
                    IsStale = false,
                };
            }

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var record = new CachedContentRecord
            {
                Url = sourceUrl.AbsoluteUri,
                ETag = response.Headers.ETag?.Tag,
                FetchedAt = now,
                ExpiresAt = now.Add(ttl),
                Content = content,
            };

            await WriteAsync(tenantUrl, kind, record, cancellationToken);
            return new CachedContentResult
            {
                CacheRecord = record,
                FromCache = false,
                IsStale = false,
            };
        }
        catch (Exception exception) when (
            cached is not null &&
            exception is HttpRequestException or TaskCanceledException)
        {
            return new CachedContentResult
            {
                CacheRecord = cached,
                FromCache = true,
                IsStale = true,
            };
        }
    }

    public static void AddIfNoneMatchHeader(HttpRequestMessage request, string? eTag)
    {
        if (!string.IsNullOrWhiteSpace(eTag))
        {
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(eTag));
        }
    }
}
