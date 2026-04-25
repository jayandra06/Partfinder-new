using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using PartFinder.Models;
using PartFinder.ViewModels;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace PartFinder.Views.Pages;

public sealed partial class QrCodeManagerPage : Page
{
    private readonly QrCodeManagerViewModel _viewModel;

    public QrCodeManagerPage()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<QrCodeManagerViewModel>();
        DataContext = _viewModel;
        Loaded += OnLoaded;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QrCodeManagerViewModel.PreviewQrImageBytes))
        {
            UpdatePreviewImage();
        }
    }

    private async void UpdatePreviewImage()
    {
        if (_viewModel.PreviewQrImageBytes is null || _viewModel.PreviewQrImageBytes.Length == 0)
        {
            QrPreviewImage.Source = null;
            return;
        }

        var bitmap = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(_viewModel.PreviewQrImageBytes.AsBuffer());
        stream.Seek(0);
        await bitmap.SetSourceAsync(stream);
        QrPreviewImage.Source = bitmap;
    }

    private void OnGenerateSingleClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PartRecord record })
        {
            _viewModel.GenerateSingleQrCommand.Execute(record);
        }
    }

    private async void OnSaveQrClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.PreviewQrImageBytes is null || _viewModel.PreviewQrImageBytes.Length == 0 || App.MainAppWindow is null)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = "partfinder-qr-code",
        };
        picker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainAppWindow));
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await Windows.Storage.FileIO.WriteBytesAsync(file, _viewModel.PreviewQrImageBytes);

        if (XamlRoot is not null)
        {
            var dlg = new ContentDialog
            {
                Title = "QR Code Saved",
                Content = $"QR code saved to:\n{file.Path}",
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };
            _ = await dlg.ShowAsync();
        }
    }

    private void OnCopyPayloadClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.PreviewQrText))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(_viewModel.PreviewQrText);
        Clipboard.SetContent(package);
    }
}
