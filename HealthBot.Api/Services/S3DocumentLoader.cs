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
}
