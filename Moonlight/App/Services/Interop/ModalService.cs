﻿using Microsoft.JSInterop;

namespace Moonlight.App.Services.Interop;

public class ModalService
{
    private readonly IJSRuntime JsRuntime;

    public ModalService(IJSRuntime jsRuntime)
    {
        JsRuntime = jsRuntime;
    }

    public async Task Show(string id)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("moonlight.modals.show", id);
        }
        catch (Exception)
        {
            // ignored
        }
    }
    
    public async Task Hide(string id)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("moonlight.modals.hide", id);
        }
        catch (Exception)
        {
            // Ignored
        }
    }
}