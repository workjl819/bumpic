namespace Bumpic.Web.Extensions;

public class CultureContext
{
    public const string ContextKey = "PhoteRescueCulture";

    public CultureContext(string culture)
    {
        Culture = culture;
    }
    
    public string Culture { get; private set; }
}