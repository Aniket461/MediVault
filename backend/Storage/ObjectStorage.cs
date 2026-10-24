using Amazon.S3;
using Amazon.S3.Model;

namespace MediVault.Api.Storage;

public sealed class StorageOptions
{
    public string ServiceUrl { get; set; } = "";

    public string Bucket { get; set; } = "";

    public string AccessKey { get; set; } = "";

    public string SecretKey { get; set; } = "";
}

public sealed class ObjectStorage(IAmazonS3 s3, IConfiguration configuration)
{
    private readonly string _bucket = configuration["Storage:Bucket"]
        ?? throw new InvalidOperationException("Storage bucket is missing.");

    public async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await s3.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, cancellationToken);
                return;
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                return;
            }
            catch (Exception) when (attempt < 30)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
    }

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await s3.DeleteObjectAsync(_bucket, key, cancellationToken);
    }

    public async Task<MemoryStream> DownloadAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await s3.GetObjectAsync(_bucket, key, cancellationToken);
        var memory = new MemoryStream();
        await response.ResponseStream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;
        return memory;
    }
}
