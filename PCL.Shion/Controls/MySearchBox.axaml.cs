using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using PCL.Shion.UI.Animation;

namespace PCL.Shion.Controls;

public partial class MySearchBox : MyCard
{
    public delegate void SearchEventHandler(object? sender, EventArgs e);

    public delegate void TextChangedEventHandler(object? sender, EventArgs e);

    public static readonly StyledProperty<string?> HintTextProperty =
        AvaloniaProperty.Register<MySearchBox, string?>(nameof(HintText), string.Empty);

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MySearchBox, string?>(nameof(Text), string.Empty);

    static MySearchBox()
    {
        HintTextProperty.Changed.AddClassHandler<MySearchBox>((searchBox, _) =>
            searchBox.TextBox.HintText = searchBox.HintText ?? string.Empty);
        TextProperty.Changed.AddClassHandler<MySearchBox>((searchBox, _) =>
            searchBox.TextBox.Text = searchBox.Text);
    }

    public MySearchBox()
    {
        InitializeComponent();

        Loaded += MySearchBox_Loaded;
    }

    // 属性
    public string HintText
    {
        get => GetValue(HintTextProperty) ?? string.Empty;
        set => SetValue(HintTextProperty, value);
    }

    public string Text
    {
        get => GetValue(TextProperty) ?? string.Empty;
        set => SetValue(TextProperty, value);
    }

    public bool SearchButtonVisibility
    {
        get => BtnSearch.IsVisible;
        set
        {
            BtnClear.Margin = new Thickness(0d, 0d, value ? 70 : 10, 0d);
            BtnSearch.IsVisible = value;
        }
    }

    public event TextChangedEventHandler? TextChanged;

    private void MySearchBox_Loaded(object? sender, RoutedEventArgs e)
    {
        TextBox.Focus();
    }

    private void Text_TextChanged(object? sender, RoutedEventArgs e)
    {
        UpdateClearButtonState();
        SetCurrentValue(TextProperty, TextBox.Text ?? string.Empty);

        TextChanged?.Invoke(sender, e);
    }

    private void BtnClear_Click(object? sender, PointerReleasedEventArgs e)
    {
        TextBox.Text = "";
        TextBox.Focus();
    }

    public event SearchEventHandler? Search;

    private void BtnSearch_Click(object? sender, PointerReleasedEventArgs e)
    {
        Search?.Invoke(sender, e);
    }

    private void UpdateClearButtonState()
    {
        var hasText = !string.IsNullOrEmpty(TextBox.Text);
        ModAnimation.AniStart(ModAnimation.AaOpacity(BtnClear, hasText ? 1d - BtnClear.Opacity : -BtnClear.Opacity, 90),
            "MySearchBox ClearBtn " + uuid);
        BtnClear.IsHitTestVisible = hasText;
    }
}
