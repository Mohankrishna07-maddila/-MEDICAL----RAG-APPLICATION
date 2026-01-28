using Amazon.S3;
using Amazon.S3.Model;

namespace HealthBot.Api.Services;

public class S3DocumentLoader
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _config;
    private readonly string _bucketName;

    public S3DocumentLoader(IAmazonS3 s3Client, IConfiguration config)
    {
        _s3Client = s3Client;
        _config = config;
        _bucketName = _config["Startups:S3BucketName"] ?? "healthbot-knowledge-base"; // Default or from config
    }

    public async Task<string> LoadContent(string key)
    {
        try 
        {
            var request = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            using var response = await _s3Client.GetObjectAsync(request);
            using var reader = new StreamReader(response.ResponseStream);
            return await reader.ReadToEndAsync();
        }
        catch (AmazonS3Exception ex)
        {
            Console.WriteLine($"[S3] Error loading {key}: {ex.Message}");
            return "";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[S3] General error: {ex.Message}");
            return "";
        }
    }

    public async Task<List<string>> ListDocuments(string prefix = "")
    {
         try 
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(request);
            return response.S3Objects.Select(o => o.Key).Where(k => k.EndsWith(".txt")).ToList();
        }
        catch (Exception)
        {
            return new List<string>();
        }
    }

    public async Task<List<Models.S3FileInfo>> ListDocumentsWithMetadata(string prefix = "")
    {
        try
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix
            };

            var response = await _s3Client.ListObjectsV2Async(request);
            
            return response.S3Objects
                .Where(o => o.Key.EndsWith(".txt"))
                .Select(o => new Models.S3FileInfo
                {
                    Key = o.Key,
                    LastModified = o.LastModified ?? DateTime.UtcNow,
                    Size = o.Size ?? 0
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[S3] Error listing files with metadata: {ex.Message}");
            return new List<Models.S3FileInfo>();
        }
    }

    public async Task DeleteAllFilesAsync()
    {
        try
        {
            var objects = await ListDocuments();
            if (!objects.Any()) return;

            var deleteRequest = new DeleteObjectsRequest
            {
                BucketName = _bucketName,
                Objects = objects.Select(k => new KeyVersion { Key = k }).ToList()
            };

            await _s3Client.DeleteObjectsAsync(deleteRequest);
            Console.WriteLine($"[S3] Deleted {objects.Count} files.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[S3] Error deleting files: {ex.Message}");
        }
    }

    public async Task UploadFileAsync(string key, string content)
    {
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                ContentBody = content
            };

            await _s3Client.PutObjectAsync(request);
            Console.WriteLine($"[S3] Uploaded {key}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[S3] Error uploading {key}: {ex.Message}");
        }
    }
}
