using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;

// Author: uye (owner of the MaaAssistantArknights team)
// Original Source: MaaAssistantArknights project - https://github.com/MaaAssistantArknights/MaaAssistantArknights
// License: Apache License 2.0 (this file only)
//
// This file is based on work originally developed in the MaaAssistantArknights project,
// which is licensed under the GNU AGPL v3.0 only.
//
// As the original author of this code, I am re-licensing this specific file under
// the Apache License 2.0 for use in PCL2-CE.
//
// Description:
// Implements a WPF clipboard fix to handle OpenClipboard failures in TextBox,
// RichTextBox, and DataGrid, typically caused by focus issues or external hooks.
//
// Date: 2025-07-03

namespace PCL.Shion.Controls.Behaviors;

public sealed class ClipboardInterceptor : AvaloniaObject
{
    public static readonly AttachedProperty<bool> EnableSafeClipboardProperty =
        AvaloniaProperty.RegisterAttached<ClipboardInterceptor, Control, bool>("EnableSafeClipboard");

    static ClipboardInterceptor()
    {
        EnableSafeClipboardProperty.Changed.AddClassHandler<Control>((element, e) =>
            OnEnableSafeClipboardChanged(element, e));
    }

    private ClipboardInterceptor()
    {
    }

    public static void SetEnableSafeClipboard(AvaloniaObject element, bool value)
    {
        element.SetValue(EnableSafeClipboardProperty, value);
    }

    public static bool GetEnableSafeClipboard(AvaloniaObject element)
    {
        return element.GetValue(EnableSafeClipboardProperty);
    }

    private static void OnEnableSafeClipboardChanged(Control d, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true)
            return;

        switch (d)
        {
            case TextBox box:
                AddCommandBindingsToTextBox(box);
                break;
        }
    }

    private static void AddCommandBindingsToTextBox(TextBox tb)
    {
        tb.AddHandler(InputElement.KeyDownEvent, OnKeyDownTextBox, RoutingStrategies.Tunnel);
    }

    private static void OnKeyDownTextBox(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb || e.KeyModifiers != KeyModifiers.Control)
            return;

        switch (e.Key)
        {
            case Key.C:
                if (OnCopyTextBox(tb))
                    e.Handled = true;
                break;
            case Key.X:
                if (OnCutTextBox(tb))
                    e.Handled = true;
                break;
            case Key.V:
                OnPasteTextBox(tb);
                e.Handled = true;
                break;
        }
    }

    private static bool OnCopyTextBox(TextBox tb)
    {
        var selectedText = GetSelectedText(tb);
        if (selectedText.Length <= 0)
            return false;

        _ = TrySetClipboardText(tb, selectedText);
        return true;
    }

    private static bool OnCutTextBox(TextBox tb)
    {
        var selectedText = GetSelectedText(tb);
        if (selectedText.Length <= 0)
            return false;

        _ = TrySetClipboardText(tb, selectedText);

        ReplaceSelection(tb, string.Empty);
        return true;
    }

    private static void OnPasteTextBox(TextBox tb)
    {
        _ = PasteTextBox(tb);
    }

    private static async Task PasteTextBox(TextBox tb)
    {
        var pasteText = await GetClipboardText(tb);
        if (pasteText is null)
            return;

        if (!tb.AcceptsReturn)
            pasteText = pasteText.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");

        var start = Math.Min(tb.SelectionStart, tb.SelectionEnd);
        ReplaceSelection(tb, pasteText);
        tb.CaretIndex = start + pasteText.Length;
        tb.SelectionStart = tb.CaretIndex;
        tb.SelectionEnd = tb.CaretIndex;
    }

    private static string GetSelectedText(TextBox tb)
    {
        var text = tb.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(tb.SelectionStart, tb.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(tb.SelectionStart, tb.SelectionEnd), 0, text.Length);
        return end > start ? text.Substring(start, end - start) : string.Empty;
    }

    private static void ReplaceSelection(TextBox tb, string newText)
    {
        var text = tb.Text ?? string.Empty;
        var start = Math.Clamp(Math.Min(tb.SelectionStart, tb.SelectionEnd), 0, text.Length);
        var end = Math.Clamp(Math.Max(tb.SelectionStart, tb.SelectionEnd), 0, text.Length);
        tb.Text = text.Remove(start, end - start).Insert(start, newText);
        tb.CaretIndex = start + newText.Length;
        tb.SelectionStart = tb.CaretIndex;
        tb.SelectionEnd = tb.CaretIndex;
    }

    private static async Task<string?> GetClipboardText(Visual element)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(element)?.Clipboard;
            if (clipboard is null)
                return null;
            return await clipboard.TryGetTextAsync();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<bool> TrySetClipboardText(Visual element, string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(element)?.Clipboard;
            if (clipboard is null)
                return false;
            await clipboard.ClearAsync();
            await clipboard.SetTextAsync(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
