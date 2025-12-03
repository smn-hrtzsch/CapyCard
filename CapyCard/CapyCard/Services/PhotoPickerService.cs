using System;
using System.IO;
using System.Threading.Tasks;

namespace CapyCard.Services
{
    public interface IPhotoPickerService
    {
        Task<Stream?> PickPhotoAsync();
    }

    public static class PhotoPickerService
    {
        public static IPhotoPickerService? Current { get; set; }
    }
}
