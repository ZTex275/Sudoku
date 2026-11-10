using System.Text.Json;
using Sudoku.Core.Models;

namespace Sudoku.SharedUI;

public sealed class GamePersistence
{
    public const string StorageKey = "sudoku.savedGame";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGameStorage _storage;

    public GamePersistence(IGameStorage storage) => _storage = storage;

    public async Task SaveAsync(SavedGame save)
    {
        try
        {
            var json = JsonSerializer.Serialize(save, Json);
            await _storage.SaveAsync(json);
        }
        catch
        {
            // Хранилище может быть недоступно — партия просто не запомнится.
        }
    }

    public async Task<SavedGame?> LoadAsync()
    {
        try
        {
            var json = await _storage.LoadAsync();
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonSerializer.Deserialize<SavedGame>(json, Json);
        }
        catch
        {
            return null;
        }
    }
}
