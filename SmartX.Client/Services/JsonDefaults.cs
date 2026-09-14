using System.Text.Json;

namespace SmartX.Client.Services
{
    public  static class JsonDefaults
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    }
}
