using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PartFinder.Models;

public sealed class InfiniteCanvasColumnItem : INotifyPropertyChanged
{
    private int _index;
    private double _width;
    private double _height;
    private string _headerText = string.Empty;
    private string _cellContent = string.Empty;
    private bool _isLast;

    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value);
    }

    public double Width
    {
        get => _width;
        set => SetProperty(ref _width, value);
    }

    public double Height
    {
        get => _height;
        set => SetProperty(ref _height, value);
    }

    public string HeaderText
    {
        get => _headerText;
        set => SetProperty(ref _headerText, value);
    }

    public string CellContent
    {
        get => _cellContent;
        set => SetProperty(ref _cellContent, value);
    }

    public bool IsLast
    {
        get => _isLast;
        set => SetProperty(ref _isLast, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
