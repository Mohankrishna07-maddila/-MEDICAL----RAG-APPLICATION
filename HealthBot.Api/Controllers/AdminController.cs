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

    [HttpPost("sync-rag/incremental")]
    [AllowAnonymous]
    public async Task<IActionResult> IncrementalSync()
    {
        var result = await _rag.IncrementalSyncAsync();
        
        return Ok(new
        {
            Message = result.FilesProcessed > 0 
                ? $"✅ Synced {result.FilesProcessed} files, {result.ChunksAdded} chunks in {result.DurationSeconds:F2}s"
                : "✅ No new files to sync",
            FilesProcessed = result.FilesProcessed,
            ChunksAdded = result.ChunksAdded,
            DurationSeconds = result.DurationSeconds,
            ProcessedFiles = result.ProcessedFiles
        });
    }

    [HttpPost("sync-rag/full")]
    [AllowAnonymous]
    public async Task<IActionResult> FullSync()
    {
        // Upload local files to S3 first
        var sourceDir = Path.Combine(Directory.GetCurrentDirectory(), "source");
        if (Directory.Exists(sourceDir))
        {
            var files = Directory.GetFiles(sourceDir, "*.txt", SearchOption.AllDirectories);
            Console.WriteLine($"[Admin] Found {files.Length} files to upload");

            await _s3.DeleteAllFilesAsync();

            foreach (var file in files)
            {
                var content = await System.IO.File.ReadAllTextAsync(file);
                
                // Preserve folder structure in S3 key
                var relativePath = Path.GetRelativePath(sourceDir, file).Replace("\\", "/");
                await _s3.UploadFileAsync(relativePath, content);
            }
        }

        await _rag.ResetAndIngestFromS3Async();
        return Ok(new { Message = "✅ Full sync complete" });
    }

    [HttpGet("sync-rag/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSyncStatus()
    {
        var status = await _rag.GetSyncStatusAsync();
        return Ok(status);
    }

    [HttpGet("diagnostic")]
    [AllowAnonymous]
    public async Task<IActionResult> GetDiagnostic()
    {
        var diagnostic = await _rag.GetDiagnosticInfoAsync();
        return Ok(diagnostic);
    }
}
