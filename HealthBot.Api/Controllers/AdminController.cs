using Microsoft.AspNetCore.Mvc;
using HealthBot.Api.Services;
using HealthBot.Api;
using System.IO;
using Microsoft.AspNetCore.Authorization;

namespace HealthBot.Api.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly S3DocumentLoader _s3;
    private readonly PolicyRagService _rag;

    public AdminController(S3DocumentLoader s3, PolicyRagService rag)
    {
        _s3 = s3;
        _rag = rag;
    }

    [HttpPost("sync-rag")]
    [AllowAnonymous]
    public async Task<IActionResult> SyncRag()
    {
        // 1. Upload Local Files to S3
        var sourceDir = Path.Combine(Directory.GetCurrentDirectory(), "source");
        if (Directory.Exists(sourceDir))
        {
            var files = Directory.GetFiles(sourceDir, "*.txt");
            Console.WriteLine($"[Admin] Found {files.Length} files in {sourceDir} to sync.");
            
            // Delete existing files in S3 first to ensure a clean state
            await _s3.DeleteAllFilesAsync(); 

            foreach (var file in files)
            {
                var content = await System.IO.File.ReadAllTextAsync(file);
                var key = Path.GetFileName(file);
                await _s3.UploadFileAsync(key, content);
            }
        }
        else
        {
            Console.WriteLine($"[Admin] Source directory {sourceDir} not found!");
            return NotFound($"Source directory {sourceDir} not found.");
        }

        // 2. Reset RAG System (Clear DB + Re-ingest from S3)
        await _rag.ResetAndIngestFromS3Async();

        return Ok(new { Message = "RAG System Synced & Reset Successfully" });
    }

    [HttpGet("diagnostic")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDiagnostic()
    {
        var diagnostic = await _rag.GetDiagnosticInfoAsync();
        return Ok(diagnostic);
    }
}
