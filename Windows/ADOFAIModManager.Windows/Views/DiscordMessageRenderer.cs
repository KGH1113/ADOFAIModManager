using ADOFAIModManager.Windows.Application.Catalog;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using Windows.UI.Text;

namespace ADOFAIModManager.Windows.Views;

internal static class DiscordMessageRenderer
{
    public static void Render(Panel destination, string message)
    {
        destination.Children.Clear();
        foreach (var block in DiscordMessageParser.Parse(message))
            destination.Children.Add(CreateBlock(block));
    }

    private static UIElement CreateBlock(DiscordMessageBlock block) => block switch
    {
        DiscordHeading heading => CreateText(heading.Content, heading.Level switch { 1 => 24, 2 => 20, _ => 17 }, FontWeights.SemiBold),
        DiscordParagraph paragraph => CreateText(paragraph.Content, 15, FontWeights.Normal),
        DiscordSubtext subtext => CreateSubtext(subtext),
        DiscordCodeBlock code => new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(6),
            Background = Brush("ControlFillColorSecondaryBrush"),
            Child = CreateCodeBlock(code)
        },
        DiscordBulletList list => CreateList(list),
        DiscordOrderedList list => CreateOrderedList(list),
        DiscordTable table => CreateTable(table),
        DiscordDivider => new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4, 0, 4),
            Background = Brush("DividerStrokeColorDefaultBrush")
        },
        DiscordQuote quote => new Border
        {
            BorderBrush = Brush("AccentFillColorDefaultBrush"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 2, 0, 2),
            Child = CreateQuote(quote)
        },
        _ => new TextBlock()
    };

    private static TextBlock CreateSubtext(DiscordSubtext subtext)
    {
        var text = CreateText(subtext.Content, 12, FontWeights.Normal);
        text.Foreground = Brush("TextFillColorSecondaryBrush");
        return text;
    }

    private static StackPanel CreateCodeBlock(DiscordCodeBlock code)
    {
        var panel = new StackPanel { Spacing = 7 };
        if (!string.IsNullOrWhiteSpace(code.Language))
            panel.Children.Add(new TextBlock { Text = code.Language, FontSize = 11, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock
        {
            Text = code.Code,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true
        });
        return panel;
    }

    private static StackPanel CreateList(DiscordBulletList list)
    {
        var panel = new StackPanel { Spacing = 5 };
        foreach (var item in list.Items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
            row.Children.Add(new TextBlock { Text = "•", FontSize = 15 });
            row.Children.Add(CreateText(item, 15, FontWeights.Normal));
            panel.Children.Add(row);
        }
        return panel;
    }

    private static StackPanel CreateOrderedList(DiscordOrderedList list)
    {
        var panel = new StackPanel { Spacing = 5 };
        for (var index = 0; index < list.Items.Count; index++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9 };
            row.Children.Add(new TextBlock { Text = $"{list.Start + index}.", FontSize = 15 });
            row.Children.Add(CreateText(list.Items[index], 15, FontWeights.Normal));
            panel.Children.Add(row);
        }
        return panel;
    }

    private static ScrollViewer CreateTable(DiscordTable table)
    {
        var grid = new Grid { BorderBrush = Brush("DividerStrokeColorDefaultBrush"), BorderThickness = new Thickness(1) };
        var columns = table.Rows.Count == 0 ? 0 : table.Rows.Max(row => row.Count);
        for (var column = 0; column < columns; column++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        for (var row = 0; row < table.Rows.Count; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (var column = 0; column < table.Rows[row].Count; column++)
            {
                var cell = CreateText(table.Rows[row][column], 14, row == 0 ? FontWeights.SemiBold : FontWeights.Normal);
                var border = new Border
                {
                    Padding = new Thickness(8),
                    BorderBrush = Brush("DividerStrokeColorDefaultBrush"),
                    BorderThickness = new Thickness(column == 0 ? 0 : 1, row == 0 ? 0 : 1, 0, 0),
                    Child = cell
                };
                Grid.SetColumn(border, column);
                Grid.SetRow(border, row);
                grid.Children.Add(border);
            }
        }
        return new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = grid };
    }

    private static StackPanel CreateQuote(DiscordQuote quote)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var block in quote.Blocks) panel.Children.Add(CreateBlock(block));
        return panel;
    }

    private static TextBlock CreateText(IReadOnlyList<DiscordInline> content, double size, FontWeight weight)
    {
        var text = new TextBlock { FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        foreach (var item in content)
        {
            var decorations = TextDecorations.None;
            if (item.Underline) decorations |= TextDecorations.Underline;
            if (item.Strikethrough) decorations |= TextDecorations.Strikethrough;
            var run = new Run
            {
                Text = item.Text,
                FontWeight = item.Bold ? FontWeights.SemiBold : FontWeights.Normal,
                FontStyle = item.Italic ? FontStyle.Italic : FontStyle.Normal,
                TextDecorations = decorations,
                FontFamily = item.Code ? new FontFamily("Consolas") : null,
                Foreground = item.Spoiler ? Brush("TextFillColorSecondaryBrush") : null
            };
            if (item.Link is { } link)
            {
                var hyperlink = new Hyperlink { NavigateUri = link };
                hyperlink.Inlines.Add(run);
                text.Inlines.Add(hyperlink);
            }
            else text.Inlines.Add(run);
        }
        return text;
    }

    private static Brush Brush(string key) => (Brush)Microsoft.UI.Xaml.Application.Current.Resources[key];
}
