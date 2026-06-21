namespace LIAnsureProtect.Application.Quotes.Rating;

public interface ICyberRatingStrategy
{
    bool CanRate(CyberRatingInput input);

    CyberRatingResult Rate(CyberRatingInput input);
}
