using MediatR;
using StockInvestment.Application.Interfaces;
using StockInvestment.Domain.Entities;
using System.Text.Json;

namespace StockInvestment.Application.Features.CorporateEvents.CreateEvent;

public class CreateEventCommandHandler : IRequestHandler<CreateEventCommand, CorporateEvent>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateEventCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CorporateEvent> Handle(CreateEventCommand request, CancellationToken cancellationToken)
    {
        // Create the appropriate event type based on EventType
        CorporateEvent corporateEvent = request.EventType switch
        {
            CorporateEventType.Earnings => CreateEarningsEvent(request),
            CorporateEventType.Dividend => CreateDividendEvent(request),
            CorporateEventType.StockSplit => CreateStockSplitEvent(request),
            CorporateEventType.AGM => CreateAGMEvent(request),
            CorporateEventType.RightsIssue => CreateRightsIssueEvent(request),
            _ => throw new ArgumentException($"Unknown event type: {request.EventType}")
        };

        // Set common properties
        corporateEvent.StockTickerId = request.StockTickerId;
        corporateEvent.EventDate = request.EventDate;
        corporateEvent.Title = request.Title;
        corporateEvent.Description = request.Description;
        corporateEvent.SourceUrl = request.SourceUrl;
        corporateEvent.Status = request.Status;

        return await _unitOfWork.CorporateEvents.CreateAsync(corporateEvent);
    }

    private EarningsEvent CreateEarningsEvent(CreateEventCommand request)
    {
        var earnings = new EarningsEvent();
        
        if (request.EventData != null)
        {
            if (request.EventData.TryGetValue("Period", out var period))
                earnings.Period = period?.ToString() ?? string.Empty;
            
            if (request.EventData.TryGetValue("Year", out var year))
                earnings.Year = Convert.ToInt32(year);
            
            if (request.EventData.TryGetValue("EPS", out var eps))
                earnings.EPS = Convert.ToDecimal(eps);
            
            if (request.EventData.TryGetValue("Revenue", out var revenue))
                earnings.Revenue = Convert.ToDecimal(revenue);
            
            if (request.EventData.TryGetValue("NetProfit", out var netProfit))
                earnings.NetProfit = Convert.ToDecimal(netProfit);
        }
        
        return earnings;
    }

    private DividendEvent CreateDividendEvent(CreateEventCommand request)
    {
        var dividend = new DividendEvent();
        
        if (request.EventData != null)
        {
            if (request.EventData.TryGetValue("DividendPerShare", out var dps))
                dividend.DividendPerShare = Convert.ToDecimal(dps);
            
            if (request.EventData.TryGetValue("CashDividend", out var cash))
                dividend.CashDividend = Convert.ToDecimal(cash);
            
            if (request.EventData.TryGetValue("StockDividendRatio", out var ratio))
                dividend.StockDividendRatio = Convert.ToDecimal(ratio);
            
            if (request.EventData.TryGetValue("ExDividendDate", out var exDate))
                dividend.ExDividendDate = Convert.ToDateTime(exDate);
            
            if (request.EventData.TryGetValue("RecordDate", out var recordDate))
                dividend.RecordDate = Convert.ToDateTime(recordDate);
            
            if (request.EventData.TryGetValue("PaymentDate", out var paymentDate))
                dividend.PaymentDate = Convert.ToDateTime(paymentDate);
        }
        
        return dividend;
    }

    private StockSplitEvent CreateStockSplitEvent(CreateEventCommand request)
    {
        var split = new StockSplitEvent();
        
        if (request.EventData != null)
        {
            if (request.EventData.TryGetValue("SplitRatio", out var splitRatio))
                split.SplitRatio = splitRatio?.ToString() ?? string.Empty;
            
            if (request.EventData.TryGetValue("IsReverseSplit", out var isReverse))
                split.IsReverseSplit = Convert.ToBoolean(isReverse);
            
            if (request.EventData.TryGetValue("EffectiveDate", out var effectiveDate))
                split.EffectiveDate = Convert.ToDateTime(effectiveDate);
            
            if (request.EventData.TryGetValue("RecordDate", out var recordDate))
                split.RecordDate = Convert.ToDateTime(recordDate);
        }
        
        return split;
    }

    private AGMEvent CreateAGMEvent(CreateEventCommand request)
    {
        var agm = new AGMEvent();
        
        if (request.EventData != null)
        {
            if (request.EventData.TryGetValue("Location", out var location))
                agm.Location = location?.ToString();
            
            if (request.EventData.TryGetValue("MeetingTime", out var meetingTime))
                agm.MeetingTime = TimeSpan.Parse(meetingTime?.ToString() ?? "00:00");
            
            if (request.EventData.TryGetValue("Agenda", out var agenda))
                agm.Agenda = JsonSerializer.Serialize(agenda);
            
            if (request.EventData.TryGetValue("RecordDate", out var recordDate))
                agm.RecordDate = Convert.ToDateTime(recordDate);
            
            if (request.EventData.TryGetValue("Year", out var year))
                agm.Year = Convert.ToInt32(year);
        }
        
        return agm;
    }

    private RightsIssueEvent CreateRightsIssueEvent(CreateEventCommand request)
    {
        var rights = new RightsIssueEvent();
        
        if (request.EventData != null)
        {
            if (request.EventData.TryGetValue("NumberOfShares", out var shares))
                rights.NumberOfShares = Convert.ToInt64(shares);
            
            if (request.EventData.TryGetValue("IssuePrice", out var price))
                rights.IssuePrice = Convert.ToDecimal(price);
            
            if (request.EventData.TryGetValue("RightsRatio", out var ratio))
                rights.RightsRatio = ratio?.ToString();
            
            if (request.EventData.TryGetValue("RecordDate", out var recordDate))
                rights.RecordDate = Convert.ToDateTime(recordDate);
            
            if (request.EventData.TryGetValue("SubscriptionStartDate", out var startDate))
                rights.SubscriptionStartDate = Convert.ToDateTime(startDate);
            
            if (request.EventData.TryGetValue("SubscriptionEndDate", out var endDate))
                rights.SubscriptionEndDate = Convert.ToDateTime(endDate);
            
            if (request.EventData.TryGetValue("Purpose", out var purpose))
                rights.Purpose = purpose?.ToString();
        }
        
        return rights;
    }
}
