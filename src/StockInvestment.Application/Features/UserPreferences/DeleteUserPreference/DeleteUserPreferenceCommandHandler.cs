using MediatR;
using StockInvestment.Application.Interfaces;

namespace StockInvestment.Application.Features.UserPreferences.DeleteUserPreference;

public class DeleteUserPreferenceCommandHandler : IRequestHandler<DeleteUserPreferenceCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteUserPreferenceCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteUserPreferenceCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.UserPreferences.DeleteByUserAndKeyAsync(request.UserId, request.PreferenceKey);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
