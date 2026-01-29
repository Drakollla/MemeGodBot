using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MemeGodBot.ConsoleApp.Services
{
    public class ImageEncoder : IDisposable
    {
        private readonly InferenceSession _session;
        private const int ImageSize = 224;

        public ImageEncoder(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        public float[] GenerateEmbedding(string imagePath)
        {
            using var image = Image.Load<Rgb24>(imagePath);

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(ImageSize, ImageSize),
                Mode = ResizeMode.Crop
            }));

            var tensor = new DenseTensor<float>(new[] { 1, 3, ImageSize, ImageSize });

            // Константы нормализации (стандарт для CLIP)
            float[] mean = { 0.48145466f, 0.4578275f, 0.40821073f };
            float[] std = { 0.26862954f, 0.26130258f, 0.27577711f };

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < accessor.Width; x++)
                    {
                        // Нормализуем каждый канал (R, G, B)
                        tensor[0, 0, y, x] = (row[x].R / 255f - mean[0]) / std[0];
                        tensor[0, 1, y, x] = (row[x].G / 255f - mean[1]) / std[1];
                        tensor[0, 2, y, x] = (row[x].B / 255f - mean[2]) / std[2];
                    }
                }
            });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("pixel_values", tensor)
            };

            using var results = _session.Run(inputs);

            var output = results.First().AsEnumerable<float>().ToArray();

            return Normalize(output);
        }

        private float[] Normalize(float[] vector)
        {
            var norm = MathF.Sqrt(vector.Sum(x => x * x));
            return vector.Select(x => x / norm).ToArray();
        }

        public void Dispose() => _session.Dispose();
    }
}
