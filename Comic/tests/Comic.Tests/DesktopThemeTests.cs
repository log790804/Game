using System.Xml.Linq;

namespace Comic.Tests;

public sealed class DesktopThemeTests
{
    [Fact]
    public void AppTheme_DefinesReadableComboBoxAndItemColors()
    {
        var document = XDocument.Load(FindProjectFile("src", "Comic.Desktop", "App.xaml"));
        XNamespace presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        var comboBoxStyle = FindImplicitStyle(document, "ComboBox");
        var comboBoxItemStyle = FindImplicitStyle(document, "ComboBoxItem");

        Assert.Equal("#12181D", FindSetterValue(comboBoxStyle, presentation, "Foreground"));
        Assert.Equal("#F2F5F7", FindSetterValue(comboBoxStyle, presentation, "Background"));
        Assert.Equal("#12181D", FindSetterValue(comboBoxItemStyle, presentation, "Foreground"));
        Assert.Equal("#F2F5F7", FindSetterValue(comboBoxItemStyle, presentation, "Background"));
    }

    [Fact]
    public void AppTheme_DefinesReadableActiveAndInactiveHighlightColors()
    {
        var document = XDocument.Load(FindProjectFile("src", "Comic.Desktop", "App.xaml"));

        AssertBrushColor(document, "{x:Static SystemColors.HighlightBrushKey}", "#37A57C");
        AssertBrushColor(document, "{x:Static SystemColors.HighlightTextBrushKey}", "#08130F");
        AssertBrushColor(document, "{x:Static SystemColors.InactiveSelectionHighlightBrushKey}", "#3C574D");
        AssertBrushColor(document, "{x:Static SystemColors.InactiveSelectionHighlightTextBrushKey}", "#F2F5F7");
    }

    [Fact]
    public void ReaderSelectors_UseTwoWayBindingsAndExplainAvailableChoices()
    {
        var document = XDocument.Load(FindProjectFile("src", "Comic.Desktop", "MainWindow.xaml"));
        var comboBoxes = document.Descendants()
            .Where(element => element.Name.LocalName == "ComboBox")
            .ToArray();
        var comicSelector = FindSelector(comboBoxes, "選擇離線漫畫");
        var chapterSelector = FindSelector(comboBoxes, "選擇離線章節");

        Assert.Contains("Mode=TwoWay", FindAttribute(comicSelector, "SelectedItem"));
        Assert.Contains("UpdateSourceTrigger=PropertyChanged", FindAttribute(comicSelector, "SelectedItem"));
        Assert.Contains("Mode=TwoWay", FindAttribute(chapterSelector, "SelectedItem"));
        Assert.Contains("UpdateSourceTrigger=PropertyChanged", FindAttribute(chapterSelector, "SelectedItem"));
        AssertSelectorUsesReadableItemTemplate(comicSelector);
        AssertSelectorUsesReadableItemTemplate(chapterSelector);
        Assert.Equal(
            2,
            comicSelector.Parent!
                .Elements()
                .Single(element => element.Name.LocalName == "Grid.RowDefinitions")
                .Elements()
                .Count(element => element.Name.LocalName == "RowDefinition"));
        Assert.Contains(
            document.Descendants().Where(element => element.Name.LocalName == "TextBlock"),
            element => FindAttribute(element, "Text") == "{Binding Reader.SelectionHint}");
    }

    [Fact]
    public void ReaderView_ProvidesAccessibleImageWidthControlsAndBinding()
    {
        var document = XDocument.Load(FindProjectFile("src", "Comic.Desktop", "MainWindow.xaml"));
        var slider = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "Slider" &&
                FindAttribute(element, "AutomationProperties.Name") == "調整漫畫圖片寬度");
        var readerImage = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "Image" &&
                FindAttribute(element, "AutomationProperties.Name") == "{Binding PageLabel}");
        var buttons = document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToArray();

        Assert.Contains("ReaderDisplayWidthPolicy.Minimum", FindAttribute(slider, "Minimum"));
        Assert.Contains("ReaderDisplayWidthPolicy.Maximum", FindAttribute(slider, "Maximum"));
        Assert.Contains("ReaderDisplayWidthPolicy.Step", FindAttribute(slider, "TickFrequency"));
        Assert.Contains("Reader.PageMaxWidth", FindAttribute(slider, "Value"));
        Assert.Contains("Mode=TwoWay", FindAttribute(slider, "Value"));
        Assert.Contains("Reader.PageMaxWidth", FindAttribute(readerImage, "MaxWidth"));
        Assert.Contains(buttons, button =>
            FindAttribute(button, "AutomationProperties.Name") == "縮小漫畫圖片");
        Assert.Contains(buttons, button =>
            FindAttribute(button, "AutomationProperties.Name") == "放大漫畫圖片");
        Assert.Contains(buttons, button =>
            FindAttribute(button, "AutomationProperties.Name") == "重設漫畫圖片寬度");
    }

    [Fact]
    public void ReaderView_ProvidesCheckLatestAction()
    {
        var document = XDocument.Load(FindProjectFile("src", "Comic.Desktop", "MainWindow.xaml"));
        var button = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "Button" &&
                FindAttribute(element, "AutomationProperties.Name") == "查看所選漫畫最新章節");

        Assert.Equal("查看最新", FindAttribute(button, "Content"));
        Assert.Equal("OnCheckLatestClick", FindAttribute(button, "Click"));
        Assert.Equal("{Binding Reader.CanCheckLatest}", FindAttribute(button, "IsEnabled"));
    }

    [Fact]
    public void DownloadModeSelector_IsAccessibleAndDisabledDuringOperations()
    {
        var document = XDocument.Load(FindProjectFile("src", "Comic.Desktop", "MainWindow.xaml"));
        var selector = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "ComboBox" &&
                FindAttribute(element, "AutomationProperties.Name") == "下載模式");

        Assert.Equal("{Binding DownloadModes}", FindAttribute(selector, "ItemsSource"));
        Assert.Contains("SelectedDownloadMode", FindAttribute(selector, "SelectedValue"));
        Assert.Contains("Mode=TwoWay", FindAttribute(selector, "SelectedValue"));
        Assert.Equal("{Binding CanStartOperation}", FindAttribute(selector, "IsEnabled"));
        AssertSelectorUsesReadableItemTemplate(selector);
        Assert.Contains(
            document.Descendants().Where(element => element.Name.LocalName == "TextBlock"),
            element => FindAttribute(element, "Text") == "{Binding DownloadModeDescription}");
    }

    [Fact]
    public void ReaderView_UsesAFullHeightViewportBesideItsControls()
    {
        var document = XDocument.Load(FindProjectFile("src", "Comic.Desktop", "MainWindow.xaml"));
        var readerTab = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "TabItem" &&
                FindAttribute(element, "Header") == "離線閱讀 (_R)");
        var readerLayout = readerTab.Elements()
            .Single(element => element.Name.LocalName == "Grid");
        var columns = readerLayout.Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .Where(element => element.Name.LocalName == "ColumnDefinition")
            .Select(element => FindAttribute(element, "Width"))
            .ToArray();
        var controls = readerLayout.Descendants()
            .Single(element =>
                element.Name.LocalName == "ScrollViewer" &&
                FindAttribute(element, "AutomationProperties.Name") == "離線閱讀控制區");
        var viewport = readerLayout.Descendants()
            .Single(element =>
                element.Name.LocalName == "Border" &&
                FindAttribute(element, "Name") == "ReaderViewport");

        Assert.Equal(["420", "12", "*"], columns);
        Assert.Equal("0", FindAttribute(controls, "Grid.Column"));
        Assert.Equal("2", FindAttribute(viewport, "Grid.Column"));
        Assert.DoesNotContain(
            readerLayout.Elements(),
            element => element.Name.LocalName == "Grid.RowDefinitions");
    }

    [Fact]
    public void ReaderMode_CanCollapseSharedWindowChrome()
    {
        var document = XDocument.Load(FindProjectFile("src", "Comic.Desktop", "MainWindow.xaml"));
        var mainTabs = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "TabControl" &&
                FindAttribute(element, "Name") == "MainTabs");
        var appHeader = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "Border" &&
                FindAttribute(element, "Name") == "AppHeader");
        var globalStatusBar = document.Descendants()
            .Single(element =>
                element.Name.LocalName == "StatusBar" &&
                FindAttribute(element, "Name") == "GlobalStatusBar");

        Assert.Equal("OnMainTabsSelectionChanged", FindAttribute(mainTabs, "SelectionChanged"));
        Assert.Equal("0", FindAttribute(appHeader, "Grid.Row"));
        Assert.Equal("2", FindAttribute(globalStatusBar, "Grid.Row"));
    }

    private static void AssertSelectorUsesReadableItemTemplate(XElement selector)
    {
        var textBlock = selector
            .Elements()
            .Single(element => element.Name.LocalName == "ComboBox.ItemTemplate")
            .Descendants()
            .Single(element => element.Name.LocalName == "TextBlock");

        Assert.Equal("#12181D", FindAttribute(textBlock, "Foreground"));
        Assert.Equal("{Binding DisplayName}", FindAttribute(textBlock, "Text"));
    }

    private static XElement FindSelector(IEnumerable<XElement> comboBoxes, string accessibleName) =>
        comboBoxes.Single(element =>
            FindAttribute(element, "AutomationProperties.Name") == accessibleName);

    private static string FindAttribute(XElement element, string name) =>
        element.Attributes()
            .SingleOrDefault(attribute => attribute.Name.LocalName == name)
            ?.Value ?? string.Empty;

    private static XElement FindImplicitStyle(XDocument document, string targetType) =>
        document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "Style" &&
                element.Attribute("TargetType")?.Value == targetType);

    private static void AssertBrushColor(XDocument document, string resourceKey, string expectedColor)
    {
        var brush = document
            .Descendants()
            .Single(element =>
                element.Name.LocalName == "SolidColorBrush" &&
                element.Attributes().Any(attribute =>
                    attribute.Name.LocalName == "Key" && attribute.Value == resourceKey));

        Assert.Equal(expectedColor, brush.Attribute("Color")?.Value);
    }

    private static string? FindSetterValue(
        XElement style,
        XNamespace presentation,
        string property) =>
        style
            .Elements(presentation + "Setter")
            .Single(element => element.Attribute("Property")?.Value == property)
            .Attribute("Value")?.Value;

    private static string FindProjectFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"找不到專案檔案：{Path.Combine(relativeSegments)}。");
    }
}
