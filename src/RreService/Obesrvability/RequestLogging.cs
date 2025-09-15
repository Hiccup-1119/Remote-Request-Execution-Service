using System.Text.Json;
using RreService.Security;

namespace RreService.Observability;

public static class RequestLogging
{
    public static string toStructured(object o) =>
    JsonSerializer.Serialize(o);
}