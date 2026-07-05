using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MucLucHoSo.App.Services;

/// <summary>
/// Render các trang PDF thành ảnh (BitmapImage đã Freeze) bằng Windows.Data.Pdf — có sẵn trên Windows 10+,
/// không cần WebView2/trình duyệt, hiển thị nhanh bằng Image.
/// </summary>
public sealed class PdfRenderService
{
    public async Task<List<ImageSource>> RenderAsync(string pdfPath, uint width = 1000)
    {
        var list = new List<ImageSource>();
        var file = await StorageFile.GetFileFromPathAsync(pdfPath);
        var doc = await PdfDocument.LoadFromFileAsync(file);
        for (uint i = 0; i < doc.PageCount; i++)
        {
            using var page = doc.GetPage(i);
            using var ras = new InMemoryRandomAccessStream();
            await page.RenderToStreamAsync(ras, new PdfPageRenderOptions { DestinationWidth = width });

            var size = (uint)ras.Size;
            var bytes = new byte[size];
            using (var reader = new DataReader(ras.GetInputStreamAt(0)))
            {
                await reader.LoadAsync(size);
                reader.ReadBytes(bytes);
            }
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(bytes);
            bmp.EndInit();
            bmp.Freeze();
            list.Add(bmp);
        }
        return list;
    }
}
