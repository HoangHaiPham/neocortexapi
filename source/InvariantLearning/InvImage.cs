﻿using SkiaSharp;
public class InvImage
{
    private string imagePath;

    public string label;

    public int imageWidth;

    public int imageHeight;

    public InvImage(string imagePath, string label)
    {
        this.label = label;
        this.imagePath = imagePath;
        imageWidth = SKBitmap.Decode(this.imagePath).Width;
        imageHeight = SKBitmap.Decode(this.imagePath).Height;
    }

    public double[,,] GetPixels()
    {
        int lastXIndex = SKBitmap.Decode(this.imagePath).Width-1;
        int lastYIndex = SKBitmap.Decode(this.imagePath).Height-1;
        return GetPixels(new InvFrame(0, 0, lastXIndex, lastYIndex));
    }
    public double[,,] GetPixels(InvFrame frame)
    {
        using (SKBitmap inputBitmap = SKBitmap.Decode(this.imagePath))
        {

            double[,,] colorData = new double[frame.brX-frame.tlX+1, frame.brY-frame.tlY+1, 3];

            //
            //Get image pixels array and stride of image
            byte[] pixels = inputBitmap.Bytes;
            int stride = inputBitmap.Info.RowBytes;

            int bytesPerPixel = inputBitmap.Info.BytesPerPixel;

            for (int y = frame.brY; y >= frame.tlY; y--)
            {
                int currentLine = y * stride;
                for (int x = frame.brX*bytesPerPixel; x >=frame.tlX * bytesPerPixel; x = x - bytesPerPixel)
                {
                    //
                    // The 3D array colors is in RGB order like the colors order of input Skbitmap.                    
                    colorData[x / bytesPerPixel - frame.tlX, y - frame.tlY, 0] = pixels[currentLine + x];
                    colorData[x / bytesPerPixel - frame.tlX, y - frame.tlY, 1] = pixels[currentLine + x + 1];
                    colorData[x / bytesPerPixel - frame.tlX, y - frame.tlY, 2] = pixels[currentLine + x + 2];
                }
            }
            return colorData;
        };
    }

    /// <summary>
    /// Save an Image in png, BRG format from a 2D matrix of BRG values
    /// </summary>
    /// <param name="colorData"></param>
    /// <exception cref="NotImplementedException"></exception>
    public static void SaveAsImage(double[,,] colorData, string outputPath)
    {
        SKBitmap outputBitmap = new SKBitmap(colorData.GetLength(0), colorData.GetLength(1));

        for (int y = 0; y < outputBitmap.Height; y++)
        {
            for (int x = 0; x < outputBitmap.Width; x++)
            {
                int r = (int)colorData[x, y, 0];
                int g = (int)colorData[x, y, 1];
                int b = (int)colorData[x, y, 2];
                outputBitmap.SetPixel(x, y, new SKColor((byte)(b), (byte)(g), (byte)(r)));
            }
        }

        //
        // 3. Reading a Picture file in pixels
        using (var image = SKImage.FromBitmap(outputBitmap))
        {
            string encodingFormat = "Png";
            SKEncodedImageFormat frm = (SKEncodedImageFormat)Enum.Parse(typeof(SKEncodedImageFormat), encodingFormat);


            using (var data = image.Encode(frm, 80))
            {
                // save the data to a stream
                using (var stream = File.OpenWrite($"{outputPath}"))
                {
                    data.SaveTo(stream);
                }
            }
        }
    }
}