using System.Text.Json;
using System.Text.Json.Serialization;
namespace PcShop.Api.Services;
public static class Json
{
 public static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter(allowIntegerValues:false)}};
 public static string Write<T>(T value)=>JsonSerializer.Serialize(value,Options);
 public static T Read<T>(string value)=>JsonSerializer.Deserialize<T>(value,Options)??throw new InvalidOperationException("Invalid stored JSON");
}

