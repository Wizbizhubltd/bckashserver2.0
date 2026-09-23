namespace BCKash.Infrastructure.Files;

public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    /// <summary>Relative paths are resolved against <see cref="AppContext.BaseDirectory"/>.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";
}
