using System;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Provider;
using CapyCard.Services;

namespace CapyCard.Android.Services
{
    public class PhotoPickerServiceAndroid : IPhotoPickerService
    {
        public static TaskCompletionSource<Stream?>? CurrentTcs { get; private set; }

        public Task<Stream?> PickPhotoAsync()
        {
            var tcs = new TaskCompletionSource<Stream?>();
            CurrentTcs = tcs;

            try
            {
                var intent = new Intent(Intent.ActionPick, MediaStore.Images.Media.ExternalContentUri);
                intent.SetType("image/*");
                
                var activity = Xamarin.Essentials.Platform.CurrentActivity;
                if (activity != null)
                {
                    activity.StartActivityForResult(intent, MainActivity.PickImageId);
                }
                else
                {
                    tcs.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }

            return tcs.Task;
        }

        public static void OnActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode == MainActivity.PickImageId && CurrentTcs != null)
            {
                if (resultCode == Result.Ok && data?.Data != null)
                {
                    try
                    {
                        var uri = data.Data;
                        var context = Application.Context;
                        var stream = context.ContentResolver?.OpenInputStream(uri);
                        
                        if (stream != null)
                        {
                            // Copy to MemoryStream to avoid issues with stream being closed too early
                            var ms = new MemoryStream();
                            stream.CopyTo(ms);
                            stream.Close();
                            ms.Position = 0;
                            CurrentTcs.TrySetResult(ms);
                        }
                        else
                        {
                            CurrentTcs.TrySetResult(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        CurrentTcs.TrySetException(ex);
                    }
                }
                else
                {
                    CurrentTcs.TrySetResult(null);
                }
                CurrentTcs = null;
            }
        }
    }
}
