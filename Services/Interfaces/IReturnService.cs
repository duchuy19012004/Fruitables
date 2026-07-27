using Fruitables.Models.Returns;
using Fruitables.ViewModels.Returns;

namespace Fruitables.Services.Interfaces;

public interface IReturnService
{
    Task<ReturnResult> SubmitAsync(int userId, ReturnSubmitViewModel model, CancellationToken cancellationToken = default);
    Task<ReturnRequest?> GetForCustomerAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<List<ReturnRequest>> GetCustomerRequestsAsync(int userId, CancellationToken cancellationToken = default);
    Task<ReturnRequest?> GetForAdminAsync(int id, CancellationToken cancellationToken = default);
    Task<List<ReturnRequest>> GetQueueAsync(ReturnQueueFilter filter, CancellationToken cancellationToken = default);
    Task<ReturnResult> RequestEvidenceAsync(int id, int adminId, string note, byte[] rowVersion, CancellationToken cancellationToken = default);
    Task<ReturnResult> StartReviewAsync(int id, int adminId, byte[] rowVersion, CancellationToken cancellationToken = default);
    Task<ReturnResult> DecideAsync(int adminId, ReturnDecisionViewModel model, CancellationToken cancellationToken = default);
    Task<ReturnResult> CancelAsync(int id, int userId, CancellationToken cancellationToken = default);
    Task<ReturnResult> UpdateResolutionAsync(int id, int adminId, ReturnRequestStatus target, string note, byte[] rowVersion, CancellationToken cancellationToken = default);
}
