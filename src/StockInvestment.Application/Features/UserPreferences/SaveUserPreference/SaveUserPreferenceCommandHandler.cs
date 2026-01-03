using MediatR;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;

namespace StockInvestment.Application.Features.UserPreferences.SaveUserPreference;

public class SaveUserPreferenceCommandHandler : IRequestHandler<SaveUserPreferenceCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public SaveUserPreferenceCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(SaveUserPreferenceCommand request, CancellationToken cancellationToken)
    {
        // Check if preference already exists
        var existingPreference = await _unitOfWork.UserPreferences
            .GetByUserAndKeyAsync(request.UserId, request.PreferenceKey);

        if (existingPreference != null)
        {
            // Update existing preference
            existingPreference.PreferenceValue = request.PreferenceValue;
            existingPreference.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.Repository<UserPreference>().UpdateAsync(existingPreference);
        }
        else
        {
            // Create new preference
            var newPreference = new UserPreference
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                PreferenceKey = request.PreferenceKey,
                PreferenceValue = request.PreferenceValue,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _unitOfWork.UserPreferences.AddAsync(newPreference);
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
