// Copyright (c) 2026 Dennis Liu. All rights reserved.
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Path = Avalonia.Controls.Shapes.Path;

namespace NvtFwCombiner.Presentation.Avalonia.Views;

/// <summary>One in-place disclosure; its body visibility is the UI-thread-only expansion state.</summary>
internal sealed class OutputLayoutDisclosure
{
    private readonly Path _chevron = OutputLayoutVisuals.Chevron();

    internal OutputLayoutDisclosure(Control label, Control body, string name, string id, bool expanded = false, bool chevronFirst = false)
    {
        Body = body;
        Toggle = new RelayCommand(() => SetExpanded(!IsExpanded));
        Header = new Button
        {
            Classes = { "hexChangedBlockRow" },
            Command = Toggle,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        Grid header = OutputLayoutVisuals.Grid(chevronFirst ? "16,*" : "*,16");
        Grid.SetColumn(label, chevronFirst ? 1 : 0);
        header.Children.Add(label);
        Grid.SetColumn(_chevron, chevronFirst ? 0 : 1);
        header.Children.Add(_chevron);
        Header.Content = header;
        OutputLayoutVisuals.Name(Header, name, id);
        Root = OutputLayoutVisuals.Stack("NfcSpace4");
        Root.Children.Add(Header);
        Root.Children.Add(Body);
        SetExpanded(expanded);
    }

    internal StackPanel Root { get; }
    internal Button Header { get; }
    internal Control Body { get; }
    internal RelayCommand Toggle { get; }
    internal bool IsExpanded => Body.IsVisible;
    internal void SetExpanded(bool value)
    {
        Body.IsVisible = value;
        _chevron.Data = Geometry.Parse(value ? "M 3,6 L 8,11 L 13,6" : "M 6,3 L 11,8 L 6,13");
    }

    internal bool Owns(Control? control)
    {
        for (Control? current = control; current is not null; current = current.Parent as Control)
        {
            if (ReferenceEquals(current, Root)) { return true; }
        }
        return false;
    }

    internal void Focus()
    {
        _ = Header.Focus(NavigationMethod.Directional);
    }
}
