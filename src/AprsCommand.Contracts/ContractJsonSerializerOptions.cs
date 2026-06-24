using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;

namespace AprsCommand.Contracts;

public static class ContractJsonSerializerOptions
{
    public static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
