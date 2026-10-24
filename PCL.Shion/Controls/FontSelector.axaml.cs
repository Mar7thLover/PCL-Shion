using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using PCL.Shion.Localization;

namespace PCL.Shion.Controls;

public partial class FontSelector : ContentControl
{
    public delegate void SelectionChangedEventHandler(object? sender, SelectionChangedEventArgs e);

    public static readonly StyledProperty<string?> TooltipProperty =
        AvaloniaProperty.Register<FontSelector, string?>(nameof(Tooltip));

    static FontSelector()
    {
        TooltipProperty.Changed.AddClassHandler<FontSelector>((control, e) =>
            ToolTip.SetTip(control.ComboFont, e.NewValue));
    }

    private bool _isInitializing;
    private bool _isListeningLanguageChanged;
    private string? _pendingFontTag;

    public FontSelector()
    {
        InitializeComponent();
        ComboFont.ItemsSource = CustomFontCollection;
        Loaded += FontSelector_Loaded;
        Unloaded += FontSelector_Unloaded;
        ComboFont.SelectionChanged += ComboFont_SelectionChanged;
    }

    public string? Tooltip
    {
        get => GetValue(TooltipProperty);
        set => SetValue(TooltipProperty, value);
    }

    public ObservableCollection<CustomFontProperties> CustomFontCollection { get; } = [];

    public string SelectedFontTag
    {
        get
        {
            if (ComboFont.SelectedItem is null) return "";
            return ComboFont.SelectedItem is not CustomFontProperties selectedFont ? "" : selectedFont.Tag;
        }
        set
        {
            // 如果字体还在加载中，延迟设置
            if (CustomFontCollection.Count == 0 ||
                (CustomFontCollection is [{ Name: var name }] && name == Lang.Text("Common.State.Loading")))
            {
                _pendingFontTag = value;
                return;
            }

            _isInitializing = true;

            var targetSelection = CustomFontCollection.FirstOrDefault(i => i.Tag == value);
            if (targetSelection is null)
                ComboFont.SelectedIndex = 0;
            else
                ComboFont.SelectedItem = targetSelection;

            _isInitializing = false;
        }
    }

    public int SelectedIndex
    {
        get => ComboFont.SelectedIndex;
        set => ComboFont.SelectedIndex = value;
    }

    public new bool IsEnabled
    {
        get => ComboFont.IsEnabled;
        set => ComboFont.IsEnabled = value;
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    private void FontSelector_Loaded(object? sender, EventArgs e)
    {
        StartListeningLanguageChanged();

        if (CustomFontCollection.Count == 0)
            LoadFonts();
        else
            RefreshDefaultFont();
    }

    private void FontSelector_Unloaded(object? sender, EventArgs e)
    {
        StopListeningLanguageChanged();
    }

    private void StartListeningLanguageChanged()
    {
        if (_isListeningLanguageChanged) return;
        LocalizationService.LanguageChanged += LocalizationService_LanguageChanged;
        _isListeningLanguageChanged = true;
    }

    private void StopListeningLanguageChanged()
    {
        if (!_isListeningLanguageChanged) return;
        LocalizationService.LanguageChanged -= LocalizationService_LanguageChanged;
        _isListeningLanguageChanged = false;
    }

    private void LocalizationService_LanguageChanged()
    {
        RefreshDefaultFont();
    }

    private void RefreshDefaultFont()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Invoke(RefreshDefaultFont);
            return;
        }

        GetDefaultFont()?.Font = LocalizationFontService.BuildLaunchFontFamily();
    }

    private void LoadFonts()
    {
        Dispatcher.UIThread.Post(() => _ = LoadFontsAsync());
    }

    private async Task LoadFontsAsync()
    {
        ComboFont.IsEnabled = false;
        _isInitializing = true;
        CustomFontCollection.Add(new CustomFontProperties { Name = Lang.Text("Common.State.Loading") });
        ComboFont.SelectedIndex = 0;

        var availableFontNames = await Task.Run(() =>
            FontManager.Current.SystemFonts
                .Select(font => font.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList());

        CustomFontCollection.Clear();
        CustomFontCollection.Add(new CustomFontProperties
        {
            Name = Lang.Text("Common.Option.Default"),
            Font = LocalizationFontService.BuildLaunchFontFamily(),
            Tag = ""
        });

        foreach (var fontName in availableFontNames)
            CustomFontCollection.Add(new CustomFontProperties
                { Name = fontName, Font = new FontFamily(fontName), Tag = fontName });

        ComboFont.IsEnabled = true;

        if (_pendingFontTag is not null)
        {
            var pendingTag = _pendingFontTag;
            _pendingFontTag = null;
            SelectedFontTag = pendingTag;
        }

        _isInitializing = false;
    }

    private void ComboFont_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_isInitializing) SelectionChanged?.Invoke(sender, e);
    }

    private CustomFontProperties? GetDefaultFont()
    {
        return CustomFontCollection.FirstOrDefault(i => string.IsNullOrEmpty(i.Tag));
    }

    public class CustomFontProperties : INotifyPropertyChanged
    {
        public string Name
        {
            get;
            init => SetField(ref field, value);
        } = string.Empty;

        public FontFamily Font
        {
            get;
            set => SetField(ref field, value);
        } = LocalizationFontService.BuildLaunchFontFamily();

        public string Tag
        {
            get;
            set => SetField(ref field, value);
        } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
