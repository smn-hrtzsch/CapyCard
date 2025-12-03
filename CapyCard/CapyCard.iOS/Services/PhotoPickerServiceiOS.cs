using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapyCard.Services;
using Foundation;
using PhotosUI;
using UIKit;
using UniformTypeIdentifiers;

namespace CapyCard.iOS.Services
{
    public class PhotoPickerServiceiOS : IPhotoPickerService
    {
        public Task<Stream?> PickPhotoAsync()
        {
            var tcs = new TaskCompletionSource<Stream?>();

            UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                try
                {
                    // Use IsIOSVersionAtLeast to satisfy CA1416 analyzer
                    if (!OperatingSystem.IsIOSVersionAtLeast(14))
                    {
                        Console.WriteLine("[PhotoPicker] iOS version too old (< 14.0)");
                        tcs.TrySetResult(null); 
                        return;
                    }

#pragma warning disable CA1416
                    Console.WriteLine("[PhotoPicker] Opening PHPickerViewController");
                    var config = new PHPickerConfiguration();
                    config.SelectionLimit = 1;
                    config.Filter = PHPickerFilter.ImagesFilter;

                    var picker = new PHPickerViewController(config);
                    var delegateHandler = new PhotoPickerDelegate(tcs);
                    picker.Delegate = delegateHandler;

                    var window = UIApplication.SharedApplication.ConnectedScenes
                        .OfType<UIWindowScene>()
                        .SelectMany(s => s.Windows)
                        .FirstOrDefault(w => w.IsKeyWindow);

                    var vc = window?.RootViewController;
                    
                    while (vc?.PresentedViewController != null)
                    {
                        vc = vc.PresentedViewController;
                    }

                    if (vc != null)
                    {
                        vc.PresentViewController(picker, true, null);
                    }
                    else
                    {
                        Console.WriteLine("[PhotoPicker] No RootViewController found");
                        tcs.TrySetResult(null);
                    }
#pragma warning restore CA1416
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PhotoPicker] Exception starting picker: {ex}");
                    tcs.TrySetException(ex);
                }
            });

            return tcs.Task;
        }

        private class PhotoPickerDelegate : PHPickerViewControllerDelegate
        {
            private readonly TaskCompletionSource<Stream?> _tcs;

            public PhotoPickerDelegate(TaskCompletionSource<Stream?> tcs)
            {
                _tcs = tcs;
            }

            public override void DidFinishPicking(PHPickerViewController picker, PHPickerResult[] results)
            {
#pragma warning disable CA1416 // Validate platform compatibility
                Console.WriteLine($"[PhotoPicker] DidFinishPicking. Results: {results.Length}");
                picker.DismissViewController(true, null);

                if (results.Length == 0)
                {
                    _tcs.TrySetResult(null);
                    return;
                }

                var result = results[0];
                var itemProvider = result.ItemProvider;

                // Use LoadDataRepresentation to get raw data and convert to UIImage manually
                // This avoids NSItemProviderReadingWrapper marshalling issues
                if (itemProvider.HasItemConformingTo(UTTypes.Image.Identifier))
                {
                    Console.WriteLine("[PhotoPicker] Item conforms to UTTypes.Image. Loading data representation...");
                    
                    itemProvider.LoadDataRepresentation(UTTypes.Image, (data, error) =>
                    {
                        if (error != null)
                        {
                            Console.WriteLine($"[PhotoPicker] Error loading data: {error.LocalizedDescription}");
                            _tcs.TrySetResult(null);
                            return;
                        }

                        if (data != null)
                        {
                            Console.WriteLine($"[PhotoPicker] Data loaded. Length: {data.Length}");
                            try
                            {
                                var image = UIImage.LoadFromData(data);
                                if (image != null)
                                {
                                    Console.WriteLine($"[PhotoPicker] UIImage created from data. Size: {image.Size}");
                                    var jpegData = image.AsJPEG(0.8f);
                                    if (jpegData != null)
                                    {
                                        var stream = new MemoryStream();
                                        using (var nsStream = jpegData.AsStream())
                                        {
                                            nsStream.CopyTo(stream);
                                        }
                                        stream.Position = 0;
                                        Console.WriteLine($"[PhotoPicker] JPEG Stream created. Length: {stream.Length}");
                                        _tcs.TrySetResult(stream);
                                    }
                                    else
                                    {
                                        Console.WriteLine("[PhotoPicker] Failed to convert to JPEG");
                                        _tcs.TrySetResult(null);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("[PhotoPicker] Failed to create UIImage from data");
                                    _tcs.TrySetResult(null);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[PhotoPicker] Exception processing image data: {ex}");
                                _tcs.TrySetResult(null);
                            }
                        }
                        else
                        {
                            Console.WriteLine("[PhotoPicker] Data is null");
                            _tcs.TrySetResult(null);
                        }
                    });
                }
                else
                {
                    Console.WriteLine("[PhotoPicker] Item does not conform to UTType.Image");
                     // Debugging: List available types
                    foreach (var type in itemProvider.RegisteredTypeIdentifiers)
                    {
                        Console.WriteLine($"[PhotoPicker] Available Type: {type}");
                    }
                    _tcs.TrySetResult(null);
                }
#pragma warning restore CA1416 // Validate platform compatibility
            }
        }
    }
}
