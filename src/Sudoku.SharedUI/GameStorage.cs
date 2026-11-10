using Microsoft.JSInterop;

namespace Sudoku.SharedUI;

public interface IGameStorage
{
    Task SaveAsync(string json);

    Task<string?> LoadAsync();
}

public sealed class BrowserGameStorage : IGameStorage
{
    private readonly IJSRuntime _js;

    public BrowserGameStorage(IJSRuntime js) => _js = js;

    public async Task SaveAsync(string json)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", GamePersistence.StorageKey, json);
        }
        catch
        {
        }
    }

    public async Task<string?> LoadAsync()
    {
        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", GamePersistence.StorageKey);
        }
        catch
        {
            return null;
        }
    }
}
