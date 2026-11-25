using System;
using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CherryTomato
{
    public static class PixelScrambler
    {
        // The Gilbert traversal and pixel shift are compatible with the ComfyUI node.
        // See ThirdPartyNotices.txt for the original license notices.
        private static void FillCurve(int x, int y, int ax, int ay, int bx, int by,
                                      int stride, int[] curve, ref int cursor)
        {
            int width = Math.Abs(ax + ay), height = Math.Abs(bx + by);
            int dx = Math.Sign(ax), dy = Math.Sign(ay), ex = Math.Sign(bx), ey = Math.Sign(by);
            if (height == 1 || width == 1)
            {
                int index = y * stride + x;
                int step = height == 1 ? dx + dy * stride : ex + ey * stride;
                for (int i = 0; i < Math.Max(width, height); i++, index += step)
                    curve[cursor++] = index;
                return;
            }

            // Arithmetic right shift preserves floor division for negative directions.
            int ax2 = ax >> 1, ay2 = ay >> 1, bx2 = bx >> 1, by2 = by >> 1;
            if (2L * width > 3L * height)
            {
                if ((Math.Abs(ax2 + ay2) & 1) != 0 && width > 2) { ax2 += dx; ay2 += dy; }
                FillCurve(x, y, ax2, ay2, bx, by, stride, curve, ref cursor);
                FillCurve(x + ax2, y + ay2, ax - ax2, ay - ay2, bx, by, stride, curve, ref cursor);
            }
            else
            {
                if ((Math.Abs(bx2 + by2) & 1) != 0 && height > 2) { bx2 += ex; by2 += ey; }
                FillCurve(x, y, bx2, by2, ax2, ay2, stride, curve, ref cursor);
                FillCurve(x + bx2, y + by2, ax, ay, bx - bx2, by - by2, stride, curve, ref cursor);
                FillCurve(x + ax - dx + bx2 - ex, y + ay - dy + by2 - ey,
                          -bx2, -by2, ax2 - ax, ay2 - ay, stride, curve, ref cursor);
            }
        }

        private static BitmapSource DisplayOrientation(BitmapFrame frame)
        {
            var metadata = frame.Metadata as BitmapMetadata;
            if (metadata == null || !(frame.Decoder is JpegBitmapDecoder)) return frame;
            object orientation = metadata.GetQuery("/app1/ifd/{ushort=274}");
            Matrix transform;
            switch (Convert.ToInt32(orientation))
            {
                case 2: transform = new Matrix(-1, 0, 0, 1, 0, 0); break;
                case 3: transform = new Matrix(-1, 0, 0, -1, 0, 0); break;
                case 4: transform = new Matrix(1, 0, 0, -1, 0, 0); break;
                case 5: transform = new Matrix(0, 1, 1, 0, 0, 0); break;
                case 6: transform = new Matrix(0, 1, -1, 0, 0, 0); break;
                case 7: transform = new Matrix(0, -1, -1, 0, 0, 0); break;
                case 8: transform = new Matrix(0, -1, 1, 0, 0, 0); break;
                default: return frame;
            }
            return new TransformedBitmap(frame, new MatrixTransform(transform));
        }

        public static BitmapSource ProcessFile(string path, bool encrypt)
        {
            BitmapFrame frame;
            using (FileStream stream = File.OpenRead(path))
                frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat,
                                            BitmapCacheOption.OnLoad);
            BitmapSource bitmap = DisplayOrientation(frame);
            if (bitmap.Format != PixelFormats.Bgra32)
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            int width = bitmap.PixelWidth, height = bitmap.PixelHeight;
            int count = checked(width * height), stride = checked(width * 4);
            int[] input = new int[count], output = new int[count], curve = new int[count];
            bitmap.CopyPixels(input, stride, 0);
            int cursor = 0;
            if (width >= height) FillCurve(0, 0, width, 0, 0, height, width, curve, ref cursor);
            else FillCurve(0, 0, 0, height, width, 0, width, curve, ref cursor);

            int shift = (int)Math.Floor((Math.Sqrt(5) - 1) / 2 * width * height + 0.5);
            int from = encrypt ? (count - shift) % count : shift % count;
            for (int i = 0; i < count; i++)
            {
                output[curve[i]] = input[curve[from]];
                if (++from == count) from = 0;
            }
            BitmapSource result = BitmapSource.Create(width, height, 96, 96,
                PixelFormats.Bgra32, null, output, stride);
            result.Freeze();
            return result;
        }

        private static void WritePixelsOnlyPng(BitmapSource image, Stream target)
        {
            // WIC can emit pHYs and other ancillary chunks even for a new bitmap.
            // Copy only PNG chunks required to reconstruct pixel values, keeping their CRCs.
            using (var encoded = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(encoded);
                byte[] bytes = encoded.GetBuffer();
                int end = checked((int)encoded.Length);
                target.Write(bytes, 0, 8);
                for (int pos = 8; pos < end; )
                {
                    int size = checked(12 + (bytes[pos] << 24) + (bytes[pos + 1] << 16)
                                      + (bytes[pos + 2] << 8) + bytes[pos + 3]);
                    string type = Encoding.ASCII.GetString(bytes, pos + 4, 4);
                    if (type == "IHDR" || type == "IDAT" || type == "IEND" ||
                        type == "PLTE" || type == "tRNS")
                        target.Write(bytes, pos, size);
                    pos = checked(pos + size);
                }
            }
        }

        public static string SaveResult(BitmapSource image, string inputPath, bool encrypt)
        {
            const string folder = @"F:\知识库\编程开发\图像加密解密\output";
            Directory.CreateDirectory(folder);
            string stem = Path.GetFileNameWithoutExtension(inputPath) + (encrypt ? "_加密" : "_解密");
            string temp = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write))
                    WritePixelsOnlyPng(image, stream);
                for (int n = 1; ; n++)
                {
                    string suffix = n == 1 ? "" : " (" + n + ")";
                    string path = Path.Combine(folder, stem + suffix + ".png");
                    try { File.Move(temp, path); return path; }
                    catch (IOException) { if (!File.Exists(path)) throw; }
                }
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
