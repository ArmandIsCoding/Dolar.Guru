using Microsoft.Extensions.Configuration;

namespace ARM.Mesa.Bursatil.Sync;

public static class SyncConfiguration
{
    public static IConfigurationRoot Load(string baseDirectory, string? privateSettingsPath = null)
    {
        if (privateSettingsPath is not null && !Path.IsPathFullyQualified(privateSettingsPath))
            throw new ArgumentException("--settings requiere una ruta absoluta al archivo privado.");
        try
        {
            return new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(baseDirectory, "appsettings.json"), optional: false)
                .AddJsonFile(privateSettingsPath ?? Path.Combine(baseDirectory, "appsettings.Production.json"),
                    optional: privateSettingsPath is null)
                .Build();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or UnauthorizedAccessException)
        {
            // JSON parser exceptions may contain source text. Never log private file contents.
            throw new InvalidOperationException("No se pudo cargar la configuración de Sync. Revise que los archivos existan, sean JSON válido y tengan permisos de lectura.");
        }
    }

    public static string ReadApiKey(IConfiguration configuration)
    {
        var key = configuration["Briefing:ApiKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Configure Briefing:ApiKey en el archivo privado de Sync. No se llamó al proveedor.");
        return key;
    }
}
