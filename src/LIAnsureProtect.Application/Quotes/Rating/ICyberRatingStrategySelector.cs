namespace LIAnsureProtect.Application.Quotes.Rating;

public interface ICyberRatingStrategySelector
{
    CyberRatingResult Rate(CyberRatingInput input);
}
