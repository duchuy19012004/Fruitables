using Fruitables.Models.Returns;
using Microsoft.AspNetCore.Http;

namespace Fruitables.Services.Interfaces;

public interface IReturnEvidenceService
{
    Task<(bool Success, string? Error, ReturnEvidence? Evidence)> UploadAsync(int returnRequestId, int? returnItemId, int userId, IFormFile file, bool isAdmin, CancellationToken cancellationToken = default);
    Task<(ReturnEvidence Evidence, Stream Content)?> OpenReadAsync(int evidenceId, int userId, bool isAdmin, CancellationToken cancellationToken = default);
}
