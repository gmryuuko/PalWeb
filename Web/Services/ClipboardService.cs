using Microsoft.JSInterop;
using MudBlazor;

namespace Web.Services;

public class ClipboardService(IJSRuntime jsRuntime, ISnackbar snackbar)
{
    public async Task CopyToClipboardAsync(string text)
    {
        await jsRuntime.InvokeVoidAsync("unsecuredCopyToClipboard", text);
        snackbar.Add("Copied to clipboard", Severity.Success);
    }
}
