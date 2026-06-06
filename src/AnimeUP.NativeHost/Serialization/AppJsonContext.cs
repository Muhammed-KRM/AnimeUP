using System.Text.Json.Serialization;
using AnimeUP.NativeHost.Models;

namespace AnimeUP.NativeHost.Serialization
{
    /// <summary>
    /// AOT / Trim uyumlu JSON serileştirme bağlamı.
    ///
    /// PublishTrimmed=true modunda System.Text.Json'ın normal Serialize/Deserialize
    /// çağrıları çalışma zamanında reflection kullandığı için trimmer uyarısı üretir.
    /// Bu sınıf, kullanılan tüm tipleri derleme zamanında kayıt altına alarak
    /// IL2026 uyarılarını ve olası trim hatalarını tamamen ortadan kaldırır.
    /// </summary>
    [JsonSerializable(typeof(PlayRequest))]
    [JsonSerializable(typeof(StatusResponse))]
    [JsonSerializable(typeof(Dictionary<string, object?>))]
    [JsonSerializable(typeof(IReadOnlyList<Dictionary<string, object?>>))]
    [JsonSerializable(typeof(List<Dictionary<string, object?>>))]
    [JsonSerializable(typeof(object))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false)]
    public partial class AppJsonContext : JsonSerializerContext
    {
    }
}
