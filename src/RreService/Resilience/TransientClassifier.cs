using System.Net;

namespace RreService.Resilience;

public static class TransientClassifier
{
    public static bool IsRetryableHttpStatus(int code) => code is 408 or 429 || (code >= 500 && code != 501 && code != 505);
}